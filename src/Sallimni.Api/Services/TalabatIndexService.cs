using System.Text.RegularExpressions;
using JordanGrocery;
using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Services;

/// <summary>
/// مهمّة خلفية تفهرس كتالوجات متاجر طلبات الأردنية دوريًّا في جدول
/// <see cref="TalabatPriceEntry"/>. لا واجهة بحث بالباركود في طلبات، فنمسح
/// كل متجر مرّة كل بضع ساعات (بهدوء) ونخزّن (باركود من sku + سعر + توفّر)؛
/// ثم يقرأ فحص السعر الحيّ من الجدول فورًا دون ضرب طلبات وقت الطلب.
/// </summary>
public class TalabatIndexService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TalabatIndexService> _logger;

    private static readonly TimeSpan Interval     = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay  = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan BetweenStores = TimeSpan.FromSeconds(2);

    // مناطق مرشّحة لاكتشاف منطقة كل فرع تلقائيًّا (الـslug "store" يعمل لكل الفروع).
    private static readonly int[] CandidateAids = { 4809, 4941, 6636, 4914 };

    // متاجر طلبات في المنطقة: (الاسم, معرّف الفرع). المنطقة تُكتشف تلقائيًّا.
    private static readonly (string Name, string BranchId)[] Stores =
    {
        ("Gourmet Grocery Supermarket", "49447"),
        ("Farah Way Stores", "676935"),
        ("Hypermax (698360)", "698360"),
        ("Abu Odeh Stores gold", "726851"),
        ("AlSaheel Supermarket", "796372"),
        ("My Market", "644116"),
        ("JJ's Supermarket (770932)", "770932"),
        ("Bustan", "780383"),
        ("JJ's Supermarket (717856)", "717856"),
        ("Majd Al Deen Markets", "790282"),
        ("Al Raya Bakery", "695241"),
        ("bon al Badawiya Coffee", "46288"),
        ("Brazilian Coffee House", "652243"),
        ("Izhiman Premier", "46490"),
        ("Saffaron Kingdom", "48883"),
        ("Safeway", "49320"),
        ("AL Markazi Fruits", "49701"),
        ("Paradise Bakeries", "50365"),
        ("Forangy Bakery", "650654"),
        ("Basman Roastery - Madina St", "50638"),
        ("Izhiman's Coffee", "711506"),
        ("Food Gate Market", "766762"),
        ("Centro", "715604"),
        ("Grape for Fruits and Vegetables", "732475"),
        ("Talabat Mart", "642456"),
        ("Aswaq Ya Hala", "660700"),
        ("Jannat Alnaeam For Fruit And Vegetable", "661231"),
        ("More Market", "679082"),
        ("Mega Super Market", "679299"),
        ("Kefya Market", "682206"),
        ("Talabat Mart - Wellness", "700196"),
        ("Grand Mall For Fruits And Vegetables", "743998"),
        ("City mart", "761760"),
        ("3045 Supermarket", "1100915"),
        ("Al Rayhan", "671844"),
        ("Military Consumer Establishment", "621346"),
        ("Al Ameed Coffee", "657766"),
        ("Al-Hawary Mall", "632258"),
        ("Durra Markets", "705543"),
        ("Irbid Mall Grand", "681049"),
        ("Smile Market", "687278"),
        ("Hypermax (698392)", "698392"),
        ("Irbid Mall Center", "758124"),
        ("Safrjal Fruits and Vegetables", "764908"),
        ("Aswaq AlSultan Alestihlakiya", "777335"),
        ("Rakan AlMarkazi", "779531"),
        ("Alrayyan international markets", "780758"),
        ("Abu Elize Shop for Vegetables and Fruits", "790702"),
    };

    public TalabatIndexService(IServiceScopeFactory scopeFactory, ILogger<TalabatIndexService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(StartupDelay, ct); } catch { return; } // انتظر اكتمال الهجرة والإقلاع

        while (!ct.IsCancellationRequested)
        {
            try { await RefreshAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "[TalabatIndex] فشل دورة الفهرسة"); }

            try { await Task.Delay(Interval, ct); } catch { return; }
        }
    }

    /// <summary>يوحّد اسم المتجر للمقارنة: يزيل لاحقة "(معرّف)" والفراغات والحالة.</summary>
    private static string NormalizeName(string name)
        => Regex.Replace(name, @"\s*\(\d+\)\s*$", "").Trim().ToLowerInvariant();

    private async Task RefreshAsync(CancellationToken ct)
    {
        // فرع واحد لكل اسم متجر (لا تكرار لأفرع نفس السلسلة — نفس السعر).
        var unique = Stores
            .GroupBy(s => NormalizeName(s.Name))
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation("[TalabatIndex] بدء الفهرسة لـ {Count} متجر (بعد توحيد الأسماء من {Total})",
            unique.Count, Stores.Length);
        int okStores = 0, totalRows = 0;

        foreach (var (name, branchId) in unique)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (client, aid) = await ResolveClientAsync(name, branchId, ct);
                if (client is null)
                {
                    _logger.LogWarning("[TalabatIndex] {Store} ({Branch}) — لا كتالوج (منطقة غير معروفة/مغلق)", name, branchId);
                    continue;
                }

                var products = await client.GetAllProductsAsync(ct);
                await UpsertBranchAsync(branchId, name, products, ct);
                okStores++;
                totalRows += products.Count;
                _logger.LogInformation("[TalabatIndex] {Store} (aid={Aid}) → {Count} منتج", name, aid, products.Count);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning("[TalabatIndex] {Store} ({Branch}) فشل: {Msg}", name, branchId, ex.Message);
            }

            try { await Task.Delay(BetweenStores, ct); } catch { return; }
        }

        _logger.LogInformation("[TalabatIndex] انتهت: {Stores}/{Total} متجر مُفهرَس، {Rows} صف", okStores, Stores.Length, totalRows);
    }

    /// <summary>يجرّب المناطق المرشّحة ويعيد أول عميل يُرجع كتالوجًا.</summary>
    private static async Task<(TalabatClient? Client, int Aid)> ResolveClientAsync(
        string name, string branchId, CancellationToken ct)
    {
        foreach (var aid in CandidateAids)
        {
            var client = new TalabatClient(name, branchId, "store", aid);
            if (await client.HasCatalogAsync(ct)) return (client, aid);
        }
        return (null, 0);
    }

    /// <summary>يستبدل كل صفوف الفرع بالنتائج الجديدة (upsert ذرّي لكل فرع).</summary>
    private async Task UpsertBranchAsync(string branchId, string storeName, List<ProductInfo> products, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();

        var old = await db.TalabatPriceIndex.Where(e => e.BranchId == branchId).ToListAsync(ct);
        db.TalabatPriceIndex.RemoveRange(old);

        var now = DateTimeOffset.UtcNow;
        foreach (var p in products)
        {
            db.TalabatPriceIndex.Add(new TalabatPriceEntry
            {
                BranchId   = branchId,
                StoreName  = storeName,
                Barcode    = p.Barcode,
                Name       = p.Name,
                Price      = p.Price,
                Special    = p.Special,
                InStock    = p.InStock,
                ImageUrl   = p.ImageUrl,
                ProductUrl = p.ProductUrl,
                UpdatedAt  = now,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
