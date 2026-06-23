using JordanGrocery;
using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Services;

/// <summary>
/// مهمّة خلفية تكتشف متاجر بقالة المدينة من طلبات تلقائيًّا (بلا قائمة يدوية)
/// وتفهرس كتالوجاتها دوريًّا في <see cref="TalabatPriceEntry"/>. يقرأ فحص السعر
/// الحيّ من الجدول فورًا بالباركود دون ضرب طلبات وقت الطلب.
///
/// الاكتشاف: <see cref="TalabatDiscovery"/> يزحف مناطق المدينة ويوحّد المتاجر
/// بالاسم (فرع واحد لكل متجر). الفهرسة: لكل متجر يُمسح كتالوجه (باركود من sku).
/// كل شيء مُهدّأ و429-آمن؛ الفشل يبقي آخر فهرسة قائمة.
/// </summary>
public class TalabatIndexService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TalabatIndexService> _logger;

    // المدينة المستهدفة (slug كما في طلبات). قابلة للتوسعة لاحقًا لعدّة مدن.
    private const string CitySlug = "amman";

    private static readonly TimeSpan Interval        = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetryOnError    = TimeSpan.FromMinutes(20); // عند الفشل: أعد بسرعة لا بعد 6 ساعات
    private static readonly TimeSpan StartupDelay    = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan BetweenStores   = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PerStoreTimeout = TimeSpan.FromMinutes(3);  // متجر بطيء/محجوب يُتخطّى ولا يُجمّد الباقي

    public TalabatIndexService(IServiceScopeFactory scopeFactory, ILogger<TalabatIndexService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(StartupDelay, ct); } catch { return; } // انتظر الهجرة والإقلاع

        while (!ct.IsCancellationRequested)
        {
            var next = Interval;
            try { await RefreshAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TalabatIndex] فشل دورة الفهرسة — إعادة بعد {Min} دقيقة", RetryOnError.TotalMinutes);
                next = RetryOnError;
            }

            try { await Task.Delay(next, ct); } catch { return; }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        _logger.LogInformation("[TalabatIndex] اكتشاف متاجر «{City}» من طلبات…", CitySlug);
        var stores = await TalabatDiscovery.DiscoverCityStoresAsync(CitySlug, ct);
        _logger.LogInformation("[TalabatIndex] اكتُشف {Found} متجرًا (موحّدة بالاسم)؛ بدء فهرسة الجميع…", stores.Count);

        int okStores = 0, totalRows = 0;
        foreach (var store in stores)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // حدّ زمني لكل متجر: متجر بطيء/محجوب يُتخطّى بدل تجميد الدورة كلها.
                using var storeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                storeCts.CancelAfter(PerStoreTimeout);

                var client = new TalabatClient(store.Name, store.BranchId, store.Slug, store.AreaId);
                var products = await client.GetAllProductsAsync(storeCts.Token);
                await UpsertBranchAsync(store.BranchId, store.Name, store.Latitude, store.Longitude, products, ct);
                okStores++;
                totalRows += products.Count;
                _logger.LogInformation("[TalabatIndex] {Store} (aid={Aid}) → {Count} منتج", store.Name, store.AreaId, products.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } // إيقاف الخدمة
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[TalabatIndex] {Store} ({Branch}) تجاوز المهلة — تخطٍّ", store.Name, store.BranchId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[TalabatIndex] {Store} ({Branch}) فشل: {Msg}", store.Name, store.BranchId, ex.Message);
            }

            try { await Task.Delay(BetweenStores, ct); } catch { return; }
        }

        _logger.LogInformation("[TalabatIndex] انتهت: {Stores}/{Total} متجر مُفهرَس، {Rows} صف", okStores, stores.Count, totalRows);

        await PurgeStaleAsync(ct);
    }

    /// <summary>يحذف صفوف فروعٍ لم تُحدَّث منذ &gt;50 ساعة (دورتان يوميّتان) — متاجر لم تعد تُكتشف.</summary>
    private async Task PurgeStaleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromHours(50);
        var stale = await db.TalabatPriceIndex.Where(e => e.UpdatedAt < cutoff).ToListAsync(ct);
        if (stale.Count == 0) return;
        db.TalabatPriceIndex.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[TalabatIndex] حُذف {N} صفًّا قديمًا (تنظيف)", stale.Count);
    }

    /// <summary>يستبدل كل صفوف الفرع بالنتائج الجديدة (upsert ذرّي لكل فرع).</summary>
    private async Task UpsertBranchAsync(string branchId, string storeName, double? lat, double? lng,
        List<ProductInfo> products, CancellationToken ct)
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
                Latitude   = lat,
                Longitude  = lng,
                UpdatedAt  = now,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
