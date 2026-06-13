using Microsoft.EntityFrameworkCore;
using Sallimni.Application.Services;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;

namespace Sallimni.Infrastructure.Services;

/// <summary>إدارة الموجات: إيجاد/إنشاء أقرب موجة قادمة لإلحاق الطلب بها (قسم 5.1).</summary>
public class WaveService
{
    private readonly SallimniDbContext _db;
    public WaveService(SallimniDbContext db) => _db = db;

    public async Task<WaveConfig> GetConfigAsync(CancellationToken ct = default)
    {
        var cfg = await _db.WaveConfigs.FirstOrDefaultAsync(c => c.IsActive, ct);
        return cfg ?? new WaveConfig();
    }

    /// <summary>أقرب موجة جمع قادمة لوقت معيّن؛ تُنشأ إن لم توجد.</summary>
    public async Task<Wave> GetOrCreateWaveAsync(
        DateTimeOffset collectionAt, WaveConfig cfg, CancellationToken ct = default)
    {
        var wave = await _db.Waves
            .FirstOrDefaultAsync(w => w.CollectionStartAt == collectionAt && w.Status == WaveStatus.Open, ct);
        if (wave is not null) return wave;

        wave = new Wave
        {
            CollectionStartAt = collectionAt,
            DistributionStartAt = collectionAt.AddMinutes(cfg.DistributionGapMinutes),
            Status = WaveStatus.Open
        };
        _db.Waves.Add(wave);
        return wave;
    }
}
