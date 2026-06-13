using Sallimni.Domain.Enums;

namespace Sallimni.Api.Dtos;

// ===== الكتالوج =====
public record CatalogCategoryDto(Guid Id, string NameAr, string NameEn, string? Icon, int ProductCount);

public record ProductDto(Guid Id, string NameAr, string NameEn, string? Barcode,
    string? UnitSize, string? ImageUrl, string? Emoji, TaxClass TaxClass, Guid CategoryId,
    decimal? CheapestPriceInclTax, decimal? RegularPriceInclTax, int SavingsPercent);

public record ProductDetailDto(Guid Id, string NameAr, string NameEn, string? Barcode,
    string? UnitSize, string? ImageUrl, string? Emoji, string? Description, TaxClass TaxClass,
    Guid CategoryId, string CategoryNameAr,
    decimal? CheapestPriceInclTax, decimal? RegularPriceInclTax, int SavingsPercent);

public record BarcodeLookupDto(bool Found, Guid? ProductId, string? NameAr, string? NameEn,
    string? ImageUrl, string? Emoji, decimal? OurPriceInclTax, decimal? RegularPriceInclTax, int SavingsPercent);

// ===== الطلبات =====
public record CartLineDto(Guid ProductId, int Quantity);

public record PlaceOrderRequest(Guid CustomerId, Guid AddressId,
    List<CartLineDto> Items, PaymentMethod PaymentMethod = PaymentMethod.Cash);

public record OrderItemDto(Guid ProductId, string Name, int Quantity,
    decimal UnitPriceInclTax, decimal UnitTaxAmount, TaxClass TaxClass, OrderItemStatus Status);

public record SubOrderDto(Guid Id, Guid MerchantId, string MerchantName,
    decimal SubtotalInclTax, decimal TaxTotal, decimal CommissionRate,
    decimal CommissionAmount, decimal MerchantPayout, List<OrderItemDto> Items);

public record OrderDto(Guid Id, OrderStatus Status, decimal SubtotalInclTax,
    decimal TaxTotal, decimal GrandTotal, DateTimeOffset? EstimatedDeliveryAt,
    Guid? WaveId, List<SubOrderDto> SubOrders, List<UnfulfilledDto> Unfulfilled);

public record UnfulfilledDto(Guid ProductId, int Quantity, string Reason);

// ===== الإدارة =====
public record CommissionConfigDto(decimal DefaultRate);
