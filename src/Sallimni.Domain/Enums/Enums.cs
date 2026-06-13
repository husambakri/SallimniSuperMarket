namespace Sallimni.Domain.Enums;

/// <summary>شرائح ضريبة المبيعات الرسمية (ISTD). لا توجد شريحة 8%.</summary>
public enum TaxClass
{
    Exempt = -1, // معفى
    Zero = 0,    // 0%
    Two = 2,     // 2%
    Four = 4,    // 4%
    Five = 5,    // 5%
    Ten = 10,    // 10%
    Sixteen = 16 // 16% (الافتراضي لما تبقّى)
}

/// <summary>حالة طلب إضافة صنف جديد من التاجر.</summary>
public enum SubmissionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>دورة حياة الطلب الأب — مطابقة للقسم 10 من الـ PRD.</summary>
public enum OrderStatus
{
    Cart = 0,
    Placed = 1,
    Split = 2,
    CollectionAssigned = 3,
    Picked = 4,
    CrossDocked = 5,
    DistributionAssigned = 6,
    OutForDelivery = 7,
    DeliveredAndPaid = 8,
    Settled = 9,
    Cancelled = 100
}

/// <summary>حالة الطلب الفرعي لكل تاجر.</summary>
public enum SubOrderStatus
{
    Pending = 0,
    Preparing = 1,
    PickedUp = 2,      // استلمها السائق من المتجر
    AtHub = 3,         // وصلت المستودع وفُرزت
    OutForDelivery = 4,
    Delivered = 5,
    Settled = 6,
    Cancelled = 100
}

/// <summary>حالة صنف داخل الطلب — يدعم الرفض عند الباب والبدائل عند الفرز.</summary>
public enum OrderItemStatus
{
    Active = 0,
    OutOfStockReassigned = 1, // أُعيد إسناده لتاجر آخر بسبب النفاد
    MissingAtSort = 2,        // ناقص عند الفرز — بانتظار قرار الزبون
    RejectedAtDoor = 3,       // رُفض عند الباب فلا يُحصَّل
    Cancelled = 4
}

/// <summary>نوع مهمة التوصيل.</summary>
public enum TaskType
{
    Collection = 0,   // متاجر ← مستودع
    Distribution = 1  // مستودع ← زبائن
}

public enum TaskStatus
{
    Created = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 100
}

/// <summary>حالة الموجة اللوجستية.</summary>
public enum WaveStatus
{
    Open = 0,        // تقبل طلبات جديدة
    Collecting = 1,  // مرحلة الجمع جارية
    Sorting = 2,     // الفرز المركزي
    Distributing = 3,// التوزيع جارٍ
    Closed = 4
}

/// <summary>مراحل سلسلة العهدة لمسح QR.</summary>
public enum ScanStage
{
    MerchantToDriver = 0, // متجر → سائق (استلام أول ميل)
    DriverToHub = 1,      // سائق → مستودع (إدخال)
    HubToDriver = 2,      // مستودع → سائق (تحميل توزيع)
    DriverToCustomer = 3  // سائق → زبون (تسليم)
}

/// <summary>طريقة الدفع عند الاستلام.</summary>
public enum PaymentMethod
{
    Cash = 0,
    CliQ = 1
}

public enum SettlementStatus
{
    Pending = 0,
    Paid = 1
}

/// <summary>نتيجة فحص الباركود.</summary>
public enum BarcodeScanResult
{
    Found = 0,
    NotFound = 1
}
