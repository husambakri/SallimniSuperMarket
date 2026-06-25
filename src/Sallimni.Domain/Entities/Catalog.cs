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

/// <summary>
/// فهرس أسعار طلبات: صورة دوريّة لكتالوجات متاجر طلبات الأردنية (مقارنة السعر).
/// تملؤها مهمّة خلفية كل بضع ساعات (مسح + استخراج باركود من sku)، ويقرأها فحص
/// السعر الحيّ فورًا بالباركود دون ضرب طلبات وقت الطلب.
/// </summary>
public class TalabatPriceEntry : BaseEntity
{
    public string BranchId   { get; set; } = string.Empty;
    public string StoreName  { get; set; } = string.Empty;
    public string Barcode    { get; set; } = string.Empty;
    public string Name       { get; set; } = string.Empty;
    public decimal Price     { get; set; }   // السعر الأصلي
    public decimal Special   { get; set; }   // سعر التخفيض (0 = لا يوجد)
    public bool InStock      { get; set; }
    public string ImageUrl   { get; set; } = string.Empty;
    public string ProductUrl { get; set; } = string.Empty;
    /// <summary>إحداثيات فرع المتجر (لحساب المسافة لأقرب متجر) — null إن لم تتوفّر.</summary>
    public double? Latitude  { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// دليل فروع المتاجر (مواقع فقط، بلا كتالوج). فهرس الأسعار يوحّد المتجر بفرع واحد،
/// وهذا الدليل يحتفظ بكل الفروع لإظهار أقرب فرع لنفس المتجر عند المسح.
/// </summary>
public class StoreBranch : BaseEntity
{
    public string StoreNameNorm { get; set; } = string.Empty; // اسم موحّد للمطابقة مع الفهرس
    public string StoreName     { get; set; } = string.Empty; // الاسم كما يُعرض
    public string BranchId      { get; set; } = string.Empty;
    public double Latitude      { get; set; }
    public double Longitude     { get; set; }
    /// <summary>مصدر الصفّ: "talabat" (اكتشاف) أو "independent" (متاجر مستقلّة) — كل مصدر يحدّث صفوفه فقط.</summary>
    public string Source        { get; set; } = "talabat";
}

/// <summary>
/// سجلّ تحقّق سعر ميداني (append-only): كل عملية مسح من تطبيق validation تولّد صفّاً ثابتاً
/// لا يُعدّل ولا يُمسح. يحفظ لقطة (السعر المخزّن عندنا وقت الفحص مقابل السعر الحقيقي الذي
/// رصده العامل) فيعطي تاريخاً كاملاً بكلفة صفّ صغير لكل فحص — دون لمس السعر الحيّ.
/// MerchantId مجرّد معرّف مفهرس بلا مفتاح أجنبي حتى يبقى التاريخ سليماً لو حُذف التاجر.
/// </summary>
public class PriceValidation : BaseEntity
{
    public Guid MerchantId      { get; set; }            // الفرع (تاجر سلّمني) الذي رُصد فيه
    public string MerchantName  { get; set; } = string.Empty; // لقطة اسم الفرع وقت الفحص
    public string? BranchId     { get; set; }            // لقطة معرّف الفرع الخارجي إن وُجد

    public Guid? ProductId      { get; set; }            // الصنف المطابق للباركود (إن وُجد)
    public string Barcode       { get; set; } = string.Empty;
    public string? ProductName  { get; set; }            // لقطة اسم الصنف وقت الفحص

    /// <summary>السعر المخزّن عندنا للفرع وقت الفحص (null إن لم يكن للصنف سعر في هذا الفرع).</summary>
    public decimal? ExpectedPrice { get; set; }
    /// <summary>السعر الحقيقي الذي أكّده/أدخله العامل.</summary>
    public decimal ActualPrice  { get; set; }
    /// <summary>هل طابق المخزّن الواقع؟ (يُحسب وقت الفحص).</summary>
    public bool IsMatch         { get; set; }

    public double? Latitude     { get; set; }            // موقع العامل وقت الفحص
    public double? Longitude    { get; set; }
    public string? Auditor      { get; set; }            // اسم/جهاز العامل (اختياري)
}
