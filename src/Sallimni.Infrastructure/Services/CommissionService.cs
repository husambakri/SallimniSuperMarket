using Microsoft.EntityFrameworkCore;

namespace Sallimni.Infrastructure.Services;

/// <summary>يحلّ نسبة العمولة لتاجر: استثناء التاجر إن وُجد، وإلا النسبة الافتراضية (قسم 7).</summary>
public class CommissionService
{
    private readonly SallimniDbContext _db;
    public CommissionService(SallimniDbContext db) => _db = db;

    public async Task<decimal> ResolveRateAsync(Guid merchantId, CancellationToken ct = default)
    {
        var configs = await _db.CommissionConfigs.Where(c => c.IsActive).ToListAsync(ct);
        var merchantRate = configs.FirstOrDefault(c => c.MerchantId == merchantId)?.MerchantRate;
        if (merchantRate.HasValue) return merchantRate.Value;
        var def = configs.FirstOrDefault(c => c.MerchantId == null)?.DefaultRate;
        return def ?? 0.10m;
    }

    /// <summary>تحميل كل النسب مرّة واحدة لاستخدامها بشكل متزامن داخل محرّك التقسيم.</summary>
    public async Task<Func<Guid, decimal>> BuildResolverAsync(CancellationToken ct = default)
    {
        var configs = await _db.CommissionConfigs.Where(c => c.IsActive).ToListAsync(ct);
        var defaultRate = configs.FirstOrDefault(c => c.MerchantId == null)?.DefaultRate ?? 0.10m;
        var overrides = configs
            .Where(c => c.MerchantId != null && c.MerchantRate != null)
            .ToDictionary(c => c.MerchantId!.Value, c => c.MerchantRate!.Value);
        return mid => overrides.TryGetValue(mid, out var r) ? r : defaultRate;
    }
}
