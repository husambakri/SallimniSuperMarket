// ===================================================
// مجمّع — يبحث في جميع المتاجر دفعة واحدة
// ===================================================
namespace JordanGrocery;

public class GroceryAggregator
{
    // المتاجر التي تدعم البحث بالباركود مباشرة وحيًّا.
    // (متاجر طلبات تُفهرَس دوريًّا في جدول TalabatPriceIndex وتُقرأ من هناك، لا حيًّا.)
    private readonly List<IGroceryStoreClient> _regularStores;

    public GroceryAggregator(
        string martooKey    = "",
        string martooSecret = "")
    {
        _regularStores = new List<IGroceryStoreClient>
        {
            // ياسر مول: يُسحب كتالوجه كاملًا في الفهرس (StoreCatalogIndexService)، فلا يُبحث حيًّا هنا.
            new CTownClient(),           // C-Town        — Magento 2 GraphQL ✅
            new HypermaxClient(),        // هايبرماكس       — MAF Next.js API ✅
            new CozmoClient(),           // كوزمو          — HTML Scraper ✅
            new CentroMarketClient(),                  // سنترو ماركت    — Custom REST API ✅
            new MartooClient(martooKey, martooSecret), // مارتو — WooCommerce ✅
            new CarrefourJordanClient(), // كارفور الأردن  — ⚠️ متوقف
            new SamehMallClient(),       // سامح مول       — ⚠️ متوقف
            new DookantiClient(),        // دكانتي         — ⚠️ متوقف
        };
    }

    /// <summary>ابحث في متاجر الباركود الحيّة بالتوازي (طلبات تُقرأ من الفهرس منفصلةً).</summary>
    public async Task<List<ProductInfo>> SearchAllAsync(string barcode)
    {
        var tasks = _regularStores.Select(async store =>
        {
            try
            {
                var result = await store.GetByBarcodeAsync(barcode);
                Console.WriteLine($"  {store.StoreName,-30} {(result is not null ? "✅ وُجد" : "➖ غير موجود")}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {store.StoreName,-30} ❌ خطأ: {ex.Message}");
                return (ProductInfo?)null;
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Cast<ProductInfo>().ToList();
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
        _regularStores.Select(s => s.StoreName).ToList();

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
