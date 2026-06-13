using Sallimni.Domain.Common;
using Sallimni.Domain.Enums;

namespace Sallimni.Domain.Entities;

/// <summary>الموجة اللوجستية: جدولة جمع وتوزيع لدفعة طلبات (قسم 5).</summary>
public class Wave : BaseEntity
{
    public WaveStatus Status { get; set; } = WaveStatus.Open;

    /// <summary>موعد موجة الجمع (ساعة رأسية).</summary>
    public DateTimeOffset CollectionStartAt { get; set; }
    /// <summary>موعد موجة التوزيع التالية للجمع.</summary>
    public DateTimeOffset DistributionStartAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<DeliveryTask> Tasks { get; set; } = new List<DeliveryTask>();
}

/// <summary>مهمة توصيل — تجميع (متاجر←مستودع) أو توزيع (مستودع←زبائن) (قسم 5.2).</summary>
public class DeliveryTask : BaseEntity
{
    public TaskType Type { get; set; }
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Created;

    public Guid WaveId { get; set; }
    public Wave? Wave { get; set; }

    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}

/// <summary>محطة في مسار مهمة — متجر (تجميع) أو زبون (توزيع)، مع ETA وترتيب.</summary>
public class RouteStop : BaseEntity
{
    public Guid TaskId { get; set; }
    public DeliveryTask? Task { get; set; }

    public int Sequence { get; set; } // ترتيب الزيارة بعد تحسين المسار

    // وجهة المحطة: إمّا متجر (تجميع) أو طلب أب لزبون (توزيع)
    public Guid? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    public Guid? SubOrderId { get; set; }   // الطلب الفرعي المستلَم في التجميع
    public SubOrder? SubOrder { get; set; }
    public Guid? OrderId { get; set; }       // الطلب المُسلَّم في التوزيع
    public Order? Order { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public DateTimeOffset? EstimatedArrivalAt { get; set; }
    public DateTimeOffset? ActualArrivalAt { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>حدث مسح QR (سلسلة العهدة) في كل تسليم/استلام (قسم 5.3).</summary>
public class ScanEvent : BaseEntity
{
    public ScanStage Stage { get; set; }
    public string QrCode { get; set; } = string.Empty;

    public Guid? SubOrderId { get; set; }
    public SubOrder? SubOrder { get; set; }
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public Guid? TaskId { get; set; }

    public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.UtcNow;
}
