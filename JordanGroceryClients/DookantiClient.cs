// ===================================================
// دكانتي — ⚠️ متوقف مؤقتاً
// dookanti.com لا يستجيب (connection refused)
// TODO: أعد تفعيله عند عودة الموقع
// ===================================================
namespace JordanGrocery;

public class DookantiClient : IGroceryStoreClient
{
    public string StoreName => "Dookanti";

    public Task<ProductInfo?> GetByBarcodeAsync(string barcode)
        => Task.FromResult<ProductInfo?>(null);

    public Task<ProductInfo?> GetByProductIdAsync(string productId)
        => Task.FromResult<ProductInfo?>(null);
}
