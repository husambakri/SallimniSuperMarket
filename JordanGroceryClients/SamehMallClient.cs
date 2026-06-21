// ===================================================
// سامح مول — OpenCart (نفس منصة ياسر مول)
// Website: http://www.samehgroup.com
// ملاحظة: SSL معطوب، يعمل على HTTP فقط
// ===================================================
using System.Text.Json;
namespace JordanGrocery;

public class SamehMallClient : IGroceryStoreClient
{
    public string StoreName => "Sameh Mall";
    private readonly HttpClient _http;
    private const string BaseApi = "http://www.samehgroup.com/index.php";

    public SamehMallClient()
    {
        // تجاهل أخطاء SSL (الموقع يعمل على HTTP فقط)
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _http = new HttpClient(handler);
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
    }

    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        var url = $"{BaseApi}?route=api/wkrestapi/catalog/search&search={Uri.EscapeDataString(barcode)}&width=400";
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var products = doc.RootElement.GetPropertyOrNull("products");
        if (products is null || products.Value.GetArrayLength() == 0) return null;

        string? productId = null;
        foreach (var p in products.Value.EnumerateArray())
        {
            if (p.GetString("model") == barcode) { productId = p.GetString("product_id"); break; }
        }
        productId ??= products.Value[0].GetString("product_id");

        return productId is null ? null : await GetByProductIdAsync(productId);
    }

    public async Task<ProductInfo?> GetByProductIdAsync(string productId)
    {
        var url = $"{BaseApi}?route=api/wkrestapi/catalog/getProduct&product_id={productId}&width=800";
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var d = doc.RootElement;

        return new ProductInfo(
            Store       : StoreName,
            ProductId   : d.GetString("product_id") ?? productId,
            Barcode     : d.GetString("model")       ?? "",
            Name        : d.GetString("name")        ?? "",
            Price       : d.GetDecimal("price"),
            Special     : d.GetDecimal("special"),
            InStock     : d.GetBool("stock"),
            StockStatus : d.GetString("stock_status") ?? "",
            ImageUrl    : d.GetString("thumb")        ?? "",
            ProductUrl  : $"http://www.samehgroup.com/index.php?route=product/product&product_id={productId}"
        );
    }
}
