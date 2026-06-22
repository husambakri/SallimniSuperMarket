using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace JordanGrocery;

/// <summary>
/// Client for a single Talabat Jordan grocery store.
///
/// Talabat is a Next.js SSR app. All store products are embedded in the
/// initial page HTML inside a <script id="__NEXT_DATA__"> tag, under:
///   pageProps.initialMenuState.menuData.items
///
/// Each item: { id, name, description, price, oldPrice, image, ... }
/// NOTE: Talabat does NOT expose EAN barcodes in their public web API.
///       GetByBarcodeAsync searches by name (case-insensitive contains).
///       GetByProductIdAsync searches by Talabat's internal numeric item ID.
///
/// Required cookies: tlb_country, tlb_area (JSON), tlb_vertical.
/// </summary>
public class TalabatClient : IGroceryStoreClient
{
    public string StoreName { get; }

    private readonly string _branchId;
    private readonly string _branchSlug;
    private readonly int _areaId;
    private readonly string _areaName;
    private readonly string _areaSlug;
    private readonly double _areaLat;
    private readonly double _areaLng;
    private readonly HttpClient _http;

    private const string BaseUrl     = "https://www.talabat.com";
    private const string CountrySlug = "jordan";

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
        _http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    // ─── Cookie setup ─────────────────────────────────────────────────────────

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
        jar.Add(uri, new Cookie("tlb_country",   CountrySlug));
        jar.Add(uri, new Cookie("tlb_area",      Uri.EscapeDataString(areaJson)));
        jar.Add(uri, new Cookie("tlb_vertical",  "1"));
        jar.Add(uri, new Cookie("next-i18next",  "en"));
        return jar;
    }

    // ─── Fetch + parse ────────────────────────────────────────────────────────

    private string StorePageUrl =>
        $"/{CountrySlug}/grocery/{_branchId}/{_branchSlug}?aid={_areaId}";

    /// <summary>
    /// Fetches the store HTML page and extracts all items from __NEXT_DATA__.
    /// Returns an empty list if the page is blocked or data is unavailable.
    /// </summary>
    private async Task<List<TlbItem>> GetItemsAsync()
    {
        try
        {
            using var resp = await _http.GetAsync(StorePageUrl);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"    [Talabat:{_branchId}] HTTP {(int)resp.StatusCode}");
                return [];
            }

            var html = await resp.Content.ReadAsStringAsync();

            // Extract <script id="__NEXT_DATA__" ...>{...}</script>
            var match = Regex.Match(html,
                @"<script[^>]+id=""__NEXT_DATA__""[^>]*>\s*(\{.*?\})\s*</script>",
                RegexOptions.Singleline);
            if (!match.Success)
            {
                Console.WriteLine($"    [Talabat:{_branchId}] __NEXT_DATA__ غير موجود (HTML: {html.Length} حرف)");
                return [];
            }

            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            var root = doc.RootElement;

            // Navigate: props → pageProps → initialMenuState → menuData → items
            if (!root.TryGetProperty("props", out var props)) { Console.WriteLine($"    [Talabat:{_branchId}] ❌ props"); return []; }
            if (!props.TryGetProperty("pageProps", out var pp)) { Console.WriteLine($"    [Talabat:{_branchId}] ❌ pageProps"); return []; }
            if (!pp.TryGetProperty("initialMenuState", out var ims)) { Console.WriteLine($"    [Talabat:{_branchId}] ❌ initialMenuState"); return []; }
            if (!ims.TryGetProperty("menuData", out var menuData)) { Console.WriteLine($"    [Talabat:{_branchId}] ❌ menuData"); return []; }
            if (!menuData.TryGetProperty("items", out var itemsEl)) { Console.WriteLine($"    [Talabat:{_branchId}] ❌ items"); return []; }

            var items = JsonSerializer.Deserialize<List<TlbItem>>(
                itemsEl.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Console.WriteLine($"    [Talabat:{_branchId}] ✅ {items?.Count ?? 0} منتج");
            return items ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [Talabat:{_branchId}] ❌ استثناء: {ex.Message}");
            return [];
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Talabat does not expose barcodes in its web API.
    /// This always returns null — use standalone store clients for barcode lookup.
    /// </summary>
    public Task<ProductInfo?> GetByBarcodeAsync(string barcode) =>
        Task.FromResult<ProductInfo?>(null);

    /// <summary>
    /// Looks up a product by Talabat's internal numeric item ID.
    /// </summary>
    public async Task<ProductInfo?> GetByProductIdAsync(string productId)
    {
        var items = await GetItemsAsync();
        var item  = items.FirstOrDefault(i => i.Id.ToString() == productId);
        return item is null ? null : ToProductInfo(item);
    }

    /// <summary>
    /// Searches all items by name (case-insensitive contains match).
    /// Useful when you already know the product name.
    /// </summary>
    public async Task<ProductInfo?> GetByNameAsync(string name)
    {
        var items = await GetItemsAsync();
        var item  = items.FirstOrDefault(i =>
            i.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
        return item is null ? null : ToProductInfo(item);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private ProductInfo ToProductInfo(TlbItem item)
    {
        var inStock = true; // Talabat only shows in-stock items in the catalog
        var price   = (decimal)item.Price;
        var special = item.OldPrice > 0 && item.OldPrice > item.Price
            ? price           // price is already the discounted price
            : 0m;
        var regularPrice = item.OldPrice > 0 ? (decimal)item.OldPrice : price;

        return new ProductInfo(
            Store:       StoreName,
            ProductId:   item.Id.ToString(),
            Barcode:     string.Empty,          // not available from Talabat's web API
            Name:        item.Name ?? string.Empty,
            Price:       regularPrice,
            Special:     special,
            InStock:     inStock,
            StockStatus: "In Stock",
            ImageUrl:    item.Image ?? string.Empty,
            ProductUrl:  $"{BaseUrl}/{CountrySlug}/grocery/{_branchId}/{_branchSlug}?aid={_areaId}"
        );
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    private sealed record TlbItem(
        [property: JsonPropertyName("id")]          long    Id,
        [property: JsonPropertyName("name")]        string? Name,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("price")]       double  Price,
        [property: JsonPropertyName("oldPrice")]    double  OldPrice,
        [property: JsonPropertyName("image")]       string? Image);
}
