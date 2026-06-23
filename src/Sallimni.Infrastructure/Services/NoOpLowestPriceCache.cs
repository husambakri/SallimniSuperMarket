using Sallimni.Application.Abstractions;

namespace Sallimni.Infrastructure.Services;

/// <summary>
/// بديل صامت عند غياب Redis (تنمية محليّة أو لم يُضبط REDIS_URL): القراءة ترجع null
/// فيعتمد المتّصل على القاعدة، والكتابة لا تفعل شيئاً. يبقى الخادم يعمل كما لو لا كاش.
/// </summary>
public sealed class NoOpLowestPriceCache : ILowestPriceCache
{
    public Task<LowestPrice?> GetAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult<LowestPrice?>(null);

    public Task SetAsync(string barcode, LowestPrice value, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string barcode, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task EnsureDurabilityAsync(CancellationToken ct = default)
        => Task.CompletedTask;
}
