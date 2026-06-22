// ===================================================
// ياسر مول — OpenCart wkrestapi
// API: https://api.yasermallonline.com/index.php
//
// GetByBarcodeAsync:
//   1. POST catalog/searchSuggest  → product_id (سريع، بدون تفاصيل)
//      أو GET catalog/productSearch → قائمة المنتجات المطابقة
//   2. GET catalog/getProduct      → model (الباركود) + كامل البيانات
//
// GetByProductIdAsync:
//   GET catalog/getProduct مباشرة
// ===================================================
using System.Net.Http.Headers;
using System.Text.Json;
namespace JordanGrocery;

public class YaserMallClient : ICatalogStoreClient
{
    public string StoreName => "Yaser Mall";

    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        UseCookies = false,
    })
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36" },
            { "Accept",     "application/json, text/plain, */*" },
            { "Referer",    "https://www.yasermallonline.com/" },
            { "Origin",     "https://www.yasermallonline.com" },
        }
    };

    private const string BaseApi  = "https://api.yasermallonline.com/index.php";
    private const string BaseSite = "https://www.yasermallonline.com";

    // ── البحث بالباركود ────────────────────────────────────────────────
    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        // الخطوة 1: ابحث عن المنتج بالباركود (model)
        var searchUrl = $"{BaseApi}?route=api/wkrestapi/catalog/productSearch" +
                        $"&search={Uri.EscapeDataString(barcode)}&page=1&limit=10&width=400";

        using var searchResp = await _http.GetAsync(searchUrl);
        if (!searchResp.IsSuccessStatusCode) return null;

        var searchBody = await searchResp.Content.ReadAsStringAsync();
        if (searchBody.TrimStart().StartsWith('<')) return null;

        using var searchDoc = JsonDocument.Parse(searchBody);
        var products = searchDoc.RootElement.GetPropertyOrNull("products");
        if (products is null) return null;

        // الخطوة 2: ابحث عن المنتج الذي model == barcode
        foreach (var item in products.Value.EnumerateArray())
        {
            var pid = item.GetString("product_id");
            if (string.IsNullOrEmpty(pid)) continue;

            // اجلب التفاصيل الكاملة للتحقق من الباركود
            var info = await GetByProductIdAsync(pid);
            if (info is not null && info.Barcode == barcode)
                return info;
        }

        return null;
    }

    // ── سحب كامل الكتالوج ──────────────────────────────────────────────
    // productSearch ببحث فارغ يسرد كل المنتجات (مرقّمة) لكن دون الباركود؛
    // نجلب الباركود لكل منتج عبر getProduct (بتزامن محدود).
    public async Task<List<ProductInfo>> GetAllProductsAsync(CancellationToken ct = default)
    {
        var result = new List<ProductInfo>();
        var seen = new HashSet<string>();

        for (int page = 1; !ct.IsCancellationRequested; page++)
        {
            var listUrl = $"{BaseApi}?route=api/wkrestapi/catalog/productSearch" +
                          $"&search=&page={page}&limit=100&width=200";

            List<string> ids;
            try
            {
                using var resp = await _http.GetAsync(listUrl, ct);
                if (!resp.IsSuccessStatusCode) break;
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (body.TrimStart().StartsWith('<')) break;

                using var doc = JsonDocument.Parse(body);
                var products = doc.RootElement.GetPropertyOrNull("products");
                if (products is null) break;

                ids = products.Value.EnumerateArray()
                    .Select(p => p.GetString("product_id"))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => id!)
                    .ToList();
            }
            catch { break; }

            if (ids.Count == 0) break; // انتهت الصفحات

            // اجلب تفاصيل كل منتج (الباركود) بتزامن محدود.
            var sem = new SemaphoreSlim(8);
            var tasks = ids.Select(async id =>
            {
                await sem.WaitAsync(ct);
                try { return await GetByProductIdAsync(id); }
                catch { return null; }
                finally { sem.Release(); }
            });
            foreach (var info in await Task.WhenAll(tasks))
                if (info is not null && info.Barcode.Length >= 8 && seen.Add(info.Barcode))
                    result.Add(info);
        }

        return result;
    }

    // ── جلب منتج بالـ ID ───────────────────────────────────────────────
    public async Task<ProductInfo?> GetByProductIdAsync(string productId)
    {
        var url = $"{BaseApi}?route=api/wkrestapi/catalog/getProduct&product_id={productId}&width=800";

        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync();
        if (body.TrimStart().StartsWith('<')) return null;

        using var doc = JsonDocument.Parse(body);
        var d = doc.RootElement;

        // getProduct يُعيد خطأ إذا لم يُوجد المنتج
        if (d.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Number && err.GetInt32() != 0)
            return null;

        var inStock = d.GetBool("stock");

        return new ProductInfo(
            Store       : StoreName,
            ProductId   : d.GetString("product_id") ?? productId,
            Barcode     : d.GetString("model")       ?? "",
            Name        : d.GetString("name")        ?? "",
            Price       : d.GetDecimal("price"),
            Special     : d.GetDecimal("special"),
            InStock     : inStock,
            StockStatus : d.GetString("stock_status") ?? (inStock ? "In Stock" : "Out Of Stock"),
            ImageUrl    : d.GetString("thumb")        ?? "",
            ProductUrl  : $"{BaseSite}/ar/product/{productId}"
        );
    }
}
