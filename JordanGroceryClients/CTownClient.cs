// ===================================================
// C-Town Jordan — Magento 2 GraphQL
// API: https://ctown.jo/graphql
// البحث بالباركود (SKU) مباشرة عبر GraphQL guest query
// ===================================================
using System.Text;
using System.Text.Json;
namespace JordanGrocery;

public class CTownClient : ICatalogStoreClient
{
    public string StoreName => "C-Town";

    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36" },
        }
    };

    private const string GraphQlUrl = "https://ctown.jo/graphql";
    private const string BaseUrl    = "https://ctown.jo";

    // ── بناء GraphQL query ──────────────────────────────────────────────
    // نستخدم PLACEHOLDER لتجنب تعارض الأقواس مع raw string interpolation
    private const string QueryTemplate = """
        { products(filter: {FIELD: {eq: "VALUE"}}) { items {
            id sku name url_key stock_status
            special_price special_from_date special_to_date
            price_range { minimum_price {
                regular_price { value }
                final_price   { value }
                discount      { amount_off }
            }}
            small_image { url }
        }}}
        """;

    private static string BuildSkuQuery(string sku) =>
        QueryTemplate.Replace("FIELD", "sku").Replace("VALUE", sku);

    private static string BuildIdQuery(string id) =>
        QueryTemplate.Replace("FIELD", "id").Replace("VALUE", id);

    // ── البحث بالباركود ─────────────────────────────────────────────────
    public Task<ProductInfo?> GetByBarcodeAsync(string barcode)
        => QueryGraphQL(BuildSkuQuery(barcode));

    // ── البحث بالـ ID ───────────────────────────────────────────────────
    public Task<ProductInfo?> GetByProductIdAsync(string productId)
        => QueryGraphQL(BuildIdQuery(productId));

    // ── سحب كامل الكتالوج ──────────────────────────────────────────────
    // قائمة Magento ترجع SKU (الباركود) مباشرةً، فلا حاجة لنداء لكل منتج.
    // فلتر price.from=0 يطابق كل المنتجات؛ نُرقّم حتى تفرغ الصفحات.
    public async Task<List<ProductInfo>> GetAllProductsAsync(CancellationToken ct = default)
    {
        var result = new List<ProductInfo>();
        var seen = new HashSet<string>();

        for (int page = 1; page <= 1000 && !ct.IsCancellationRequested; page++)
        {
            var query =
                "{ products(filter: {price: {from: \"0\"}}, pageSize: 100, currentPage: " + page + ") { items { " +
                "id sku name url_key stock_status " +
                "special_price special_from_date special_to_date " +
                "price_range { minimum_price { regular_price { value } final_price { value } discount { amount_off } } } " +
                "small_image { url } } } }";

            JsonElement.ArrayEnumerator items;
            JsonDocument? doc = null;
            try
            {
                var payload = JsonSerializer.Serialize(new { query });
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(GraphQlUrl, content, ct);
                if (!resp.IsSuccessStatusCode) break;
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (body.TrimStart().StartsWith('<')) break;

                doc = JsonDocument.Parse(body);
                var itemsEl = doc.RootElement.GetPropertyOrNull("data")?
                    .GetPropertyOrNull("products")?.GetPropertyOrNull("items");
                if (itemsEl is null || itemsEl.Value.GetArrayLength() == 0) { doc.Dispose(); break; }
                items = itemsEl.Value.EnumerateArray();
            }
            catch { doc?.Dispose(); break; }

            foreach (var it in items)
            {
                var info = ParseProduct(it);
                if (info.Barcode.Length >= 8 && seen.Add(info.Barcode))
                    result.Add(info);
            }
            doc.Dispose();
        }

        return result;
    }

    // ── تنفيذ GraphQL query ─────────────────────────────────────────────
    private async Task<ProductInfo?> QueryGraphQL(string query)
    {
        var payload = JsonSerializer.Serialize(new { query });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(GraphQlUrl, content);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync();
        if (body.TrimStart().StartsWith('<')) return null;

        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement
            .GetPropertyOrNull("data")?
            .GetPropertyOrNull("products")?
            .GetPropertyOrNull("items");
        if (items is null || items.Value.GetArrayLength() == 0) return null;

        return ParseProduct(items.Value[0]);
    }

    // ── تحويل JSON → ProductInfo ────────────────────────────────────────
    private ProductInfo ParseProduct(JsonElement p)
    {
        var id      = p.TryGetProperty("id", out var idProp) ? idProp.GetInt32().ToString() : "";
        var sku     = p.GetString("sku")      ?? "";
        var name    = p.GetString("name")     ?? "";
        var urlKey  = p.GetString("url_key")  ?? "";
        var inStock = p.GetString("stock_status") == "IN_STOCK";

        var minPrice    = p.GetPropertyOrNull("price_range")?.GetPropertyOrNull("minimum_price");
        var regularPrice = minPrice?.GetPropertyOrNull("regular_price")?.GetDecimal("value") ?? 0;
        var finalPrice   = minPrice?.GetPropertyOrNull("final_price")  ?.GetDecimal("value") ?? 0;

        // العرض يُعتمد فقط إن كان ساري المفعول الآن: واجهة C-Town تُبقي final_price منخفضًا
        // حتى بعد انتهاء العرض، لذا نتحقّق من تاريخي البداية/النهاية بدل الاعتماد على discount.
        var specialPrice = p.GetDecimal("special_price");
        var from = ParseDate(p.GetString("special_from_date"));
        var to   = ParseDate(p.GetString("special_to_date"));
        var now  = DateTime.UtcNow.Date;
        var active = specialPrice > 0 && specialPrice < regularPrice
                     && (from is null || now >= from.Value.Date)
                     && (to   is null || now <= to.Value.Date);
        var special = active ? specialPrice : 0;
        var price   = regularPrice > 0 ? regularPrice : finalPrice;

        var imageUrl = p.GetPropertyOrNull("small_image")?.GetString("url") ?? "";

        return new ProductInfo(
            Store       : StoreName,
            ProductId   : id,
            Barcode     : sku,
            Name        : name,
            Price       : price,
            Special     : special,
            InStock     : inStock,
            StockStatus : inStock ? "In Stock" : "Out Of Stock",
            ImageUrl    : imageUrl,
            // صفحة المنتج المباشرة بالـ url_key تُعطي 404؛ البحث بالباركود يعمل.
            ProductUrl  : string.IsNullOrEmpty(sku) ? BaseUrl : $"{BaseUrl}/catalogsearch/result/?q={sku}"
        );
    }

    /// <summary>يحلّل تاريخ Magento ("yyyy-MM-dd HH:mm:ss") — null إن غاب أو فشل.</summary>
    private static DateTime? ParseDate(string? s)
        => DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
               System.Globalization.DateTimeStyles.None, out var dt) ? dt : null;
}
