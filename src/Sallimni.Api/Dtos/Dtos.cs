using Sallimni.Domain.Enums;

namespace Sallimni.Api.Dtos;

// ===== الكتالوج =====
public record CatalogCategoryDto(Guid Id, string NameAr, string NameEn, string? Icon, string? ImageUrl, int ProductCount);

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

// ===== تحقّق الأسعار الميداني (validation) =====

/// <summary>متجر في قائمة الاختيار (تُثبَّت في ترويسة المسح).</summary>
public record ValidationMerchantDto(Guid Id, string Name);

/// <summary>
/// نتيجة استعلام المسح: الفرع المختار + سعرنا المخزّن للباركود (العادي + العرض إن وُجد).
/// </summary>
public record ValidationLookupDto(
    bool BranchFound, Guid? MerchantId, string? MerchantName, string? BranchId, double? DistanceKm,
    bool ProductFound, Guid? ProductId, string? ProductName,
    bool HasOurPrice, decimal? ExpectedPrice, decimal? ExpectedSpecialPrice, bool HasOffer);

/// <summary>طلب تسجيل تحقّق (يضيف صفّاً تاريخياً append-only؛ لا يلمس السعر الحيّ).</summary>
public record ValidationRecordRequest(
    Guid MerchantId, string MerchantName, string? BranchId,
    Guid? ProductId, string Barcode, string? ProductName,
    decimal? ExpectedPrice, decimal? ExpectedSpecialPrice, decimal ActualPrice,
    double? Latitude, double? Longitude, string? Auditor);

/// <summary>فرع ظهر في سجلّ التحقّق (لمنتقي صفحة السجلّ).</summary>
public record ValidationBranchDto(Guid MerchantId, string MerchantName, int Count, DateTimeOffset LastAt);

/// <summary>صفّ في سجلّ تحقّقات فرع.</summary>
public record ValidationHistoryDto(
    Guid Id, string Barcode, string? ProductName,
    decimal? ExpectedPrice, decimal? ExpectedSpecialPrice, decimal ActualPrice, bool IsMatch,
    string? Auditor, DateTimeOffset CreatedAt);
