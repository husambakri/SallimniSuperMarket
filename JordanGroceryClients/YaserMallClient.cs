// ===================================================
// ياسر مول — OpenCart wkrestapi ✅ مؤكد
// API: https://api.yasermallonline.com/index.php
// ===================================================
using System.Text.Json;
namespace JordanGrocery;

public class YaserMallClient : IGroceryStoreClient
{
    public string StoreName => "Yaser Mall";
    private static readonly HttpClient _http = new();
    private const string BaseApi = "https://api.yasermallonline.com/index.php";

    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        // خطوة 1: ابحث بالباركود
        var url = $"{BaseApi}?route=api/wkrestapi/catalog/search&search={Uri.EscapeDataString(barcode)}&width=400";
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var products = doc.RootElement.GetPropertyOrNull("products");
        if (products is null || products.Value.GetArrayLength() == 0) return null;

        // طابق بالـ model (الباركود)
        string? productId = null;
        foreach (var p in products.Value.EnumerateArray())
        {
            if (p.GetString("model") == barcode)
            { productId = p.GetString("product_id"); break; }
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
            ProductUrl  : $"https://www.yasermallonline.com/ar/product/{productId}"
        );
    }
}
