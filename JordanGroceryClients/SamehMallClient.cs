// ===================================================
// سامح مول — ⚠️ متوقف مؤقتاً
// www.samehgroup.com لا يستجيب (الموقع متوقف)
// TODO: أعد تفعيله عند عودة الموقع
// ===================================================
namespace JordanGrocery;

public class SamehMallClient : IGroceryStoreClient
{
    public string StoreName => "Sameh Mall";

    public Task<ProductInfo?> GetByBarcodeAsync(string barcode)
        => Task.FromResult<ProductInfo?>(null);

    public Task<ProductInfo?> GetByProductIdAsync(string productId)
        => Task.FromResult<ProductInfo?>(null);
}
