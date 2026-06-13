using Sallimni.Domain.Common;
using Sallimni.Domain.Enums;

namespace Sallimni.Domain.Entities;

/// <summary>الطلب الأب — سلّة موحّدة تُقسَّم لطلبات فرعية لكل تاجر فائز (قسم 4).</summary>
public class Order : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Cart;

    /// <summary>موقع التوصيل المثبّت لحظة الطلب (نسخة من العنوان + pin اختياري).</summary>
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }
    public double DeliveryLatitude { get; set; }
    public double DeliveryLongitude { get; set; }

    public Guid? WaveId { get; set; }
    public Wave? Wave { get; set; }

    /// <summary>الإجماليات شاملة الضريبة (مجموع الطلبات الفرعية).</summary>
    public decimal SubtotalInclTax { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>وقت التسليم المتوقّع (ETA) — يُحسب عند الطلب ويُحدَّث بالمسار.</summary>
    public DateTimeOffset? EstimatedDeliveryAt { get; set; }
    public DateTimeOffset? PlacedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }

    public ICollection<SubOrder> SubOrders { get; set; } = new List<SubOrder>();
}

/// <summary>طلب فرعي لتاجر واحد، مع تثبيت لقطة السعر لحظة التأكيد.</summary>
public class SubOrder : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public SubOrderStatus Status { get; set; } = SubOrderStatus.Pending;

    /// <summary>إجماليات الطلب الفرعي (شاملة الضريبة) — أساس احتساب العمولة.</summary>
    public decimal SubtotalInclTax { get; set; }
    public decimal TaxTotal { get; set; }

    /// <summary>العمولة المحتسبة على الإجمالي شامل الضريبة (قرار 2).</summary>
    public decimal CommissionRate { get; set; }   // النسبة المثبّتة لحظة الطلب
    public decimal CommissionAmount { get; set; }
    /// <summary>صافي ما يُدفع للتاجر = الإجمالي - العمولة.</summary>
    public decimal MerchantPayout { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

/// <summary>صنف داخل طلب فرعي — يحمل لقطة السعر (PriceSnapshot) لحظة التأكيد.</summary>
public class OrderItem : BaseEntity
{
    public Guid SubOrderId { get; set; }
    public SubOrder? SubOrder { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>لقطة السعر شامل الضريبة لحظة التأكيد (لا تتغيّر بعدها).</summary>
    public decimal UnitPriceInclTax { get; set; }
    public TaxClass TaxClass { get; set; }
    /// <summary>قيمة الضريبة لكل وحدة (مشتقّة من السعر الشامل والشريحة).</summary>
    public decimal UnitTaxAmount { get; set; }

    public OrderItemStatus Status { get; set; } = OrderItemStatus.Active;

    public decimal LineTotalInclTax => UnitPriceInclTax * Quantity;
}
