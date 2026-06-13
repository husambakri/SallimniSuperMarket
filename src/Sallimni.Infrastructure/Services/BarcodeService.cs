using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;

namespace Sallimni.Infrastructure.Services;

/// <summary>نتيجة فحص السعر بالباركود (قسم 4.1) — يُعرض سعرنا فقط (أرخص تاجر، شامل الضريبة).</summary>
public record BarcodeLookupResult(
    bool Found,
    Guid? ProductId,
    string? NameAr,
    string? NameEn,
    string? ImageUrl,
    string? Emoji,
    decimal? OurPriceInclTax,
    decimal? RegularPriceInclTax,
    int SavingsPercent,
    Guid? CheapestMerchantId);

/// <summary>خطّاف Scan-to-Compare: رقم الباركود ← أرخص سعر متوفر شامل الضريبة.</summary>
public class BarcodeService
{
    private readonly SallimniDbContext _db;
    public BarcodeService(SallimniDbContext db) => _db = db;

    public async Task<BarcodeLookupResult> LookupAsync(
        string barcode, Guid? customerId = null, bool logScan = true, CancellationToken ct = default)
    {
        barcode = (barcode ?? string.Empty).Trim();

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.IsActive && p.Barcode == barcode, ct);

        BarcodeLookupResult result;
        if (product is null)
        {
            result = new BarcodeLookupResult(false, null, null, null, null, null, null, null, 0, null);
        }
        else
        {
            var offers = await _db.MerchantProducts
                .Where(mp => mp.ProductId == product.Id && mp.IsAvailable && mp.StockQty > 0)
                .OrderBy(mp => mp.Price)
                .Select(mp => new { mp.Price, mp.MerchantId })
                .ToListAsync(ct);

            if (offers.Count == 0)
            {
                result = new BarcodeLookupResult(false, product.Id, product.NameAr, product.NameEn,
                    product.ImageUrl, product.Emoji, null, null, 0, null);
            }
            else
            {
                var cheapest = offers.First();
                var regular = offers.Max(o => o.Price);
                var savings = regular > 0 ? (int)Math.Round((regular - cheapest.Price) / regular * 100m) : 0;
                result = new BarcodeLookupResult(true, product.Id, product.NameAr, product.NameEn,
                    product.ImageUrl, product.Emoji, cheapest.Price, regular, savings, cheapest.MerchantId);
            }
        }

        if (logScan)
        {
            _db.BarcodeScans.Add(new BarcodeScan
            {
                Barcode = barcode,
                Result = result.Found ? BarcodeScanResult.Found : BarcodeScanResult.NotFound,
                ProductId = product?.Id,
                CustomerId = customerId
            });
            await _db.SaveChangesAsync(ct);
        }

        return result;
    }
}
