// ===================================================
// مجمّع — يبحث في جميع المتاجر دفعة واحدة
// ===================================================
namespace JordanGrocery;

public class GroceryAggregator
{
    // المتاجر التي تدعم البحث بالباركود مباشرة
    private readonly List<IGroceryStoreClient> _regularStores;
    // متاجر طلبات — لا تكشف الباركود، تُستخدم GetByNameAsync كـ fallback
    private readonly List<TalabatClient> _talabatStores;

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
        // ── Hypermax (Carrefour rebranded) ─────────────────────────────────────
        ("Talabat — Hypermax City Center",   "698392", "store", 4809),
        ("Talabat — Hypermax Branch 388",    "698388", "store", 4914),
        ("Talabat — Hypermax Branch 389",    "698389", "store", 4914),
        ("Talabat — Hypermax Branch 390",    "698390", "store", 4914),
        ("Talabat — Hypermax Branch 391",    "698391", "store", 4914),
        ("Talabat — Hypermax Branch 393",    "698393", "store", 4914),
        ("Talabat — Hypermax Branch 394",    "698394", "store", 4914),
        ("Talabat — Hypermax Branch 395",    "698395", "store", 4914),
        ("Talabat — Hypermax Branch 396",    "698396", "store", 4914),
        ("Talabat — Hypermax Branch 397",    "698397", "store", 4914),

        // ── Carrefour (still shown as Carrefour in Talabat system) ─────────────
        ("Talabat — Carrefour Swaifyeh",     "600928", "carrefour-al-swaifyeh", 4914),

        // ── Safeway ────────────────────────────────────────────────────────────
        ("Talabat — Safeway Al Sahel",       "49320",  "safeway-al-sahel",      4941),
        ("Talabat — Safeway Branch 321",     "49321",  "store",                 4914),
        ("Talabat — Safeway Branch 322",     "49322",  "store",                 4914),
        ("Talabat — Safeway Muqabalain",     "49323",  "safeway-al-muqabalain", 6636),
        ("Talabat — Safeway Branch 324",     "49324",  "store",                 4914),
        ("Talabat — Safeway Branch 325",     "49325",  "store",                 4914),
        ("Talabat — Safeway Branch 326",     "49326",  "store",                 4914),
        ("Talabat — Safeway Branch 327",     "49327",  "store",                 4914),
    ];

    public GroceryAggregator(
        string martooKey    = "",
        string martooSecret = "")
    {
        _regularStores = new List<IGroceryStoreClient>
        {
            new YaserMallClient(),       // ياسر مول      — OpenCart productSearch ✅
            new CTownClient(),           // C-Town        — Magento 2 GraphQL ✅
            new HypermaxClient(),        // هايبرماكس       — MAF Next.js API ✅
            new CozmoClient(),           // كوزمو          — HTML Scraper ✅
            new MartooClient(martooKey, martooSecret), // مارتو — WooCommerce ✅
            new CarrefourJordanClient(), // كارفور الأردن  — ⚠️ متوقف
            new SamehMallClient(),       // سامح مول       — ⚠️ متوقف
            new DookantiClient(),        // دكانتي         — ⚠️ متوقف
        };

        // متاجر طلبات — تُبحث بالاسم (لا تكشف الباركود)
        _talabatStores = TalabatStores
            .Select(t => new TalabatClient(t.Name, t.BranchId, t.BranchSlug, t.AreaId))
            .ToList();
    }

    /// <summary>
    /// ابحث في جميع المتاجر بالتوازي.
    /// المتاجر العادية: بحث بالباركود.
    /// متاجر طلبات: بحث بالاسم (fallback) باستخدام أول اسم منتج وُجد في المتاجر العادية.
    /// </summary>
    public async Task<List<ProductInfo>> SearchAllAsync(string barcode)
    {
        // ── المرحلة 1: ابحث في المتاجر العادية بالباركود بالتوازي ──────────
        var regularTasks = _regularStores.Select(async store =>
        {
            try
            {
                var result = await store.GetByBarcodeAsync(barcode);
                var status = result is not null ? "✅ وُجد" : "➖ غير موجود";
                Console.WriteLine($"  {store.StoreName,-30} {status}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {store.StoreName,-30} ❌ خطأ: {ex.Message}");
                return (ProductInfo?)null;
            }
        });

        var regularResults = (await Task.WhenAll(regularTasks))
            .Where(r => r is not null).Cast<ProductInfo>().ToList();

        // ── المرحلة 2: ابحث في طلبات بالاسم إن وُجد المنتج في متجر آخر ──
        // نأخذ أقصر اسم منتج (عادةً اسم المنتج الإنجليزي أوضح للمطابقة)
        var productName = regularResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .OrderBy(r => r.Name.Length)
            .FirstOrDefault()?.Name;

        List<ProductInfo> talabatResults = [];

        if (!string.IsNullOrEmpty(productName))
        {
            // استخرج أول كلمتين فقط للبحث (تقليل الضوضاء)
            var searchTerm = string.Join(" ", productName.Split(' ').Take(2));
            Console.WriteLine($"\n  طلبات: بحث بالاسم «{searchTerm}»");

            var talabatTasks = _talabatStores.Select(async store =>
            {
                try
                {
                    var result = await store.GetByNameAsync(searchTerm);
                    var status = result is not null ? "✅ وُجد" : "➖ غير موجود";
                    Console.WriteLine($"  {store.StoreName,-30} {status}");
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  {store.StoreName,-30} ❌ خطأ: {ex.Message}");
                    return (ProductInfo?)null;
                }
            });

            talabatResults = (await Task.WhenAll(talabatTasks))
                .Where(r => r is not null).Cast<ProductInfo>().ToList();
        }
        else
        {
            // المنتج غير موجود في أي متجر عادي — أبلغ فقط
            foreach (var store in _talabatStores)
                Console.WriteLine($"  {store.StoreName,-30} ➖ تخطى (لا اسم للبحث)");
        }

        return [.. regularResults, .. talabatResults];
    }

    /// <summary>ابحث في متجر واحد بالاسم</summary>
    public async Task<ProductInfo?> SearchStoreAsync(string storeName, string barcode)
    {
        var store = _regularStores.FirstOrDefault(s =>
            s.StoreName.Equals(storeName, StringComparison.OrdinalIgnoreCase));
        if (store is null) return null;
        return await store.GetByBarcodeAsync(barcode);
    }

    public IReadOnlyList<string> StoreNames =>
        [.. _regularStores.Select(s => s.StoreName), .. _talabatStores.Select(s => s.StoreName)];

    /// <summary>
    /// امسح الباركود في جميع المتاجر ثم قارن الأسعار:
    /// يعيد العروض مرتّبة تصاعديًا بالسعر الفعلي مع تحديد الأرخص والأغلى ومقدار التوفير.
    /// </summary>
    public async Task<PriceComparison> ScanCompareAsync(string barcode)
    {
        var found = await SearchAllAsync(barcode);

        var offers = found
            .Select(StoreOffer.From)
            .OrderBy(o => o.EffectivePrice)
            .ToList();

        // اعتمد المتوفر بسعر موجب لحساب الأرخص/التوفير، وإلا ارجع لكل العروض ذات السعر الموجب
        var priced  = offers.Where(o => o.EffectivePrice > 0).ToList();
        var inStock = priced.Where(o => o.InStock).ToList();
        var basis   = inStock.Count > 0 ? inStock : priced;

        var cheapest      = basis.FirstOrDefault();
        var mostExpensive = basis.LastOrDefault();
        var maxSavings    = cheapest is not null && mostExpensive is not null
            ? mostExpensive.EffectivePrice - cheapest.EffectivePrice
            : 0m;

        return new PriceComparison(barcode, offers, cheapest, mostExpensive, maxSavings);
    }
}

// ===================================================
// نتيجة مقارنة الأسعار
// ===================================================

/// <summary>عرض متجر واحد للمنتج مع السعر الفعلي (بعد التخفيض إن وُجد).</summary>
public record StoreOffer(
    string  Store,
    decimal EffectivePrice,   // السعر الفعلي = Special إن وُجد وإلا Price
    decimal Price,
    decimal Special,
    bool    InStock,
    string  StockStatus,
    string  Name,
    string  ImageUrl,
    string  ProductUrl)
{
    public static StoreOffer From(ProductInfo p) => new(
        Store:          p.Store,
        EffectivePrice: p.Special > 0 ? p.Special : p.Price,
        Price:          p.Price,
        Special:        p.Special,
        InStock:        p.InStock,
        StockStatus:    p.StockStatus,
        Name:           p.Name,
        ImageUrl:       p.ImageUrl,
        ProductUrl:     p.ProductUrl);
}

/// <summary>حصيلة مسح الباركود عبر كل المتاجر: العروض مرتّبة + الأرخص/الأغلى + التوفير.</summary>
public record PriceComparison(
    string                    Barcode,
    IReadOnlyList<StoreOffer> Offers,         // مرتّبة تصاعديًا بالسعر الفعلي
    StoreOffer?               Cheapest,
    StoreOffer?               MostExpensive,
    decimal                   MaxSavings)
{
    public int StoresFound => Offers.Count;
}

// ===================================================
// مثال على الاستخدام
// ===================================================
class Program
{
    static async Task Main(string[] args)
    {
        var barcode = args.Length > 0 ? args[0] : "6253363801751";
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
