namespace Sallimni.MerchantApp.Models;

public class MerchantInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsSalesTaxRegistered { get; set; }
}

public class MerchantCatalogRowDto
{
    public Guid ProductId { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public int TaxClass { get; set; }
    public bool IsLinked { get; set; }
    public decimal? Price { get; set; }
    public int StockQty { get; set; }
    public bool IsAvailable { get; set; }
}

public class UpsertMerchantProductRequest
{
    public decimal Price { get; set; }
    public int StockQty { get; set; }
    public bool IsAvailable { get; set; }
}

public class MerchantSubOrderItemDto
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPriceInclTax { get; set; }
}

public class MerchantSubOrderDto
{
    public Guid SubOrderId { get; set; }
    public Guid OrderId { get; set; }
    public int Status { get; set; }
    public decimal SubtotalInclTax { get; set; }
    public decimal MerchantPayout { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<MerchantSubOrderItemDto> Items { get; set; } = new();
}

public class CreateSubmissionRequest
{
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public int SuggestedTaxClass { get; set; } = 16;
}

public class SubmissionDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public int SuggestedTaxClass { get; set; }
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
