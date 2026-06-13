using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;

namespace Sallimni.Infrastructure.Services;

// ===== نماذج عرض خاصة بالتاجر =====

/// <summary>صنف في كتالوج التاجر: بطاقة الإدارة + سعر/مخزون التاجر (إن رُبط).</summary>
public record MerchantCatalogRow(
    Guid ProductId, string NameAr, string NameEn, string? Barcode, string? UnitSize,
    TaxClass TaxClass, bool IsLinked, decimal? Price, int StockQty, bool IsAvailable);

public record MerchantSubOrderRow(
    Guid SubOrderId, Guid OrderId, SubOrderStatus Status, decimal SubtotalInclTax,
    decimal MerchantPayout, DateTimeOffset CreatedAt, List<MerchantSubOrderItem> Items);

public record MerchantSubOrderItem(string Name, int Quantity, decimal UnitPriceInclTax);

/// <summary>
/// خدمات التاجر (قسم 2): ربط المخزون والسعر، استقبال وتجهيز الطلبات الفرعية،
/// طلب إضافة صنف جديد لطابور اعتماد الإدارة (قسم 3).
/// </summary>
public class MerchantService
{
    private readonly SallimniDbContext _db;
    public MerchantService(SallimniDbContext db) => _db = db;

    /// <summary>كتالوج كامل مع سعر/مخزون التاجر (null إن لم يُربط بعد).</summary>
    public async Task<List<MerchantCatalogRow>> GetCatalogAsync(Guid merchantId, CancellationToken ct = default)
    {
        var products = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.NameAr).ToListAsync(ct);
        var links = await _db.MerchantProducts
            .Where(mp => mp.MerchantId == merchantId)
            .ToDictionaryAsync(mp => mp.ProductId, ct);

        return products.Select(p =>
        {
            links.TryGetValue(p.Id, out var mp);
            return new MerchantCatalogRow(
                p.Id, p.NameAr, p.NameEn, p.Barcode, p.UnitSize, p.TaxClass,
                mp is not null, mp?.Price, mp?.StockQty ?? 0, mp?.IsAvailable ?? false);
        }).ToList();
    }

    /// <summary>ربط/تحديث سعر التاجر وكميته لبطاقة صنف (السعر شامل الضريبة).</summary>
    public async Task UpsertProductAsync(
        Guid merchantId, Guid productId, decimal price, int stockQty, bool isAvailable, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
            throw new InvalidOperationException("الصنف غير موجود.");

        var mp = await _db.MerchantProducts
            .FirstOrDefaultAsync(x => x.MerchantId == merchantId && x.ProductId == productId, ct);

        if (mp is null)
        {
            mp = new MerchantProduct { MerchantId = merchantId, ProductId = productId };
            _db.MerchantProducts.Add(mp);
        }
        mp.Price = price;
        mp.StockQty = stockQty;
        mp.IsAvailable = isAvailable && stockQty > 0;
        mp.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>الطلبات الفرعية الواردة للتاجر (الأحدث أولاً).</summary>
    public async Task<List<MerchantSubOrderRow>> GetSubOrdersAsync(Guid merchantId, CancellationToken ct = default)
    {
        return await _db.SubOrders
            .Where(s => s.MerchantId == merchantId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new MerchantSubOrderRow(
                s.Id, s.OrderId, s.Status, s.SubtotalInclTax, s.MerchantPayout, s.CreatedAt,
                s.Items.Select(i => new MerchantSubOrderItem(i.ProductNameSnapshot, i.Quantity, i.UnitPriceInclTax)).ToList()))
            .ToListAsync(ct);
    }

    /// <summary>تحديث حالة طلب فرعي (مثلاً بدء التحضير → جاهز).</summary>
    public async Task UpdateSubOrderStatusAsync(Guid subOrderId, SubOrderStatus status, CancellationToken ct = default)
    {
        var sub = await _db.SubOrders.FirstOrDefaultAsync(s => s.Id == subOrderId, ct)
            ?? throw new InvalidOperationException("الطلب الفرعي غير موجود.");
        sub.Status = status;
        sub.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>طلب إضافة صنف جديد — يدخل طابور اعتماد الإدارة.</summary>
    public async Task<ProductSubmission> CreateSubmissionAsync(
        Guid merchantId, string nameAr, string nameEn, string? barcode, string? unitSize,
        TaxClass suggestedTaxClass, CancellationToken ct = default)
    {
        var sub = new ProductSubmission
        {
            MerchantId = merchantId,
            NameAr = nameAr,
            NameEn = nameEn,
            Barcode = barcode,
            UnitSize = unitSize,
            SuggestedTaxClass = suggestedTaxClass,
            Status = SubmissionStatus.Pending
        };
        _db.ProductSubmissions.Add(sub);
        await _db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<List<ProductSubmission>> GetSubmissionsAsync(Guid merchantId, CancellationToken ct = default)
        => await _db.ProductSubmissions
            .Where(s => s.MerchantId == merchantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
}
