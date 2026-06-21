// ===================================================
// هايبرماكس — MAF/Hybris Platform (نفس كارفور)
// API: https://www.hypermax.com.jo/api/v7/
// ===================================================
using System.Text.Json;
namespace JordanGrocery;

public class HypermaxClient : IGroceryStoreClient
{
    public string StoreName => "HyperMax Jordan";
    private readonly HttpClient _http;
    private const string BaseUrl = "https://www.hypermax.com.jo";

    public HypermaxClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        var url = $"{BaseUrl}/api/v7/products?query={Uri.EscapeDataString(barcode)}&lang=en&currentPage=0&pageSize=5&sortBy=relevance&displayCurr=JOD";
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var products = doc.RootElement.GetPropertyOrNull("products")?.GetPropertyOrNull("products");
        if (products is null || products.Value.GetArrayLength() == 0) return null;

        return ParseProduct(products.Value[0]);
    }

    public async Task<ProductInfo?> GetByProductIdAsync(string productId)
    {
        var url = $"{BaseUrl}/api/v7/products/{productId}?lang=en&displayCurr=JOD";
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return ParseProduct(doc.RootElement);
    }

    private ProductInfo ParseProduct(JsonElement p)
    {
        var id   = p.GetString("code") ?? "";
        var name = p.GetString("name") ?? "";
        var price  = p.GetPropertyOrNull("price")?.GetDecimal("value") ?? 0;
        var wasPrice = p.GetPropertyOrNull("wasPrice")?.GetDecimal("value") ?? 0;
        var inStock = p.GetPropertyOrNull("stock")?.GetString("stockLevelStatus") == "inStock";
        var img = p.GetPropertyOrNull("images")?.GetArrayLength() > 0
            ? (p.GetProperty("images")[0].GetString("url") ?? "") : "";
        if (img.StartsWith("//")) img = "https:" + img;

        return new ProductInfo(
            Store       : StoreName,
            ProductId   : id,
            Barcode     : p.GetString("ean") ?? id,
            Name        : name,
            Price       : price,
            Special     : wasPrice > price ? price : 0,
            InStock     : inStock,
            StockStatus : inStock ? "In Stock" : "Out Of Stock",
            ImageUrl    : img,
            ProductUrl  : $"{BaseUrl}/mafjor/en/p/{id}"
        );
    }
}
