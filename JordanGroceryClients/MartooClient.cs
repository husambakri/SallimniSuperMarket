// ===================================================
// مارتو — WooCommerce REST API
// Website: https://martoo.com
// ملاحظة: wc/v3 يحتاج consumer_key — استخدم Store API العام
// ===================================================
using System.Text.Json;
namespace JordanGrocery;

public class MartooClient : IGroceryStoreClient
{
    public string StoreName => "Martoo";
    private readonly HttpClient _http;
    private const string BaseUrl = "https://martoo.com";

    // اختياري: أضف consumer_key/secret لو عندك
    private readonly string _consumerKey;
    private readonly string _consumerSecret;

    public MartooClient(string consumerKey = "", string consumerSecret = "")
    {
        _consumerKey    = consumerKey;
        _consumerSecret = consumerSecret;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        // WooCommerce Store API (لا يحتاج مصادقة)
        var url = $"{BaseUrl}/wp-json/wc/store/v1/products?search={Uri.EscapeDataString(barcode)}&per_page=5";
        using var resp = await _http.GetAsync(url);

        // جرب v3 لو عندك مفاتيح
        if (!resp.IsSuccessStatusCode && !string.IsNullOrEmpty(_consumerKey))
        {
            url = $"{BaseUrl}/wp-json/wc/v3/products?search={Uri.EscapeDataString(barcode)}&per_page=5&consumer_key={_consumerKey}&consumer_secret={_consumerSecret}";
            var resp2 = await _http.GetAsync(url);
            if (!resp2.IsSuccessStatusCode) return null;
            return await ParseFirstProduct(resp2);
        }

        if (!resp.IsSuccessStatusCode) return null;
        return await ParseFirstProduct(resp);
    }

    public async Task<ProductInfo?> GetByProductIdAsync(string productId)
    {
        var url = string.IsNullOrEmpty(_consumerKey)
            ? $"{BaseUrl}/wp-json/wc/store/v1/products/{productId}"
            : $"{BaseUrl}/wp-json/wc/v3/products/{productId}?consumer_key={_consumerKey}&consumer_secret={_consumerSecret}";

        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return ParseProduct(doc.RootElement, productId);
    }

    private async Task<ProductInfo?> ParseFirstProduct(HttpResponseMessage resp)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var arr = root.ValueKind == JsonValueKind.Array ? root : root.GetPropertyOrNull("products") ?? root;
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
        return ParseProduct(arr[0], null);
    }

    private ProductInfo? ParseProduct(JsonElement p, string? fallbackId)
    {
        var id   = p.GetString("id") ?? p.GetString("code") ?? fallbackId ?? "";
        var sku  = p.GetString("sku") ?? "";   // الباركود عادةً في SKU
        var name = p.GetString("name") ?? "";

        // السعر — Store API يرجعه كـ string، v3 كـ string أيضاً
        var priceStr = p.GetString("price") ?? p.GetString("regular_price") ?? "0";
        decimal.TryParse(priceStr, out var price);
        var saleStr = p.GetString("sale_price") ?? "0";
        decimal.TryParse(saleStr, out var sale);

        var inStock = p.GetString("stock_status") == "instock"
                   || p.GetBool("is_in_stock")
                   || p.GetBool("purchasable");

        // صورة
        var img = p.GetPropertyOrNull("images")?.GetArrayLength() > 0
            ? (p.GetProperty("images")[0].GetString("src") ?? p.GetProperty("images")[0].GetString("url") ?? "") : "";

        if (string.IsNullOrWhiteSpace(name)) return null;

        return new ProductInfo(
            Store       : StoreName,
            ProductId   : id,
            Barcode     : sku,
            Name        : name,
            Price       : price,
            Special     : sale > 0 && sale < price ? sale : 0,
            InStock     : inStock,
            StockStatus : inStock ? "In Stock" : "Out Of Stock",
            ImageUrl    : img,
            ProductUrl  : $"{BaseUrl}/product/{p.GetString("slug") ?? id}"
        );
    }
}
