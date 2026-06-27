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
    public decimal? ExpectedPrice { get; set; }          // العادي
    public decimal? ExpectedSpecialPrice { get; set; }   // العرض (إن وُجد)
    public bool HasOffer { get; set; }

    public string DistanceText => DistanceKm is null ? ""
        : DistanceKm < 1 ? $"يبعد {DistanceKm * 1000:0} م" : $"يبعد {DistanceKm:0.0} كم";
    public string ExpectedPriceText => ExpectedPrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "—";
    public string SpecialPriceText => ExpectedSpecialPrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "—";
    /// <summary>السعر الفعّال المتوقّع: العرض إن وُجد، وإلّا العادي.</summary>
    public decimal? EffectivePrice => HasOffer ? ExpectedSpecialPrice : ExpectedPrice;
    public string EffectivePriceText => EffectivePrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";
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
    public decimal? ExpectedSpecialPrice { get; set; }
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

/// <summary>لقطة حالة قاعدة البيانات (تبويب "القاعدة").</summary>
public class ValidationStatsDto
{
    public int Products { get; set; }
    public int Merchants { get; set; }
    public int PricedItems { get; set; }
    public int Offers { get; set; }
    public DateTimeOffset? LastPriceUpdate { get; set; }
    public int UpdatedLast24h { get; set; }
    public int Validations { get; set; }
    public int Mismatches { get; set; }
    public DateTimeOffset? LastValidation { get; set; }

    // مشتقّات العرض.
    public string ProductsText  => Products.ToString("N0", CultureInfo.InvariantCulture);
    public string MerchantsText => Merchants.ToString("N0", CultureInfo.InvariantCulture);
    public string PricedText    => PricedItems.ToString("N0", CultureInfo.InvariantCulture);
    public string OffersText    => Offers.ToString("N0", CultureInfo.InvariantCulture);
    public string ValidationsText => Validations.ToString("N0", CultureInfo.InvariantCulture);
    public string MismatchesText  => Mismatches.ToString("N0", CultureInfo.InvariantCulture);

    public string LastUpdateRelative => Relative(LastPriceUpdate);
    public string LastUpdateAbsolute => Absolute(LastPriceUpdate);
    public string LastValidationRelative => Relative(LastValidation);

    /// <summary>هل حدثت تغييرات في آخر 24 ساعة؟ (مؤشّر حداثة البيانات).</summary>
    public bool HasRecentChanges => UpdatedLast24h > 0;
    public string ChangesText => HasRecentChanges
        ? $"نعم — {UpdatedLast24h.ToString("N0", CultureInfo.InvariantCulture)} صنف تحدّث في آخر 24 ساعة"
        : "لا تغييرات في آخر 24 ساعة";

    private static string Relative(DateTimeOffset? when)
    {
        if (when is null) return "—";
        var d = DateTimeOffset.UtcNow - when.Value;
        if (d.TotalSeconds < 60) return "الآن";
        if (d.TotalMinutes < 60) return $"منذ {(int)d.TotalMinutes} دقيقة";
        if (d.TotalHours   < 24) return $"منذ {(int)d.TotalHours} ساعة";
        if (d.TotalDays    < 30) return $"منذ {(int)d.TotalDays} يوم";
        return when.Value.ToLocalTime().ToString("yyyy-MM-dd");
    }

    private static string Absolute(DateTimeOffset? when)
        => when?.ToLocalTime().ToString("yyyy-MM-dd  HH:mm") ?? "—";
}

/// <summary>صفّ في سجلّ تحقّقات فرع.</summary>
public class ValidationHistoryDto
{
    public Guid Id { get; set; }
    public string Barcode { get; set; } = "";
    public string? ProductName { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public decimal? ExpectedSpecialPrice { get; set; }
    public decimal ActualPrice { get; set; }
    public bool IsMatch { get; set; }
    public string? Auditor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // مشتقّات العرض.
    public string Title => string.IsNullOrWhiteSpace(ProductName) ? Barcode : ProductName!;
    public string ExpectedText
    {
        get
        {
            if (ExpectedPrice is null) return "بلا سعر مخزّن";
            var reg = ExpectedPrice.Value.ToString("0.00", CultureInfo.InvariantCulture);
            return ExpectedSpecialPrice is { } sp
                ? $"سعرنا {reg} (عرض {sp.ToString("0.00", CultureInfo.InvariantCulture)})"
                : $"سعرنا {reg}";
        }
    }
    public string ActualText => $"الواقع {ActualPrice.ToString("0.00", CultureInfo.InvariantCulture)}";
    public string StatusText => IsMatch ? "مطابق" : "مختلف";
    public string StatusGlyph => IsMatch ? "✓" : "✕";
    public Color StatusColor => IsMatch ? Color.FromArgb("#1D9E75") : Color.FromArgb("#D9534F");
    public string WhenText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd  HH:mm");
}
