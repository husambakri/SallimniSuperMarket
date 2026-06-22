// ===================================================
// كارفور الأردن — ⚠️ متوقف مؤقتاً
// www.carrefourjordan.com لا يستجيب (DNS/API متوقف)
// TODO: أعد تفعيله عند عودة الموقع
// ===================================================
namespace JordanGrocery;

public class CarrefourJordanClient : IGroceryStoreClient
{
    public string StoreName => "Carrefour Jordan";

    public Task<ProductInfo?> GetByBarcodeAsync(string barcode)
        => Task.FromResult<ProductInfo?>(null);

    public Task<ProductInfo?> GetByProductIdAsync(string productId)
        => Task.FromResult<ProductInfo?>(null);
}
