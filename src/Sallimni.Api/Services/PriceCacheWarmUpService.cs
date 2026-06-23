using Microsoft.EntityFrameworkCore;
using Sallimni.Application.Abstractions;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Services;

/// <summary>
/// تسخين كاش أقل سعر عند الإقلاع: يفعّل AOF في Redis (متانة عبر إعادة التشغيل) ثم يملأ
/// أقل سعر متوفّر لكل منتج له باركود وعرض نشط. يُعاد يومياً ليُجدِّد TTL ويُسقط ما لم يعد نشطاً.
/// best-effort: لا يُسقط الخادم إن غاب Redis (الكاش حينها بديل صامت).
/// </summary>
public sealed class PriceCacheWarmUpService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILowestPriceCache _cache;
    private readonly ILogger<PriceCacheWarmUpService> _logger;

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Interval     = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetryOnError = TimeSpan.FromMinutes(10);

    public PriceCacheWarmUpService(
        IServiceScopeFactory scopeFactory, ILowestPriceCache cache, ILogger<PriceCacheWarmUpService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(StartupDelay, ct); } catch { return; }

        // تفعيل AOF مرّة عند الإقلاع قبل أوّل تسخين.
        await _cache.EnsureDurabilityAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            var next = Interval;
            try
            {
                var count = await WarmUpAsync(ct);
                _logger.LogInformation("[PriceCache] تسخين مكتمل — {Count} باركود نشط", count);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PriceCache] فشل التسخين — إعادة بعد {Min} دقيقة", RetryOnError.TotalMinutes);
                next = RetryOnError;
            }
            try { await Task.Delay(next, ct); } catch { return; }
        }
    }

    /// <summary>يحسب أقل سعر متوفّر لكل باركود نشط ويكتبه في الكاش. يرجع عدد الباركودات.</summary>
    private async Task<int> WarmUpAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();

        // كل العروض النشطة لمنتجات فعّالة لها باركود — تُجمّع بالباركود في الذاكرة.
        var offers = await db.MerchantProducts
            .Where(mp => mp.IsAvailable && mp.StockQty > 0
                      && mp.Product!.IsActive && mp.Product.Barcode != null && mp.Product.Barcode != "")
            .Select(mp => new { Barcode = mp.Product!.Barcode!, mp.Price, mp.MerchantId })
            .ToListAsync(ct);

        var groups = offers.GroupBy(o => o.Barcode);
        var count = 0;
        foreach (var g in groups)
        {
            ct.ThrowIfCancellationRequested();
            var cheapest = g.MinBy(o => o.Price)!;
            var regular = g.Max(o => o.Price);
            await _cache.SetAsync(
                g.Key, new LowestPrice(cheapest.Price, regular, cheapest.MerchantId), ct);
            count++;
        }
        return count;
    }
}
