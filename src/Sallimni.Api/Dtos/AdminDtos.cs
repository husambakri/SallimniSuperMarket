using Sallimni.Domain.Enums;

namespace Sallimni.Api.Dtos;

public record CategoryDto(Guid Id, string NameAr, string NameEn);
public record CreateCategoryRequest(string NameAr, string NameEn);

public record CreateProductRequest(string NameAr, string NameEn, string? Barcode,
    string? UnitSize, Guid CategoryId, TaxClass TaxClass);

public record AdminSubmissionDto(Guid Id, Guid MerchantId, string NameAr, string NameEn,
    string? Barcode, string? UnitSize, TaxClass SuggestedTaxClass, DateTimeOffset CreatedAt);

public record ApproveSubmissionRequest(Guid CategoryId);
public record RejectSubmissionRequest(string? Note);

public record WaveSummaryDto(Guid WaveId, int Status, DateTimeOffset CollectionStartAt,
    DateTimeOffset DistributionStartAt, int OrderCount, int SubOrderCount,
    bool HasCollectionTask, bool HasDistributionTask);

public record TaskStopDto(int Sequence, string Label, double Latitude, double Longitude, bool IsCompleted);
public record TaskDto(Guid TaskId, int Type, int Status, Guid WaveId, Guid? DriverId,
    string? DriverName, List<TaskStopDto> Stops);

public record AssignDriverRequest(Guid DriverId);
public record DriverDto(Guid Id, string Name, string? Phone);

public record CommissionConfigDto2(decimal DefaultRate);
public record UpdateCommissionRequest(decimal DefaultRate);

public record WaveConfigDto(int WaveIntervalMinutes, int DistributionGapMinutes,
    int DefaultPrepMinutes, int DefaultTransitMinutes, int MaxCustomersPerDriver);

public record SettlementRowDto(Guid SubOrderId, Guid MerchantId, string MerchantName,
    int Status, decimal SubtotalInclTax, decimal CommissionAmount, decimal MerchantPayout);
