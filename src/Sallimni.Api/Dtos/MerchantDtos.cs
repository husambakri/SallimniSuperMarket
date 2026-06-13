using Sallimni.Domain.Enums;

namespace Sallimni.Api.Dtos;

public record MerchantCatalogRowDto(Guid ProductId, string NameAr, string NameEn, string? Barcode,
    string? UnitSize, TaxClass TaxClass, bool IsLinked, decimal? Price, int StockQty, bool IsAvailable);

public record UpsertMerchantProductRequest(decimal Price, int StockQty, bool IsAvailable);

public record MerchantSubOrderItemDto(string Name, int Quantity, decimal UnitPriceInclTax);

public record MerchantSubOrderDto(Guid SubOrderId, Guid OrderId, int Status, decimal SubtotalInclTax,
    decimal MerchantPayout, DateTimeOffset CreatedAt, List<MerchantSubOrderItemDto> Items);

public record UpdateSubOrderStatusRequest(int Status);

public record CreateSubmissionRequest(string NameAr, string NameEn, string? Barcode,
    string? UnitSize, TaxClass SuggestedTaxClass);

public record SubmissionDto(Guid Id, string NameAr, string NameEn, string? Barcode,
    string? UnitSize, TaxClass SuggestedTaxClass, int Status, DateTimeOffset CreatedAt);

public record MerchantInfoDto(Guid Id, string Name, bool IsSalesTaxRegistered);
