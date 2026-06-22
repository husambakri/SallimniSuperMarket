using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace JordanGrocery;

/// <summary>
/// Client for a single Talabat Jordan grocery store.
///
/// Talabat is a Next.js SSR app. The store page embeds the category tree in
/// <c>__NEXT_DATA__</c> under <c>props.pageProps.initialState.categories</c>
/// (each category has <c>subCategories</c> with slugs). Products are NOT in the
/// initial HTML — they load per sub-category from the BFF manifest:
///   /_next/data/manifests/grocery-items.json?...&amp;categorySlug=..&amp;subCategorySlug=..
/// which returns <c>pageProps.initialState.itemsData.items</c>.
///
/// NOTE: Talabat does NOT expose EAN barcodes, so <see cref="GetByBarcodeAsync"/>
/// returns null. The aggregator looks Talabat up by name (the product name found
/// in the barcode-capable stores) via <see cref="GetByNameAsync"/>.
///
/// A process-wide concurrency gate caps total simultaneous Talabat requests
/// across ALL branch instances, so fanning out over many branches does not trip
/// Cloudflare. Each lookup is also bounded by an internal timeout so a slow or
/// blocked branch never stalls the overall scan.
/// </summary>
public class TalabatClient : IGroceryStoreClient
{
    public string StoreName { get; }

    private readonly string _branchId;
    private readonly string _branchSlug;
    private readonly int    _areaId;
    private readonly string _areaName;
    private readonly string _areaSlug;
    private readonly double _areaLat;
    private readonly double _areaLng;
    private readonly HttpClient _http;

    private const string BaseUrl     = "https://www.talabat.com";
    private const string CountrySlug = "jordan";

    // عدد الطلبات المتزامنة الكلّي عبر كل فروع طلبات (يمنع حظر Cloudflare عند تعدّد الفروع).
    private static readonly SemaphoreSlim _globalGate = new(8);
    // مهلة بحث الفرع الواحد — لا يحجب بطء/حظر فرعٍ بقيّةَ المتاجر.
    private static readonly TimeSpan PerLookupBudget = TimeSpan.FromSeconds(6);
    // تزامن داخلي لكل فرع عند مسح الفئات.
    private const int PerBranchConcurrency = 3;

    public TalabatClient(
        string storeName,
        string branchId,
        string branchSlug,
        int    areaId   = 4809,
        string areaName = "Al Mala'ab",
        string areaSlug = "al-malaab",
        double areaLat  = 32.5543,
        double areaLng  = 35.8593)
    {
        StoreName    = storeName;
        _branchId    = branchId;
        _branchSlug  = branchSlug;
        _areaId      = areaId;
        _areaName    = areaName;
        _areaSlug    = areaSlug;
        _areaLat     = areaLat;
        _areaLng     = areaLng;

        var handler = new HttpClientHandler { CookieContainer = BuildCookies(), AllowAutoRedirect = true };
        _http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "text/html,application/json,*/*");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    // ─── Cookie setup ───────────────────────────────────────────────────────────

    private CookieContainer BuildCookies()
    {
        var areaJson = $"{{\"id\":{_areaId}," +
                       $"\"name\":\"{_areaName}\"," +
                       $"\"slug\":\"{_areaSlug}\"," +
                       $"\"fromMap\":false," +
                       $"\"ltd\":{_areaLat}," +
                       $"\"lngt\":{_areaLng}," +
                       $"\"displayName\":\"{_areaName}\"," +
                       $"\"ipse\":false}}";

        var jar = new CookieContainer();
        var uri = new Uri(BaseUrl);
        jar.Add(uri, new Cookie("tlb_country",  CountrySlug));
        jar.Add(uri, new Cookie("tlb_area",     Uri.EscapeDataString(areaJson)));
        jar.Add(uri, new Cookie("tlb_vertical", "1"));
        jar.Add(uri, new Cookie("next-i18next", "en"));
        return jar;
    }

    // ─── URL builders ─────────────────────────────────────────────────────────

    private string StorePageUrl =>
        $"/{CountrySlug}/grocery/{_branchId}/{_branchSlug}?aid={_areaId}";

    private string ItemsManifestUrl(string categorySlug, string subCategorySlug) =>
        $"/_next/data/manifests/grocery-items.json" +
        $"?aid={_areaId}&countrySlug={CountrySlug}&vertical=grocery" +
        $"&branchId={_branchId}&branchSlug={_branchSlug}" +
        $"&categorySlug={categorySlug}&subCategorySlug={subCategorySlug}";

    // ─── Fetch + parse ──────────────────────────────────────────────────────────

    /// <summary>Fetches the (category, subCategory) slug pairs from the store page.</summary>
    private async Task<List<(string Cat, string Sub)>> GetCategoryPairsAsync(CancellationToken ct)
    {
        await _globalGate.WaitAsync(ct);
        string html;
        try { html = await _http.GetStringAsync(StorePageUrl, ct); }
        finally { _globalGate.Release(); }

        var match = Regex.Match(html,
            @"<script[^>]+id=""__NEXT_DATA__""[^>]*>\s*(\{.*?\})\s*</script>",
            RegexOptions.Singleline);
        if (!match.Success) return [];

        using var doc = JsonDocument.Parse(match.Groups[1].Value);
        if (!doc.RootElement.TryGetProperty("props", out var props) ||
            !props.TryGetProperty("pageProps", out var pp) ||
            !pp.TryGetProperty("initialState", out var state) ||
            !state.TryGetProperty("categories", out var cats) ||
            cats.ValueKind != JsonValueKind.Array)
            return [];

        var pairs = new List<(string, string)>();
        foreach (var c in cats.EnumerateArray())
        {
            var catSlug = c.GetString("slug");
            if (catSlug is null) continue;
            if (c.TryGetProperty("subCategories", out var subs) && subs.ValueKind == JsonValueKind.Array)
                foreach (var s in subs.EnumerateArray())
                    if (s.GetString("slug") is { } subSlug)
                        pairs.Add((catSlug, subSlug));
        }
        return pairs;
    }

    /// <summary>Fetches one sub-category's items (page 1) from the BFF manifest.</summary>
    private async Task<List<TlbItem>> GetItemsAsync(string catSlug, string subSlug, CancellationToken ct)
    {
        await _globalGate.WaitAsync(ct);
        string body;
        try { body = await _http.GetStringAsync(ItemsManifestUrl(catSlug, subSlug), ct); }
        finally { _globalGate.Release(); }

        if (body.TrimStart().StartsWith('<')) return []; // Cloudflare/HTML

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("pageProps", out var pp) ||
            !pp.TryGetProperty("initialState", out var state) ||
            !state.TryGetProperty("itemsData", out var data) ||
            !data.TryGetProperty("items", out var itemsEl))
            return [];

        return JsonSerializer.Deserialize<List<TlbItem>>(
            itemsEl.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    /// <summary>
    /// Scans sub-categories with bounded concurrency, returning the first item
    /// matching <paramref name="predicate"/>. Bounded by <see cref="PerLookupBudget"/>.
    /// </summary>
    private async Task<ProductInfo?> ScanAsync(Func<TlbItem, bool> predicate)
    {
        using var cts = new CancellationTokenSource(PerLookupBudget);
        var ct = cts.Token;

        List<(string Cat, string Sub)> pairs;
        try { pairs = await GetCategoryPairsAsync(ct); }
        catch { return null; }
        if (pairs.Count == 0) return null;

        var sem = new SemaphoreSlim(PerBranchConcurrency);
        ProductInfo? result = null;

        var tasks = pairs.Select(async pair =>
        {
            if (ct.IsCancellationRequested) return;
            try { await sem.WaitAsync(ct); } catch { return; }
            try
            {
                if (ct.IsCancellationRequested) return;
                var items = await GetItemsAsync(pair.Cat, pair.Sub, ct);
                var hit = items.FirstOrDefault(predicate);
                if (hit is not null)
                {
                    Interlocked.CompareExchange(ref result, ToProductInfo(hit), null);
                    cts.Cancel(); // وقف بقيّة المسح عند أول تطابق
                }
            }
            catch { /* خطأ شبكة/إلغاء على فئة — تخطَّ */ }
            finally { sem.Release(); }
        });

        try { await Task.WhenAll(tasks); } catch { }
        return result;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// يبحث بالباركود عبر حقل sku في طلبات — صيغته "{رقم داخلي}_{باركود EAN}"
    /// (مثل "727803_6253357600056")، فالجزء بعد الشرطة السفلية هو الباركود.
    /// دقيق ولا يعتمد على لغة الاسم.
    /// </summary>
    public Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return Task.FromResult<ProductInfo?>(null);
        return ScanAsync(i => BarcodeOf(i) == barcode);
    }

    public Task<ProductInfo?> GetByProductIdAsync(string productId) =>
        ScanAsync(i => i.Id == productId);

    /// <summary>يبحث بالاسم (تطابق جزئي غير حسّاس لحالة الأحرف على عنوان المنتج).</summary>
    public Task<ProductInfo?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Task.FromResult<ProductInfo?>(null);
        return ScanAsync(i => i.Title?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private ProductInfo ToProductInfo(TlbItem item)
    {
        var price        = (decimal)item.Price;
        var regularPrice = item.OriginalPrice > 0 ? (decimal)item.OriginalPrice : price;
        // price هو السعر بعد الخصم؛ نعتبره Special إن كان أقل من السعر الأصلي.
        var special      = regularPrice > price ? price : 0m;
        var inStock      = item.StockAmount > 0;

        return new ProductInfo(
            Store:       StoreName,
            ProductId:   item.Id ?? string.Empty,
            Barcode:     BarcodeOf(item),
            Name:        item.Title ?? string.Empty,
            Price:       regularPrice,
            Special:     special,
            InStock:     inStock,
            StockStatus: inStock ? "In Stock" : "Out Of Stock",
            ImageUrl:    item.Image ?? string.Empty,
            ProductUrl:  $"{BaseUrl}/{CountrySlug}/grocery/{_branchId}/{_branchSlug}?aid={_areaId}"
        );
    }

    /// <summary>
    /// باركود المنتج: أولاً من sku ("{id}_{barcode}" → الجزء بعد '_')، وإلا من اسم
    /// ملف الصورة (مثل .../6253357600056.jpg).
    /// </summary>
    private static string BarcodeOf(TlbItem item)
    {
        if (!string.IsNullOrEmpty(item.Sku))
        {
            var idx = item.Sku.LastIndexOf('_');
            var bc  = idx >= 0 ? item.Sku[(idx + 1)..] : item.Sku;
            if (Regex.IsMatch(bc, @"^\d{8,14}$")) return bc;
        }
        if (!string.IsNullOrEmpty(item.Image))
        {
            var m = Regex.Match(item.Image, @"/(\d{8,14})\.(?:jpg|jpeg|png|webp)", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
        }
        return string.Empty;
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    private sealed record TlbItem(
        [property: JsonPropertyName("id")]            string? Id,
        [property: JsonPropertyName("title")]         string? Title,
        [property: JsonPropertyName("sku")]           string? Sku,
        [property: JsonPropertyName("price")]         double  Price,
        [property: JsonPropertyName("originalPrice")] double  OriginalPrice,
        [property: JsonPropertyName("image")]         string? Image,
        [property: JsonPropertyName("stockAmount")]   int     StockAmount);
}
