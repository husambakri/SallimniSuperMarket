using Sallimni.Domain.Enums;

namespace Sallimni.Application.Models;

/// <summary>سطر في سلّة الزبون.</summary>
public record CartLine(Guid ProductId, int Quantity);

/// <summary>معلومات بطاقة الصنف اللازمة للتقسيم.</summary>
public record ProductInfo(Guid ProductId, string Name, TaxClass TaxClass);

/// <summary>عرض تاجر لصنف: السعر شامل الضريبة + المخزون.</summary>
public record MerchantOffer(
    Guid MerchantId,
    Guid ProductId,
    decimal PriceInclTax,
    int StockQty,
    bool IsAvailable);

/// <summary>صنف بعد إسناده لتاجر فائز.</summary>
public record SplitItem(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPriceInclTax,
    TaxClass TaxClass,
    decimal UnitTaxAmount,
    bool ReassignedDueToStock);

/// <summary>طلب فرعي ناتج عن التقسيم لتاجر واحد.</summary>
public class SplitSubOrder
{
    public Guid MerchantId { get; init; }
    public List<SplitItem> Items { get; } = new();

    public decimal SubtotalInclTax => Items.Sum(i => i.UnitPriceInclTax * i.Quantity);
    public decimal TaxTotal => Items.Sum(i => i.UnitTaxAmount * i.Quantity);

    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal MerchantPayout { get; set; }
}

/// <summary>أصناف تعذّر إسنادها (لا تاجر متوفر).</summary>
public record UnfulfilledLine(Guid ProductId, int Quantity, string Reason);

/// <summary>نتيجة محرّك التقسيم.</summary>
public class SplitResult
{
    public List<SplitSubOrder> SubOrders { get; } = new();
    public List<UnfulfilledLine> Unfulfilled { get; } = new();

    public decimal SubtotalInclTax => SubOrders.Sum(s => s.SubtotalInclTax);
    public decimal TaxTotal => SubOrders.Sum(s => s.TaxTotal);
    public decimal GrandTotal => SubtotalInclTax;
}
