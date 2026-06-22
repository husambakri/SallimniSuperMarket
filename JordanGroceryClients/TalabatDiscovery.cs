using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JordanGrocery;

/// <summary>
/// اكتشاف متاجر البقالة في مدينة من طلبات (بلا قائمة يدوية):
/// يقرأ خريطة مناطق البقالة (sitemap)، يزحف صفحات المناطق، يُبقي ما كانت
/// مدينتها = citySlug، ويجمع متاجرها (id=معرّف الفرع، name، وaid=معرّف المنطقة
/// الذي يُفتح به كتالوج المتجر). يوحّد النتائج بالاسم (فرع واحد لكل متجر).
/// مُهدّأ و429-آمن: أي فشل/حظر يُتجاهَل ويُعاد ما جُمِّع.
/// </summary>
public static class TalabatDiscovery
{
    private const string BaseUrl     = "https://www.talabat.com";
    private const string AreaSitemap = "/sitemap/jordan/en/groceries_areas.xml.gz";

    private static readonly TimeSpan PaceDelay      = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromSeconds(8);
    private const int DiscoveryConcurrency = 3;

    private static HttpClient NewClient()
    {
        var jar = new CookieContainer();
        var uri = new Uri(BaseUrl);
        jar.Add(uri, new Cookie("tlb_country",  "jordan"));
        jar.Add(uri, new Cookie("tlb_vertical", "1"));
        jar.Add(uri, new Cookie("next-i18next", "en"));
        var http = new HttpClient(new HttpClientHandler { CookieContainer = jar, AllowAutoRedirect = true })
        {
            BaseAddress = uri,
            Timeout = TimeSpan.FromSeconds(20),
        };
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Add("Accept", "text/html,application/json,*/*");
        return http;
    }

    /// <summary>متجر مكتشَف: معرّف الفرع + الاسم + معرّف المنطقة (aid) لفتح كتالوجه.</summary>
    public readonly record struct DiscoveredStore(string BranchId, string Name, int AreaId);

    public static async Task<List<DiscoveredStore>> DiscoverCityStoresAsync(
        string citySlug, CancellationToken ct = default)
    {
        using var http = NewClient();

        var areaPaths = await GetAreaPathsAsync(http, ct);
        if (areaPaths.Count == 0) return [];

        var byName = new Dictionary<string, DiscoveredStore>(StringComparer.OrdinalIgnoreCase);
        var gate = new SemaphoreSlim(DiscoveryConcurrency);

        var tasks = areaPaths.Select(async path =>
        {
            try
            {
                await gate.WaitAsync(ct);
                try
                {
                    var (city, areaId, vendors) = await FetchAreaAsync(http, path, ct);
                    if (!string.Equals(city, citySlug, StringComparison.OrdinalIgnoreCase)) return;
                    lock (byName)
                        foreach (var (id, name) in vendors)
                            byName.TryAdd(NormalizeName(name), new DiscoveredStore(id, name, areaId));
                }
                finally { gate.Release(); }
            }
            catch { /* منطقة واحدة فشلت — تجاهل */ }
        });

        try { await Task.WhenAll(tasks); } catch { }
        return byName.Values.ToList();
    }

    private static string NormalizeName(string name)
        => Regex.Replace(name, @"\s*\(\d+\)\s*$", "").Trim().ToLowerInvariant();

    private static async Task<List<string>> GetAreaPathsAsync(HttpClient http, CancellationToken ct)
    {
        using var resp = await http.GetAsync(AreaSitemap, ct);
        if (!resp.IsSuccessStatusCode) return [];
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        string xml;
        try
        {
            using var gz = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            using var sr = new StreamReader(gz);
            xml = await sr.ReadToEndAsync(ct);
        }
        catch { xml = System.Text.Encoding.UTF8.GetString(bytes); }

        return Regex.Matches(xml, @"<loc>([^<]+)</loc>")
            .Select(m => m.Groups[1].Value)
            .Where(l => Regex.IsMatch(l, @"/groceries/\d+/"))
            .Select(l => new Uri(l).PathAndQuery)
            .ToList();
    }

    private static async Task<(string? City, int AreaId, List<(string Id, string Name)> Vendors)> FetchAreaAsync(
        HttpClient http, string path, CancellationToken ct)
    {
        using var resp = await http.GetAsync(path, ct);
        if (resp.StatusCode == (HttpStatusCode)429 || resp.StatusCode == HttpStatusCode.Forbidden)
        {
            await Task.Delay(RateLimitDelay, ct);
            return (null, 0, []);
        }
        if (!resp.IsSuccessStatusCode) return (null, 0, []);

        var html = await resp.Content.ReadAsStringAsync(ct);
        await Task.Delay(PaceDelay, ct);

        var m = Regex.Match(html, @"<script[^>]+id=""__NEXT_DATA__""[^>]*>(.*?)</script>", RegexOptions.Singleline);
        if (!m.Success) return (null, 0, []);

        using var doc = JsonDocument.Parse(m.Groups[1].Value);
        if (!doc.RootElement.TryGetProperty("props", out var props) ||
            !props.TryGetProperty("pageProps", out var pp))
            return (null, 0, []);

        string? city = null;
        int areaId = 0;
        if (pp.TryGetProperty("metadata", out var md))
        {
            if (md.TryGetProperty("city", out var c) && c.TryGetProperty("slug", out var cs))
                city = cs.GetString();
            if (md.TryGetProperty("area", out var a) && a.TryGetProperty("id", out var aid) &&
                aid.ValueKind == JsonValueKind.Number)
                areaId = aid.GetInt32();
        }

        var vendors = new List<(string, string)>();
        if (pp.TryGetProperty("vendors", out var vs) && vs.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vs.EnumerateArray())
            {
                string? id = v.TryGetProperty("id", out var idEl)
                    ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString() : idEl.GetString())
                    : null;
                var nm = v.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(nm))
                    vendors.Add((id!, nm!));
            }
        }
        return (city, areaId, vendors);
    }
}
