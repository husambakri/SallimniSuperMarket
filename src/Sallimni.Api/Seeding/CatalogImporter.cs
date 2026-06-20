using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Seeding;

/// <summary>
/// مستورِد الكتالوج الحقيقي من ملف TSV (مُصدَّر من إكسل طلبات-الأردن).
/// الأعمدة: Store, Category, Sub, Name, Price, ImageUrl, Sku.
/// • كل متجر مميّز → Merchant (بإحداثيات تقريبية حول عمّان).
/// • كل فئة رئيسية مميّزة → Category (مسطّحة، باسم عربي وأيقونة).
/// • كل SKU مميّز → Product واحد (بطاقة الصنف، أول ظهور يحسم الاسم/الصورة/الفئة).
/// • كل صف → MerchantProduct (سعر التاجر) — نفس SKU عبر تجار = مقارنة أسعار/توفير.
/// الإدراج دفعات (batched) مع تعطيل تتبّع التغييرات لأداء عالٍ على عشرات الآلاف.
/// </summary>
public static class CatalogImporter
{
    public sealed record ImportSummary(int Merchants, int Categories, int Products, int MerchantProducts);

    private const int BatchSize = 5000;

    // الاسم العربي + الأيقونة + شريحة الضريبة التقديرية لكل فئة رئيسية (سلَگ طلبات).
    private static readonly Dictionary<string, (string Ar, string Icon, TaxClass Tax)> CategoryMap = new()
    {
        ["baby-corner"]            = ("ركن الأطفال", "👶", TaxClass.Sixteen),
        ["bakery"]                 = ("مخبوزات", "🍞", TaxClass.Zero),
        ["beverages"]              = ("مشروبات", "🥤", TaxClass.Five),
        ["breakfast-food"]         = ("فطور", "🥣", TaxClass.Zero),
        ["canned-jarred"]          = ("معلّبات", "🥫", TaxClass.Sixteen),
        ["cleaning-laundry"]       = ("تنظيف وغسيل", "🧽", TaxClass.Sixteen),
        ["coffee-tea"]             = ("قهوة وشاي", "☕", TaxClass.Five),
        ["condiments"]             = ("صلصات وتوابل", "🧂", TaxClass.Sixteen),
        ["cooking-baking"]         = ("طبخ وخبز", "🍳", TaxClass.Zero),
        ["dairy-eggs"]             = ("ألبان وبيض", "🧀", TaxClass.Zero),
        ["deli"]                   = ("مأكولات جاهزة", "🥓", TaxClass.Sixteen),
        ["disposables"]            = ("أدوات استهلاكية", "🧻", TaxClass.Sixteen),
        ["frozen-food"]            = ("مجمّدات", "🧊", TaxClass.Sixteen),
        ["fruit-veg"]              = ("خضار وفواكه", "🥬", TaxClass.Zero),
        ["health-beauty"]          = ("صحّة وجمال", "💄", TaxClass.Sixteen),
        ["household-essentials"]   = ("مستلزمات منزلية", "🏠", TaxClass.Sixteen),
        ["ice-cream"]              = ("آيس كريم", "🍦", TaxClass.Sixteen),
        ["milk"]                   = ("حليب", "🥛", TaxClass.Zero),
        ["personal-care"]          = ("عناية شخصية", "🧴", TaxClass.Sixteen),
        ["pet-care"]               = ("مستلزمات الحيوانات", "🐾", TaxClass.Sixteen),
        ["poultry-meat-seafood"]   = ("لحوم ودواجن وأسماك", "🍗", TaxClass.Zero),
        ["protein-special-diet"]   = ("بروتين وحمية", "💪", TaxClass.Sixteen),
        ["ready-to-eat"]           = ("جاهز للأكل", "🍱", TaxClass.Sixteen),
        ["snacks-chocolate"]       = ("سناكس وشوكولاتة", "🍫", TaxClass.Sixteen),
        ["stationery-games"]       = ("قرطاسية وألعاب", "✏️", TaxClass.Sixteen),
        ["tobacco"]                = ("تبغ", "🚬", TaxClass.Sixteen),
    };

    public static async Task<ImportSummary> ImportAsync(
        SallimniDbContext db, string tsvPath, ILogger logger, CancellationToken ct = default)
    {
        if (!File.Exists(tsvPath))
        {
            logger.LogWarning("CatalogImporter: ملف البيانات غير موجود: {Path}", tsvPath);
            return new ImportSummary(0, 0, 0, 0);
        }

        var prevAutoDetect = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            // ── 1) القراءة + بناء الرسم البياني في الذاكرة (مع توليد Guid مسبقاً) ──
            var merchants  = new Dictionary<string, Merchant>(StringComparer.OrdinalIgnoreCase);
            var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            var products   = new Dictionary<string, Product>();           // sku → Product
            var mpKeys     = new Dictionary<(Guid, Guid), MerchantProduct>(); // (merchantId, productId) فريد
            var sortOrder  = 0;

            using var reader = new StreamReader(tsvPath, System.Text.Encoding.UTF8);
            await reader.ReadLineAsync(ct); // تجاوز الترويسة

            string? line;
            var lineNo = 1;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                lineNo++;
                if (line.Length == 0) continue;
                var f = line.Split('\t');
                if (f.Length < 7) continue;

                var store = f[0].Trim();
                var catCode = f[1].Trim();
                var name = f[3].Trim();
                var priceStr = f[4].Trim();
                var img = f[5].Trim();
                var sku = NormalizeBarcode(f[6]);
                if (store.Length == 0 || name.Length == 0) continue;

                // المتجر
                if (!merchants.TryGetValue(store, out var merchant))
                {
                    merchant = new Merchant { Name = store, IsSalesTaxRegistered = true, IsActive = true };
                    AssignAmmanLocation(merchant, merchants.Count);
                    merchants[store] = merchant;
                }

                // الفئة الرئيسية
                if (catCode.Length == 0) catCode = "household-essentials";
                if (!categories.TryGetValue(catCode, out var category))
                {
                    var map = CategoryMap.TryGetValue(catCode, out var m)
                        ? m : (Humanize(catCode), "🛒", TaxClass.Sixteen);
                    category = new Category
                    {
                        NameEn = Humanize(catCode), NameAr = map.Item1,
                        Icon = map.Item2, SortOrder = sortOrder++
                    };
                    categories[catCode] = category;
                }

                // بطاقة الصنف (مجمّعة بالـ SKU). بدون SKU → مفتاح بالاسم+المتجر لتفادي تضخّم مزيّف.
                var productKey = sku.Length > 0 ? $"sku:{sku}" : $"name:{store}|{name}";
                if (!products.TryGetValue(productKey, out var product))
                {
                    var tax = CategoryMap.TryGetValue(catCode, out var cm) ? cm.Tax : TaxClass.Sixteen;
                    product = new Product
                    {
                        NameEn = name, NameAr = name,
                        Barcode = sku.Length > 0 ? sku : null,
                        ImageUrl = img.Length > 0 ? img : null,
                        CategoryId = category.Id, TaxClass = tax, IsActive = true
                    };
                    products[productKey] = product;
                }

                // سعر التاجر لهذا الصنف — مع تجاهل القيم الفاسدة في المصدر
                // (بعض الصفوف بها الباركود مكرّراً في خانة السعر؛ لا سلعة بقالة تتجاوز ~5000 دينار).
                if (!decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                    continue;
                if (price <= 0 || price > 5000m) continue;

                var key = (merchant.Id, product.Id);
                if (mpKeys.TryGetValue(key, out var existing))
                {
                    if (price < existing.Price) existing.Price = price; // أبقِ الأرخص عند التكرار
                }
                else
                {
                    mpKeys[key] = new MerchantProduct
                    {
                        MerchantId = merchant.Id, ProductId = product.Id,
                        Price = price, StockQty = 100, IsAvailable = true
                    };
                }
            }

            // ── 1ب) تنظيف الأسعار الشاذّة لكل صنف ──
            // بعض صفوف المصدر بها سعر خاطئ (أضعافاً) يصنع "توفير" وهمياً.
            //  • صنف بـ3 أسعار فأكثر: نُبقي ما يقع ضمن [الوسيط÷3 , الوسيط×3] (يحذف الشاذّ عالياً ومنخفضاً).
            //  • صنف بسعر/سعرين: حارس علوي فقط (نحذف ما يتجاوز 10× الأرخص) إذ يتعذّر تمييز الشاذّ المنخفض.
            var cleanedMps = new List<MerchantProduct>(mpKeys.Count);
            foreach (var grp in mpKeys.Values.GroupBy(mp => mp.ProductId))
            {
                var list = grp.ToList();
                if (list.Count >= 3)
                {
                    var sorted = list.Select(x => x.Price).OrderBy(x => x).ToList();
                    var median = sorted[sorted.Count / 2];
                    var lo = median / 3m;
                    var hi = median * 3m;
                    cleanedMps.AddRange(list.Where(mp => mp.Price >= lo && mp.Price <= hi));
                }
                else if (list.Count == 2)
                {
                    // سعران: فارق منطقي (≤6×) نُبقيهما؛ فارق متطرّف غير قابل للحسم نُسقطهما معاً.
                    var min = list.Min(x => x.Price);
                    var max = list.Max(x => x.Price);
                    if (max <= 6m * min) cleanedMps.AddRange(list);
                }
                else
                {
                    cleanedMps.AddRange(list); // سعر واحد — يُبقى كما هو
                }
            }
            var dropped = mpKeys.Count - cleanedMps.Count;

            logger.LogInformation(
                "CatalogImporter: قُرئ {Lines} صف → {M} متجر، {C} فئة، {P} صنف، {MP} سعر (حُذف {D} سعراً شاذّاً).",
                lineNo - 1, merchants.Count, categories.Count, products.Count, cleanedMps.Count, dropped);

            // ── 2) الإدراج الدفعي ──
            db.Categories.AddRange(categories.Values);
            db.Merchants.AddRange(merchants.Values);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            await BulkInsertAsync(db, products.Values, ct);
            await BulkInsertAsync(db, cleanedMps, ct);

            logger.LogInformation("CatalogImporter: اكتمل الاستيراد.");
            return new ImportSummary(merchants.Count, categories.Count, products.Count, cleanedMps.Count);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = prevAutoDetect;
        }
    }

    private static async Task BulkInsertAsync<T>(
        SallimniDbContext db, IEnumerable<T> items, CancellationToken ct) where T : class
    {
        var batch = new List<T>(BatchSize);
        foreach (var item in items)
        {
            batch.Add(item);
            if (batch.Count >= BatchSize)
            {
                await db.Set<T>().AddRangeAsync(batch, ct);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            await db.Set<T>().AddRangeAsync(batch, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    // إحداثيات تقريبية موزّعة على حلقة حول مركز عمّان — تكفي لمسار التجميع والـETA.
    private static void AssignAmmanLocation(Merchant m, int index)
    {
        const double centerLat = 31.9539, centerLon = 35.9106;
        var angle = index * 2.399963; // الزاوية الذهبية لتوزيع متباعد
        var radius = 0.015 + (index % 5) * 0.006;
        m.Latitude = centerLat + radius * Math.Cos(angle);
        m.Longitude = centerLon + radius * Math.Sin(angle);
    }

    /// <summary>
    /// تطبيع الباركود: المصدر يخلط صيغاً لنفس المنتج (EAN نظيف، أو مسبوق بكود داخلي مثل
    /// "503185_6253339102080"، أو باركودين بفاصلة). نستخرج أطول سلسلة أرقام = الـEAN الذي
    /// يُمسح فعلياً، فتتوحّد بطاقة الصنف عبر التجار. الرموز الداخلية غير الرقمية تبقى كما هي.
    /// </summary>
    private static string NormalizeBarcode(string? sku)
    {
        sku = sku?.Trim() ?? string.Empty;
        if (sku.Length == 0) return string.Empty;

        var best = "";
        var i = 0;
        while (i < sku.Length)
        {
            if (char.IsDigit(sku[i]))
            {
                var j = i;
                while (j < sku.Length && char.IsDigit(sku[j])) j++;
                if (j - i > best.Length) best = sku.Substring(i, j - i);
                i = j;
            }
            else i++;
        }
        return best.Length >= 6 ? best : sku; // ≥6 أرقام = باركود معقول؛ وإلا رمز داخلي
    }

    private static string Humanize(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return slug;
        var words = slug.Replace('-', ' ').Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
