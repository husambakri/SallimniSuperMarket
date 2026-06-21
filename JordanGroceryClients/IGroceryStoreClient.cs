// ===================================================
// واجهة مشتركة لجميع متاجر البقالة الأردنية
// ===================================================
namespace JordanGrocery;

public record ProductInfo(
    string  Store,        // اسم المتجر
    string  ProductId,    // رقم المنتج الداخلي
    string  Barcode,      // الباركود EAN
    string  Name,         // اسم المنتج
    decimal Price,        // السعر الحالي
    decimal Special,      // سعر التخفيض (0 = لا يوجد)
    bool    InStock,      // متوفر؟
    string  StockStatus,  // نص حالة التوفر
    string  ImageUrl,     // رابط الصورة
    string  ProductUrl    // رابط صفحة المنتج
);

public interface IGroceryStoreClient
{
    string StoreName { get; }
    Task<ProductInfo?> GetByBarcodeAsync(string barcode);
    Task<ProductInfo?> GetByProductIdAsync(string productId);
}
