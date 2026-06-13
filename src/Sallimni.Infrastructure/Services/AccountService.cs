using Microsoft.EntityFrameworkCore;

namespace Sallimni.Infrastructure.Services;

/// <summary>
/// حذف الحساب — متطلب متجرَي التطبيقات (قسم 15: Apple 5.1.1 / Google).
/// يُلغى تنشيط الحساب وتُجهَّل بياناته الشخصية (PII) مع الإبقاء على سجلات الطلبات
/// لأغراض مالية/قانونية (تجهيل لا حذف صلب للسجلات المرتبطة).
/// </summary>
public class AccountService
{
    private readonly SallimniDbContext _db;
    public AccountService(SallimniDbContext db) => _db = db;

    public async Task DeleteCustomerAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Customers.Include(x => x.Addresses).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("الزبون غير موجود.");
        c.Name = "حساب محذوف";
        c.Phone = null;
        c.Email = null;
        c.IsActive = false;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        // إزالة العناوين (بيانات موقعية شخصية).
        _db.Addresses.RemoveRange(c.Addresses);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteMerchantAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _db.Merchants.Include(x => x.MerchantProducts).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("التاجر غير موجود.");
        m.Name = "متجر محذوف";
        m.Phone = null;
        m.Email = null;
        m.AddressText = null;
        m.IsActive = false;
        m.UpdatedAt = DateTimeOffset.UtcNow;
        // إيقاف عروض المتجر عن البيع.
        foreach (var mp in m.MerchantProducts) mp.IsAvailable = false;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteDriverAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _db.Drivers.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("السائق غير موجود.");
        d.Name = "سائق محذوف";
        d.Phone = null;
        d.LastLatitude = null;
        d.LastLongitude = null;
        d.IsActive = false;
        d.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
