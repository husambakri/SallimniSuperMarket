// ===================================================
// Centro Market (Centro Mall Amman)
// API: https://centromall.net/api/public/api/products
//
// GetByBarcodeAsync  → filters[barcode][type]=equals&filters[barcode][filter]={barcode}
//                      (يعمل إذا خزّن المتجر الباركود في قاعدة البيانات)
// GetByProductIdAsync → filters[id][type]=equals&filters[id][filter]={id}
// GetByNameAsync     → search={name}
//
// Image URL: https://centromall.net/api/public/media/{file_name}
// Shop ID  : 390 (Centro Market)
// Currency : JOD
// ===================================================
using System.Text.Json;
namespace JordanGrocery;

public class CentroMarketClient : IGroceryStoreClient
{
    public string StoreName => "Centro Market";

    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        UseCookies = false,
    })
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36" },
            { "Accept",     "application/json, */*" },
            { "Referer",    "https://centromall.net/grocery" },
        }
    };

    private const string BaseApi   = "https://centromall.net/api/public/api";
    private const string MediaBase = "https://centromall.net/api/public/media";
    private const string StoreUrl  = "https://centromall.net/grocery/shop/390";
    private const int    ShopId    = 390;

    // ── البحث بالباركود ─────────────────────────────────────────────────
    public Task<ProductInfo?> GetByBarcodeAsync(string barcode)
        => FetchFirst($"{BaseApi}/products?shop_id={ShopId}" +
                      $"&filters[barcode][type]=equals" +
                      $"&filters[barcode][filter]={Uri.EscapeDataString(barcode)}");

    // ── البحث بالـ ID الداخلي ────────────────────────────────────────────
    public Task<ProductInfo?> GetByProductIdAsync(string productId)
        => FetchFirst($"{BaseApi}/products?shop_id={ShopId}" +
                      $"&filters[id][type]=equals" +
                      $"&filters[id][filter]={Uri.EscapeDataString(productId)}");

    // ── البحث بالاسم (contains) ─────────────────────────────────────────
    public Task<ProductInfo?> GetByNameAsync(string name)
        => FetchFirst($"{BaseApi}/products?shop_id={ShopId}" +
                      $"&search={Uri.EscapeDataString(name)}&limit=10",
                      name);

    // ── تنفيذ الطلب ──────────────────────────────────────────────────────
    private async Task<ProductInfo?> FetchFirst(string url, string? nameHint = null)
    {
        try
        {
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            var data = doc.RootElement.GetPropertyOrNull("data");
            if (data is null || data.Value.GetArrayLength() == 0) return null;

            // إذا كان بحث بالاسم، خذ أدق تطابق
            if (nameHint is not null)
            {
                JsonElement? best = null;
                foreach (var item in data.Value.EnumerateArray())
                {
                    var n = item.GetString("name") ?? "";
                    if (n.Contains(nameHint, StringComparison.OrdinalIgnoreCase))
                    { best = item; break; }
                }
                if (best is null) return null;
                return ToProductInfo(best.Value);
            }

            return ToProductInfo(data.Value[0]);
        }
        catch
        {
            return null;
        }
    }

    // ── تحويل JSON → ProductInfo ─────────────────────────────────────────
    private ProductInfo ToProductInfo(JsonElement p)
    {
        var id       = p.TryGetProperty("id", out var idEl) ? idEl.GetInt32().ToString() : "";
        var name     = p.GetString("name")    ?? "";
        var barcode  = p.GetString("barcode") ?? "";
        var stock    = p.TryGetProperty("stock", out var stockEl) && stockEl.ValueKind == JsonValueKind.Number
                         ? stockEl.GetInt32() : 0;
        var inStock  = stock > 0;

        var price    = p.GetDecimal("price");
        var discountPrice = p.GetDecimal("discount_price");
        var special  = discountPrice > 0 && discountPrice < price ? discountPrice : 0m;
        var finalPrice = special > 0 ? price : price; // regular price stays as price

        // صورة المنتج
        var imageFile = p.GetPropertyOrNull("files")
                         ?.GetPropertyOrNull("featured_image")
                         is JsonElement imgs && imgs.GetArrayLength() > 0
                            ? imgs[0].GetString("file_name") ?? ""
                            : "";
        var imageUrl = imageFile.Length > 0 ? $"{MediaBase}/{imageFile}" : "";

        return new ProductInfo(
            Store:       StoreName,
            ProductId:   id,
            Barcode:     barcode,
            Name:        name,
            Price:       finalPrice,
            Special:     special,
            InStock:     inStock,
            StockStatus: inStock ? "In Stock" : "Out Of Stock",
            ImageUrl:    imageUrl,
            ProductUrl:  StoreUrl
        );
    }
}
