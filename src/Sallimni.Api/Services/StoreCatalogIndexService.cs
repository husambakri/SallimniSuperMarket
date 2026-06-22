using JordanGrocery;
using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Services;

/// <summary>
/// مهمّة خلفية تسحب كامل كتالوجات المتاجر المستقلّة (التي تدعم تعداد كل المنتجات
/// عبر <see cref="ICatalogStoreClient"/>) وتخزّنها في الفهرس. يقرأ فحص السعر الحيّ
/// منها فورًا بالباركود. قابلة للتوسّع: أضف عميلًا جديدًا إلى <see cref="Stores"/>.
/// </summary>
public class StoreCatalogIndexService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StoreCatalogIndexService> _logger;

    private static readonly TimeSpan Interval        = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay    = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryOnError    = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan PerStoreTimeout = TimeSpan.FromMinutes(30);

    // المتاجر المستقلّة القابلة لسحب الكتالوج الكامل (تُضاف هنا تباعًا).
    private static readonly Func<ICatalogStoreClient>[] Stores =
    {
        () => new YaserMallClient(),
        () => new CTownClient(),
    };

    public StoreCatalogIndexService(IServiceScopeFactory scopeFactory, ILogger<StoreCatalogIndexService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(StartupDelay, ct); } catch { return; }

        while (!ct.IsCancellationRequested)
        {
            var next = Interval;
            try { await RefreshAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoreIndex] فشل دورة السحب — إعادة بعد {Min} دقيقة", RetryOnError.TotalMinutes);
                next = RetryOnError;
            }
            try { await Task.Delay(next, ct); } catch { return; }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        foreach (var factory in Stores)
        {
            ct.ThrowIfCancellationRequested();
            var client = factory();
            try
            {
                _logger.LogInformation("[StoreIndex] بدء سحب كتالوج {Store}…", client.StoreName);

                using var storeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                storeCts.CancelAfter(PerStoreTimeout);

                var products = await client.GetAllProductsAsync(storeCts.Token);
                await UpsertStoreAsync(client.StoreName, products, ct);
                _logger.LogInformation("[StoreIndex] {Store} → {Count} منتج", client.StoreName, products.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[StoreIndex] {Store} تجاوز المهلة — تخطٍّ", client.StoreName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[StoreIndex] {Store} فشل: {Msg}", client.StoreName, ex.Message);
            }
        }
    }

    /// <summary>يستبدل كل صفوف المتجر بالنتائج الجديدة (upsert ذرّي لكل متجر).</summary>
    private async Task UpsertStoreAsync(string storeName, List<ProductInfo> products, CancellationToken ct)
    {
        if (products.Count == 0) return; // لا تمسح القديم إن فشل السحب

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();

        var branchId = "store:" + storeName; // مفتاح ثابت لكل متجر مستقل
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
