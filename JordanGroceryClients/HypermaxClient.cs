// ===================================================
// هايبرماكس — MAF Platform (Next.js)
// API: https://www.hypermax.com.jo/api/v4/relevance/keyword
// ملاحظة: /api/v7/ قديم ومتوقف. الـ API الجديد يعيد
//         نتائج بالصلة — نُفلتر بمطابقة EAN الدقيقة.
// ===================================================
using System.Text.Json;
namespace JordanGrocery;

public class HypermaxClient : IGroceryStoreClient
{
    public string StoreName => "HyperMax Jordan";
    private readonly HttpClient _http;
    private const string BaseUrl = "https://www.hypermax.com.jo";
    private const string Lat = "31.9804777";
    private const string Lng = "35.8354861";

    public HypermaxClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        var url = $"{BaseUrl}/api/v4/relevance/keyword" +
                  $"?keyword={Uri.EscapeDataString(barcode)}" +
                  $"&lang=en&placements=search_page.web_rank1" +
                  $"&displayCurr=JOD&latitude={Lat}&longitude={Lng}&needOOSProducts=true";

        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync();
        if (body.TrimStart().StartsWith('<')) return null; // HTML error / Cloudflare

        using var doc = JsonDocument.Parse(body);
        var placements = doc.RootElement
            .GetPropertyOrNull("data")?
            .GetPropertyOrNull("placements");
        if (placements is null) return null;

        foreach (var placement in placements.Value.EnumerateArray())
        {
            var items = placement.GetPropertyOrNull("recommendedProducts");
            if (items is null) continue;
            foreach (var p in items.Value.EnumerateArray())
                if (p.GetString("ean") == barcode)
                    return ParseProduct(p);
        }
        return null;
    }

    public async Task<ProductInfo?> GetByProductIdAsync(string productId)
    {
        // الـ API الجديد لا يدعم البحث بالـ ID مباشرةً
        return null;
    }

    private ProductInfo ParseProduct(JsonElement p)
    {
        var id      = p.GetString("code") ?? "";
        var name    = p.GetString("name") ?? "";
        var price   = p.GetPropertyOrNull("price")?.GetDecimal("price") ?? 0;
        var inStock = p.GetPropertyOrNull("stock")?.GetString("stockLevelStatus") == "inStock";
        var img     = p.GetString("image") ?? "";
        if (img.StartsWith("//")) img = "https:" + img;

        return new ProductInfo(
            Store       : StoreName,
            ProductId   : id,
            Barcode     : p.GetString("ean") ?? id,
            Name        : name,
            Price       : price,
            Special     : 0,
            InStock     : inStock,
            StockStatus : inStock ? "In Stock" : "Out Of Stock",
            ImageUrl    : img,
            ProductUrl  : $"{BaseUrl}/mafjor/en/p/{id}"
        );
    }
}
