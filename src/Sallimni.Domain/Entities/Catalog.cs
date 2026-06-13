using Sallimni.Domain.Common;
using Sallimni.Domain.Enums;

namespace Sallimni.Domain.Entities;

/// <summary>تصنيف الأصناف (تملكه الإدارة).</summary>
public class Category : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }

    /// <summary>أيقونة العرض (رمز تعبيري) لشبكة الأصناف — بديل عند غياب صورة.</summary>
    public string? Icon { get; set; }
    /// <summary>رابط صورة التصنيف العام (عند رفع صورة).</summary>
    public string? ImageUrl { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>
/// البطاقة الرئيسية للصنف — تملكها الإدارة حصراً (قسم 3).
/// التاجر لا يعدّلها؛ يربط بها سعره وكميته عبر MerchantProduct.
/// </summary>
public class Product : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }   // EAN-13/EAN-8/UPC-A/UPC-E — مفتاح فحص السعر
    public string? UnitSize { get; set; }  // الوحدة/الحجم
    /// <summary>رابط الصورة العام (يشير إلى نقطة تقديم الصورة عند رفعها).</summary>
    public string? ImageUrl { get; set; }
    /// <summary>بيانات الصورة المخزّنة في القاعدة (bytea) — مكتفية ذاتياً دون خدمة تخزين خارجية.</summary>
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    /// <summary>رمز تعبيري بديل عند غياب صورة حقيقية.</summary>
    public string? Emoji { get; set; }
    /// <summary>وصف الصنف (يظهر في صفحة التفاصيل).</summary>
    public string? Description { get; set; }
    public TaxClass TaxClass { get; set; } = TaxClass.Sixteen;

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<MerchantProduct> MerchantProducts { get; set; } = new List<MerchantProduct>();
}

/// <summary>ربط مخزون التاجر وسعره ببطاقة صنف. السعر والكمية يملكهما التاجر.</summary>
public class MerchantProduct : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>السعر شامل الضريبة (العرض للزبون شامل الضريبة — قرار 3).</summary>
    public decimal Price { get; set; }
    public int StockQty { get; set; }
    public bool IsAvailable { get; set; } = true;
}

/// <summary>طلب تاجر لإضافة صنف جديد — يدخل طابور اعتماد الإدارة (قسم 3).</summary>
public class ProductSubmission : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? UnitSize { get; set; }
    public Guid? SuggestedCategoryId { get; set; }
    public TaxClass SuggestedTaxClass { get; set; } = TaxClass.Sixteen;

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public string? ReviewNote { get; set; }
    public Guid? ApprovedProductId { get; set; } // الصنف الناتج عند الاعتماد
}

/// <summary>أصناف يملكها المستودع (مخزون صغير محصور — قسم 9). مصدر دخل بهامش ربح.</summary>
public class HubProduct : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Cost { get; set; }      // كلفة الشراء
    public decimal Price { get; set; }     // سعر البيع شامل الضريبة
    public int StockQty { get; set; }
    public bool IsAvailable { get; set; } = true;
}

/// <summary>سجل اختياري لعمليات فحص الباركود — لقياس فاعلية الخطّاف (قسم 4.1).</summary>
public class BarcodeScan : BaseEntity
{
    public string Barcode { get; set; } = string.Empty;
    public BarcodeScanResult Result { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CustomerId { get; set; }
}
