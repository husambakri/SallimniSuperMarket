// ===================================================
// كوزمو — HTML Scraping (لا يوجد REST API)
// Website: https://cozmo.jo
// ===================================================
using System.Text.RegularExpressions;
namespace JordanGrocery;

public class CozmoClient : IGroceryStoreClient
{
    public string StoreName => "Cozmo";
    private readonly HttpClient _http;
    private const string BaseUrl = "https://cozmo.jo";

    public CozmoClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    // البحث بالباركود عبر صفحة البحث
    public async Task<ProductInfo?> GetByBarcodeAsync(string barcode)
    {
        var html = await _http.GetStringAsync($"{BaseUrl}/search?type=product&q={Uri.EscapeDataString(barcode)}");
        var productId = ExtractFirstProductId(html);
        return productId is null ? null : await GetByProductIdAsync(productId);
    }

    // جلب المنتج من صفحته
    public async Task<ProductInfo?> GetByProductIdAsync(string productId)
    {
        var url = $"{BaseUrl}/lifeStyle/{productId}";
        var html = await _http.GetStringAsync(url);
        return ParseProductPage(html, productId, url);
    }

    private static string? ExtractFirstProductId(string html)
    {
        // استخراج أول product ID من نتائج البحث
        var m = Regex.Match(html, @"/lifeStyle/(\d+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static ProductInfo? ParseProductPage(string html, string productId, string url)
    {
        // استخراج الاسم
        var name = Regex.Match(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value.Trim();
        name = Regex.Replace(name, "<.*?>", "").Trim();

        // استخراج السعر
        var priceMatch = Regex.Match(html, @"JD\s*([\d.]+)", RegexOptions.IgnoreCase);
        decimal.TryParse(priceMatch.Groups[1].Value, out var price);

        // استخراج الصورة
        var imgMatch = Regex.Match(html, @"<img[^>]+(?:src|data-src)=""([^""]*upload[^""]*)""", RegexOptions.IgnoreCase);
        var img = imgMatch.Success ? imgMatch.Groups[1].Value : "";
        if (img.StartsWith("/")) img = BaseUrl + img;

        // استخراج الباركود إن وجد
        var barcodeMatch = Regex.Match(html, @"barcode["":\s]+(\d{8,14})", RegexOptions.IgnoreCase);
        var barcode = barcodeMatch.Groups[1].Value;

        if (string.IsNullOrWhiteSpace(name)) return null;

        return new ProductInfo(
            Store       : "Cozmo",
            ProductId   : productId,
            Barcode     : barcode,
            Name        : name,
            Price       : price,
            Special     : 0,
            InStock     : !html.Contains("Out of Stock", StringComparison.OrdinalIgnoreCase),
            StockStatus : html.Contains("Out of Stock", StringComparison.OrdinalIgnoreCase) ? "Out Of Stock" : "In Stock",
            ImageUrl    : img,
            ProductUrl  : url
        );
    }
}
