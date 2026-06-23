namespace Sallimni.Application.Abstractions;

/// <summary>أقل سعر متوفّر لمنتج (شامل الضريبة) مع أعلى سعر للمقارنة وأرخص تاجر.</summary>
public record LowestPrice(decimal Price, decimal RegularPrice, Guid MerchantId);

/// <summary>
/// تخزين مؤقّت لأقل سعر متوفّر لكل باركود — يدعمه Redis في الإنتاج (طبقة L2 موثوقة
/// محفوظة بـ AOF) فوق ذاكرة L1 داخل العملية. يحتفظ بالأسعار النشطة فقط عبر TTL،
/// ويُحدَّث فوراً عند تغيّر السعر في القاعدة (Event Handler + Pub/Sub).
///
/// كل العمليات best-effort: عند تعذّر Redis يرجع القارئ null ويعتمد المتّصل على القاعدة،
/// فلا يتعطّل الخادم إن غاب Redis.
/// </summary>
public interface ILowestPriceCache
{
    /// <summary>أقل سعر متوفّر للباركود، أو null إن لم يُخزَّن (يلجأ المتّصل للقاعدة).</summary>
    Task<LowestPrice?> GetAsync(string barcode, CancellationToken ct = default);

    /// <summary>يخزّن/يحدّث أقل سعر للباركود (يجدّد TTL وينشر إبطال L1 على Pub/Sub).</summary>
    Task SetAsync(string barcode, LowestPrice value, CancellationToken ct = default);

    /// <summary>يحذف مفتاح الباركود — يُستعمل حين لا يبقى أي عرض متوفّر (إدارة الذاكرة).</summary>
    Task RemoveAsync(string barcode, CancellationToken ct = default);

    /// <summary>
    /// يضمن متانة Redis عند الإقلاع: تفعيل AOF (appendonly) لئلّا تضيع الأسعار عند
    /// إعادة تشغيل السيرفر. best-effort — يُتجاهل بصمت إن مُنع CONFIG.
    /// </summary>
    Task EnsureDurabilityAsync(CancellationToken ct = default);
}
