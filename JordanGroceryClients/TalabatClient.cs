using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JordanGrocery;

/// <summary>
/// Client for a single Talabat Jordan grocery store.
///
/// Talabat's website is a Next.js SSR app with no direct search API.
/// Products are discovered by iterating all category/subcategory pages
/// via the BFF endpoint /_next/data/manifests/grocery-items.json.
///
/// The EAN-13 barcode is embedded in each product's SKU field as:
///   "{internalId}_{EAN13barcode}"  e.g. "404428_6281007169004"
///
/// Required cookies (tlb_country, tlb_area, tlb_vertical) must be
/// set on every request for the API to return product data.
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

    private const string BaseUrl = "https://www.talabat.com";
    private const string CountrySlug = "jordan";

    /// <param name="storeName">Display name for this store.</param>
    /// <param name="branchId">Talabat branch ID (numeric string), e.g. "698392".</param>
    /// <param name="branchSlug">URL slug for the branch, e.g. "hypermax-city-center-042".</param>
    /// <param name="areaId">Talabat area ID. Default: 4809 (Al Mala'ab, Irbid).</param>
    /// <param name="areaName">Area display name matching the area ID.</param>
    /// <param name="areaSlug">Area URL slug matching the area ID.</param>
    /// <param name="areaLat">Area latitude for the tlb_area cookie.</param>
    /// <param name="areaLng">Area longitude for the tlb_area cookie.</param>
    public TalabatClient(
        string storeName,
        string branchId,
        string branchSlug,
        int areaId = 4809,
        string areaName = "Al Mala'ab",
        string areaSlug = "al-malaab",
        double areaLat = 32.5543,
        double areaLng = 35.8593)
    {
        StoreName = storeName;
        _branchId = branchId;
        _branchSlug = branchSlug;
        _areaId = areaId;
        _areaName = areaName;
        _areaSlug = areaSlug;
        _areaLat = areaLat;
        _areaLng = areaLng;

        var handler = new HttpClientHandler { CookieContainer = BuildCookies() };
        _http = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json, */*");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _http.DefaultRequestHeaders.Add("Referer",
            $"{BaseUrl}/{CountrySlug}/grocery/{branchId}/{branchSlug}?aid={areaId}");
    }

    // ─── Cookie setup ─────────────────────────────────────────────────────────

    private CookieContainer BuildCookies()
    {
        // tlb_area value is a URL-encoded JSON object describing the delivery area
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
        jar.Add(uri, new Cookie("tlb_country", CountrySlug));
        jar.Add(uri, new Cookie("tlb_area", Uri.EscapeDataString(areaJson)));
        jar.Add(uri, new Cookie("tlb_vertical", "1"));
        jar.Add(uri, new Cookie("next-i18next", "en"));
        return jar;
    }

    // ─── URL builders ─────────────────────────────────────────────────────────

    private string ItemsApiUrl(string? categorySlug = null, string? subCategorySlug = null, int page = 1)
    {
        var url = $"/_next/data/manifests/grocery-items.json" +
                  $"?aid={_areaId}" +
                  $"&countrySlug={CountrySlug}" +
                  $"&vertical=grocery" +
                  $"&branchId={_branchId}" +
                  $"&branchSlug={_branchSlug}";
        if (categorySlug != null)    url += $"&categorySlug={categorySlug}";
        if (subCategorySlug != null) url += $"&subCategorySlug={subCategorySlug}";
        if (page > 1)                url += $"&page={page}";
        return url;
    }

    private string ProductUrl(TlbItem item) =>
        $"{BaseUrl}/{CountrySlug}/grocery/{_branchId}/{_branchSlug}/{item.Slug}-{item.Id}?aid={_areaId}";

    // ─── API calls ────────────────────────────────────────────────────────────

    /// <summary>Fetches category/subcategory tree from the store's menu page.</summary>
    private async Task<List<TlbCategory>> GetCategoriesAsync()
    {
        var json = await _http.GetStringAsync(ItemsApiUrl());
        using var doc = JsonDocument.Parse(json);

        var categories = doc.RootElement
            .GetProperty("pageProps")
            .GetProperty("initialState")
            .GetProperty("categories");

        return JsonSerializer.Deserialize<List<TlbCategory>>(
            categories.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    /// <summary>Fetches one page of items for a specific subcategory.</summary>
    private async Task<(int PageCount, List<TlbItem> Items)> GetItemsPageAsync(
        string categorySlug, string subCategorySlug, int page)
    {
        var json = await _http.GetStringAsync(ItemsApiUrl(categorySlug, subCategorySlug, page));
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("pageProps", out var pp)) return (0, []);
        if (!pp.TryGetProperty("initialState", out var state))         return (0, []);
        if (!state.TryGetProperty("itemsData", out var data))          return (0, []);

        var pageCount = data.TryGetProperty("pageCount", out var pc) ? pc.GetInt32() : 0;
        var items = data.TryGetProperty("items", out var itemsEl)
            ? JsonSerializer.Deserialize<List<TlbItem>>(
                itemsEl.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []
            : [];

        return (pageCount, items);
    }

    // ─── Search logic ─────────────────────────────────────────────────────────

    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode) =>
        await ScanAsync(item => ExtractBarcode(item.Sku) == barcode);

    public async Task<ProductInfo?> GetByProductIdAsync(string productId) =>
        await ScanAsync(item => item.Id == productId);

    /// <summary>
    /// Scans all subcategories (all pages) in parallel, returning the first
    /// item that matches the predicate. Uses bounded concurrency to avoid
    /// hammering the server.
    /// </summary>
    private async Task<ProductInfo?> ScanAsync(Func<TlbItem, bool> predicate)
    {
        List<TlbCategory> categories;
        try { categories = await GetCategoriesAsync(); }
        catch { return null; }

        // Flatten to (categorySlug, subCategorySlug) pairs
        var pairs = categories
            .SelectMany(c => c.SubCategories.Select(s => (CatSlug: c.Slug, SubSlug: s.Slug)))
            .ToList();

        using var cts = new CancellationTokenSource();
        var semaphore = new SemaphoreSlim(8); // max 8 concurrent requests
        ProductInfo? result = null;

        var tasks = pairs.Select(pair => Task.Run(async () =>
        {
            try { await semaphore.WaitAsync(cts.Token); }
            catch (OperationCanceledException) { return; }

            try
            {
                if (cts.IsCancellationRequested) return;

                // Fetch page 1
                var (pageCount, firstItems) = await GetItemsPageAsync(pair.CatSlug, pair.SubSlug, 1);
                var found = firstItems.FirstOrDefault(predicate);
                if (found != null)
                {
                    Interlocked.CompareExchange(ref result, ToProductInfo(found), null);
                    cts.Cancel();
                    return;
                }

                // Fetch remaining pages sequentially (they're within the same subcategory)
                for (int pg = 2; pg <= pageCount; pg++)
                {
                    if (cts.IsCancellationRequested) return;
                    var (_, items) = await GetItemsPageAsync(pair.CatSlug, pair.SubSlug, pg);
                    found = items.FirstOrDefault(predicate);
                    if (found != null)
                    {
                        Interlocked.CompareExchange(ref result, ToProductInfo(found), null);
                        cts.Cancel();
                        return;
                    }
                }
            }
            catch { /* swallow per-subcategory errors */ }
            finally { semaphore.Release(); }
        }));

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { }

        return result;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the EAN-13 barcode from Talabat's SKU format: "{internalId}_{barcode}".
    /// </summary>
    private static string ExtractBarcode(string? sku)
    {
        if (string.IsNullOrEmpty(sku)) return string.Empty;
        var idx = sku.IndexOf('_');
        return idx >= 0 ? sku[(idx + 1)..] : sku;
    }

    private ProductInfo ToProductInfo(TlbItem item)
    {
        var inStock = item.StockAmount > 0;
        return new ProductInfo(
            Store:       StoreName,
            ProductId:   item.Id,
            Barcode:     ExtractBarcode(item.Sku),
            Name:        item.Title,
            Price:       (decimal)item.Price,
            Special:     (decimal)item.OriginalPrice,
            InStock:     inStock,
            StockStatus: inStock ? "In Stock" : "Out of Stock",
            ImageUrl:    item.Image ?? string.Empty,
            ProductUrl:  ProductUrl(item)
        );
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    private sealed record TlbCategory(
        [property: JsonPropertyName("id")]             string Id,
        [property: JsonPropertyName("name")]           string Name,
        [property: JsonPropertyName("slug")]           string Slug,
        [property: JsonPropertyName("subCategories")] List<TlbSubCategory> SubCategories);

    private sealed record TlbSubCategory(
        [property: JsonPropertyName("id")]    string Id,
        [property: JsonPropertyName("name")]  string Name,
        [property: JsonPropertyName("slug")]  string Slug,
        [property: JsonPropertyName("count")] int Count);

    private sealed record TlbItem(
        [property: JsonPropertyName("id")]            string Id,
        [property: JsonPropertyName("title")]         string Title,
        [property: JsonPropertyName("slug")]          string Slug,
        [property: JsonPropertyName("sku")]           string? Sku,
        [property: JsonPropertyName("price")]         double Price,
        [property: JsonPropertyName("originalPrice")] double OriginalPrice,
        [property: JsonPropertyName("image")]         string? Image,
        [property: JsonPropertyName("stockAmount")]   int StockAmount);
}
