// ===================================================
// مجمّع — يبحث في جميع المتاجر دفعة واحدة
// ===================================================
namespace JordanGrocery;

public class GroceryAggregator
{
    private readonly List<IGroceryStoreClient> _stores;

    /// <summary>
    /// Known Talabat Jordan grocery stores.
    /// Add more stores here as they are discovered.
    /// Each entry: (storeName, branchId, branchSlug, areaId)
    ///
    /// To find a store's branchId and branchSlug, navigate to the store on
    /// talabat.com — they appear in the URL:
    ///   talabat.com/jordan/grocery/{branchId}/{branchSlug}?aid={areaId}
    ///
    /// Area IDs for common Jordan areas:
    ///   4809 = Al Mala'ab (Irbid)
    ///   ↑ Use the ?aid= value from any store URL in that area.
    /// </summary>
    private static readonly (string Name, string BranchId, string BranchSlug, int AreaId)[] TalabatStores =
    [
        ("Talabat — Hypermax City Center",   "698392", "hypermax-city-center-042",    4809),
        // ── Add more Talabat stores below ──────────────────────────────────────
        // ("Talabat — Safeway Abdali",       "XXXXX",  "safeway-abdali",             YYYY),
        // ("Talabat — Cozmo University St",  "XXXXX",  "cozmo-university-street",    YYYY),
    ];

    public GroceryAggregator(
        string martooKey    = "",
        string martooSecret = "")
    {
        _stores = new List<IGroceryStoreClient>
        {
            new YaserMallClient(),       // ياسر مول      — OpenCart API ✅
            new CarrefourJordanClient(), // كارفور الأردن  — MAF/Hybris API
            new HypermaxClient(),        // هايبرماكس       — MAF/Hybris API
            new CozmoClient(),           // كوزمو          — HTML Scraper
            new SamehMallClient(),       // سامح مول       — OpenCart API
            new MartooClient(martooKey, martooSecret), // مارتو — WooCommerce
            new DookantiClient(),        // دكانتي         — OpenCart API
        };

        // Add all known Talabat stores
        foreach (var (name, branchId, branchSlug, areaId) in TalabatStores)
            _stores.Add(new TalabatClient(name, branchId, branchSlug, areaId));
    }

    /// <summary>ابحث في جميع المتاجر بالتوازي</summary>
    public async Task<List<ProductInfo>> SearchAllAsync(string barcode)
    {
        var tasks = _stores.Select(async store =>
        {
            try   { return await store.GetByBarcodeAsync(barcode); }
            catch { return (ProductInfo?)null; }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Cast<ProductInfo>().ToList();
    }

    /// <summary>ابحث في متجر واحد بالاسم</summary>
    public async Task<ProductInfo?> SearchStoreAsync(string storeName, string barcode)
    {
        var store = _stores.FirstOrDefault(s =>
            s.StoreName.Equals(storeName, StringComparison.OrdinalIgnoreCase));
        if (store is null) return null;
        return await store.GetByBarcodeAsync(barcode);
    }

    public IReadOnlyList<string> StoreNames => _stores.Select(s => s.StoreName).ToList();
}

// ===================================================
// مثال على الاستخدام
// ===================================================
class Program
{
    static async Task Main(string[] args)
    {
        var barcode = args.Length > 0 ? args[0] : "7885237890081";
        Console.WriteLine($"🔍 بحث عن الباركود: {barcode}\n");

        var agg = new GroceryAggregator();
        Console.WriteLine($"المتاجر: {string.Join(", ", agg.StoreNames)}\n");

        var results = await agg.SearchAllAsync(barcode);

        if (results.Count == 0)
        {
            Console.WriteLine("❌ لم يُعثر على المنتج في أي متجر.");
            return;
        }

        Console.WriteLine($"✅ وُجد في {results.Count} متجر:\n");
        foreach (var p in results.OrderBy(p => p.Price))
        {
            var effectivePrice = p.Special > 0 ? p.Special : p.Price;
            Console.WriteLine($"🏪 {p.Store,-22} | {effectivePrice:F2} د.أ | {p.StockStatus,-12} | {p.Name}");
            Console.WriteLine($"   الصورة : {p.ImageUrl}");
            Console.WriteLine($"   الرابط : {p.ProductUrl}\n");
        }
    }
}
