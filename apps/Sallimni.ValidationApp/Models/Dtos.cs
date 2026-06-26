using System.Globalization;

namespace Sallimni.ValidationApp.Models;

// نماذج مطابقة لنقاط /api/validation في الخادم.

/// <summary>متجر في قائمة الاختيار (ترويسة المسح).</summary>
public class ValidationMerchantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

/// <summary>نتيجة استعلام المسح: أقرب فرع للموقع + سعرنا المخزّن للباركود فيه.</summary>
public class ValidationLookupDto
{
    public bool BranchFound { get; set; }
    public Guid? MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public string? BranchId { get; set; }
    public double? DistanceKm { get; set; }

    public bool ProductFound { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }

    public bool HasOurPrice { get; set; }
    public decimal? ExpectedPrice { get; set; }

    public string DistanceText => DistanceKm is null ? ""
        : DistanceKm < 1 ? $"يبعد {DistanceKm * 1000:0} م" : $"يبعد {DistanceKm:0.0} كم";
    public string ExpectedPriceText => ExpectedPrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "—";
}

/// <summary>طلب تسجيل تحقّق (يضيف صفّاً تاريخياً؛ لا يلمس السعر الحيّ).</summary>
public class ValidationRecordRequest
{
    public Guid MerchantId { get; set; }
    public string MerchantName { get; set; } = "";
    public string? BranchId { get; set; }
    public Guid? ProductId { get; set; }
    public string Barcode { get; set; } = "";
    public string? ProductName { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public decimal ActualPrice { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Auditor { get; set; }
}

/// <summary>فرع ظهر في سجلّ التحقّق (لمنتقي صفحة السجلّ).</summary>
public class ValidationBranchDto
{
    public Guid MerchantId { get; set; }
    public string MerchantName { get; set; } = "";
    public int Count { get; set; }
    public DateTimeOffset LastAt { get; set; }

    public string Display => $"{MerchantName}  ({Count})";
}

/// <summary>صفّ في سجلّ تحقّقات فرع.</summary>
public class ValidationHistoryDto
{
    public Guid Id { get; set; }
    public string Barcode { get; set; } = "";
    public string? ProductName { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public decimal ActualPrice { get; set; }
    public bool IsMatch { get; set; }
    public string? Auditor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // مشتقّات العرض.
    public string Title => string.IsNullOrWhiteSpace(ProductName) ? Barcode : ProductName!;
    public string ExpectedText => ExpectedPrice is null ? "بلا سعر مخزّن"
        : $"سعرنا {ExpectedPrice.Value.ToString("0.00", CultureInfo.InvariantCulture)}";
    public string ActualText => $"الواقع {ActualPrice.ToString("0.00", CultureInfo.InvariantCulture)}";
    public string StatusText => IsMatch ? "مطابق" : "مختلف";
    public string StatusGlyph => IsMatch ? "✓" : "✕";
    public Color StatusColor => IsMatch ? Color.FromArgb("#1D9E75") : Color.FromArgb("#D9534F");
    public string WhenText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd  HH:mm");
}
