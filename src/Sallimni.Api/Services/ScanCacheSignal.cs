using Microsoft.Extensions.Primitives;

namespace Sallimni.Api.Services;

/// <summary>
/// إشارة إبطال مشتركة لكاش نتائج المسح (scan-compare). كل مدخل كاش يربط
/// <see cref="Token"/>؛ واستدعاء <see cref="Reset"/> يُلغي الإشارة فتُخلى كل المدخلات
/// فوراً — يُستعمل بعد بذر/إعادة فهرسة متجر لإظهار الأسعار الجديدة دون انتظار TTL.
/// </summary>
public sealed class ScanCacheSignal
{
    private CancellationTokenSource _cts = new();

    /// <summary>توكن تغيير يُربط بكل مدخل كاش؛ يُطلَق عند Reset فيُخلي المدخل.</summary>
    public IChangeToken Token => new CancellationChangeToken(_cts.Token);

    /// <summary>يُخلي كل مدخلات كاش المسح المرتبطة بالإشارة الحاليّة.</summary>
    public void Reset()
    {
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }
}
