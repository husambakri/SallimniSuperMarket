namespace Sallimni.AdminApp.Models;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public override string ToString() => NameAr;
}

public class AdminProductDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public string? Emoji { get; set; }
    public string? ImageUrl { get; set; }
    public int TaxClass { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryNameAr { get; set; } = "";
    public int MerchantCount { get; set; }
    public bool IsActive { get; set; }
    public string Glyph => string.IsNullOrEmpty(Emoji) ? "🛒" : Emoji!;

    /// <summary>الرابط المطلق للصورة (يُحقن من قاعدة عنوان الخادم)؛ فارغ إن لا صورة.</summary>
    public string? FullImageUrl { get; set; }
    public bool HasImage => !string.IsNullOrEmpty(FullImageUrl);
}

public class CreateProductRequest
{
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public string? Emoji { get; set; }
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public int TaxClass { get; set; } = 16;
}

public class CreateCategoryRequest
{
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Icon { get; set; }
}

public class AdminSubmissionDto
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public int SuggestedTaxClass { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class WaveSummaryDto
{
    public Guid WaveId { get; set; }
    public int Status { get; set; }
    public DateTimeOffset CollectionStartAt { get; set; }
    public DateTimeOffset DistributionStartAt { get; set; }
    public int OrderCount { get; set; }
    public int SubOrderCount { get; set; }
    public bool HasCollectionTask { get; set; }
    public bool HasDistributionTask { get; set; }
}

public class TaskStopDto
{
    public int Sequence { get; set; }
    public string Label { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsCompleted { get; set; }
}

public class TaskDto
{
    public Guid TaskId { get; set; }
    public int Type { get; set; }   // 0=Collection, 1=Distribution
    public int Status { get; set; }
    public Guid WaveId { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public List<TaskStopDto> Stops { get; set; } = new();
}

public class DriverDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public override string ToString() => Name;
}

public class SettlementRowDto
{
    public Guid SubOrderId { get; set; }
    public Guid MerchantId { get; set; }
    public string MerchantName { get; set; } = "";
    public int Status { get; set; }
    public decimal SubtotalInclTax { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal MerchantPayout { get; set; }
}

public class CommissionConfigDto
{
    public decimal DefaultRate { get; set; }
}

public class WaveConfigDto
{
    public int WaveIntervalMinutes { get; set; }
    public int DistributionGapMinutes { get; set; }
    public int DefaultPrepMinutes { get; set; }
    public int DefaultTransitMinutes { get; set; }
    public int MaxCustomersPerDriver { get; set; }
}
