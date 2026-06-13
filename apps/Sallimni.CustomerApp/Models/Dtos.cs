using System.Text.Json.Serialization;

namespace Sallimni.CustomerApp.Models;

// نماذج مطابقة لاستجابات الخادم (Sallimni.Api).

public class CategoryDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Icon { get; set; }
    public int ProductCount { get; set; }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public string? ImageUrl { get; set; }
    public string? Emoji { get; set; }
    public int TaxClass { get; set; }
    public Guid CategoryId { get; set; }
    public decimal? CheapestPriceInclTax { get; set; }
    public decimal? RegularPriceInclTax { get; set; }
    public int SavingsPercent { get; set; }

    // مشتقّات للعرض
    public bool HasSavings => SavingsPercent > 0;
    public string SavingsText => $"وفّر {SavingsPercent}%";
    public string Glyph => string.IsNullOrEmpty(Emoji) ? "🛒" : Emoji!;

    /// <summary>الرابط المطلق للصورة (يُحقن من قاعدة عنوان الخادم)؛ فارغ إن لا صورة.</summary>
    public string? FullImageUrl { get; set; }
    public bool HasImage => !string.IsNullOrEmpty(FullImageUrl);
}

public class ProductDetailDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public string? ImageUrl { get; set; }
    public string? Emoji { get; set; }
    public string? Description { get; set; }
    public int TaxClass { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryNameAr { get; set; } = "";
    public decimal? CheapestPriceInclTax { get; set; }
    public decimal? RegularPriceInclTax { get; set; }
    public int SavingsPercent { get; set; }

    public string Glyph => string.IsNullOrEmpty(Emoji) ? "🛒" : Emoji!;
    public string? FullImageUrl { get; set; }
    public bool HasImage => !string.IsNullOrEmpty(FullImageUrl);
}

public class BarcodeLookupDto
{
    public bool Found { get; set; }
    public Guid? ProductId { get; set; }
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public string? ImageUrl { get; set; }
    public string? Emoji { get; set; }
    public decimal? OurPriceInclTax { get; set; }
    public decimal? RegularPriceInclTax { get; set; }
    public int SavingsPercent { get; set; }
}

public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<AddressDto> Addresses { get; set; } = new();
}

public class AddressDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public bool IsDefault { get; set; }
}

public class CartLineDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class PlaceOrderRequest
{
    public Guid CustomerId { get; set; }
    public Guid AddressId { get; set; }
    public List<CartLineDto> Items { get; set; } = new();
    public int PaymentMethod { get; set; } // 0=Cash, 1=CliQ
}

public class OrderItemDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPriceInclTax { get; set; }
    public decimal UnitTaxAmount { get; set; }
    public int TaxClass { get; set; }
    public int Status { get; set; }
}

public class SubOrderDto
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string MerchantName { get; set; } = "";
    public decimal SubtotalInclTax { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal MerchantPayout { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class UnfulfilledDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = "";
}

public class OrderDto
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public decimal SubtotalInclTax { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTimeOffset? EstimatedDeliveryAt { get; set; }
    public Guid? WaveId { get; set; }
    public List<SubOrderDto> SubOrders { get; set; } = new();
    public List<UnfulfilledDto> Unfulfilled { get; set; } = new();
}
