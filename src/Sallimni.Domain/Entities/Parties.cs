using Sallimni.Domain.Common;

namespace Sallimni.Domain.Entities;

/// <summary>التاجر (السوبر ماركت).</summary>
public class Merchant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>إحداثيات المتجر (ضمن دائرة التجار) — لمسار التجميع.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AddressText { get; set; }

    /// <summary>مسجّل بضريبة المبيعات؟ (قسم 8.2).</summary>
    public bool IsSalesTaxRegistered { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<MerchantProduct> MerchantProducts { get; set; } = new List<MerchantProduct>();
    public ICollection<SubOrder> SubOrders { get; set; } = new List<SubOrder>();
}

/// <summary>السائق (المندوب).</summary>
public class Driver : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>آخر موقع معروف (تتبّع foreground/background).</summary>
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTimeOffset? LastLocationAt { get; set; }
}

/// <summary>الزبون.</summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>عنوان محفوظ للزبون + إحداثيات (قرار 1). يمكن تثبيت pin لكل طلب.</summary>
public class Address : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Label { get; set; } = string.Empty; // البيت/العمل...
    public string Line { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsDefault { get; set; }
}
