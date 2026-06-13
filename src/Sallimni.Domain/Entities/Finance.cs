using Sallimni.Domain.Common;
using Sallimni.Domain.Enums;

namespace Sallimni.Domain.Entities;

/// <summary>تسوية مالية لطلب فرعي: تحصيل COD ← توريد ← دفع التاجر بعد العمولة (قسم 7).</summary>
public class Settlement : BaseEntity
{
    public Guid SubOrderId { get; set; }
    public SubOrder? SubOrder { get; set; }

    public Guid MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public Guid? DriverId { get; set; }     // السائق الذي حصّل
    public Driver? Driver { get; set; }

    public decimal CollectedAmount { get; set; }   // ما حُصّل فعلاً عند الباب
    public decimal CommissionAmount { get; set; }
    public decimal MerchantPayout { get; set; }     // المستحق للتاجر

    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    public DateTimeOffset? PaidAt { get; set; }
}

/// <summary>إعداد العمولة (تحدّدها الإدارة). يدعم نسبة عامة + استثناء لكل تاجر.</summary>
public class CommissionConfig : BaseEntity
{
    /// <summary>النسبة الافتراضية (مثلاً 0.10 = 10%).</summary>
    public decimal DefaultRate { get; set; } = 0.10m;

    /// <summary>استثناء لتاجر محدّد (null = استخدم الافتراضي).</summary>
    public Guid? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    public decimal? MerchantRate { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>إعداد الموجات: مدة الموجة، فجوة التوزيع، T_prep و T_transit الافتراضيان (قسم 6/13).</summary>
public class WaveConfig : BaseEntity
{
    /// <summary>مدة الموجة بالدقائق (كل ساعة افتراضياً — قرار 6).</summary>
    public int WaveIntervalMinutes { get; set; } = 60;

    /// <summary>فجوة بين موجة الجمع وموجة التوزيع التالية (دقائق).</summary>
    public int DistributionGapMinutes { get; set; } = 60;

    /// <summary>وقت تحضير التاجر الافتراضي (دقائق).</summary>
    public int DefaultPrepMinutes { get; set; } = 30;

    /// <summary>زمن التوصيل المتوسط المبدئي من المركز للزبون (دقائق) — يُستبدل بالمسار.</summary>
    public int DefaultTransitMinutes { get; set; } = 40;

    /// <summary>الحد الأقصى لعدد زبائن السائق في مهمة التوزيع (قرار 7).</summary>
    public int MaxCustomersPerDriver { get; set; } = 15;

    public bool IsActive { get; set; } = true;
}
