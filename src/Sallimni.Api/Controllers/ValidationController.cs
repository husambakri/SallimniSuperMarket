using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Dtos;
using Sallimni.Domain.Entities;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Controllers;

/// <summary>
/// تحقّق السعر الميداني (تطبيق validation): العامل يختار الفرع من قائمة، فيعطيه سعرنا
/// المخزّن للباركود في ذلك الفرع، ويسجّل ما رصده كصفّ تاريخي append-only
/// (لا يلمس السعر الحيّ في MerchantProduct).
/// </summary>
[ApiController]
[Route("api/validation")]
public class ValidationController : ControllerBase
{
    private readonly SallimniDbContext _db;

    public ValidationController(SallimniDbContext db) => _db = db;

    /// <summary>قائمة المتاجر الفعّالة (تُثبَّت في ترويسة المسح ليختار العامل الفرع).</summary>
    [HttpGet("merchants")]
    public async Task<IActionResult> MerchantList(CancellationToken ct)
    {
        var list = await _db.Merchants
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new ValidationMerchantDto(m.Id, m.Name))
            .ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// سعرنا المخزّن للباركود في الفرع الذي اختاره العامل. الفرع (merchantId) مطلوب.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(
        [FromQuery] string? barcode, [FromQuery] Guid merchantId, CancellationToken ct)
    {
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0) return BadRequest(new { error = "باركود فارغ." });
        if (merchantId == Guid.Empty) return BadRequest(new { error = "اختر المتجر أولاً." });

        var merchant = await _db.Merchants
            .Where(m => m.Id == merchantId)
            .Select(m => new { m.Id, m.Name, m.BranchId })
            .FirstOrDefaultAsync(ct);

        if (merchant is null)
            return Ok(new ValidationLookupDto(false, null, null, null, null, false, null, null, false, null, null, false));

        // الصنف المطابق للباركود (بطاقة الإدارة).
        var product = await _db.Products
            .Where(p => p.Barcode == barcode && p.IsActive)
            .Select(p => new { p.Id, p.NameAr })
            .FirstOrDefaultAsync(ct);

        decimal? expected = null;
        decimal? expectedSpecial = null;
        if (product is not null)
        {
            // سعرنا المخزّن لهذا الصنف في هذا الفرع تحديداً (العادي + العرض إن وُجد).
            var mp = await _db.MerchantProducts
                .Where(mp => mp.MerchantId == merchant.Id && mp.ProductId == product.Id)
                .Select(mp => new { mp.Price, mp.SpecialPrice })
                .FirstOrDefaultAsync(ct);
            if (mp is not null)
            {
                expected = mp.Price;
                // العرض فقط إن كان أقلّ من العادي.
                expectedSpecial = mp.SpecialPrice is { } sp && sp > 0 && sp < mp.Price ? sp : null;
            }
        }

        return Ok(new ValidationLookupDto(
            BranchFound: true,
            MerchantId: merchant.Id,
            MerchantName: merchant.Name,
            BranchId: merchant.BranchId,
            DistanceKm: null,
            ProductFound: product is not null,
            ProductId: product?.Id,
            ProductName: product?.NameAr,
            HasOurPrice: expected.HasValue,
            ExpectedPrice: expected,
            ExpectedSpecialPrice: expectedSpecial,
            HasOffer: expectedSpecial.HasValue));
    }

    /// <summary>
    /// يسجّل عملية تحقّق كصفّ تاريخي ثابت. التطابق يُحسب على الخادم: مطابق فقط إن كان لنا
    /// سعر مخزّن وساوى الواقع. لا يُعدّل أي صفّ قائم ولا يمسّ MerchantProduct.
    /// </summary>
    [HttpPost("record")]
    public async Task<IActionResult> Record([FromBody] ValidationRecordRequest req, CancellationToken ct)
    {
        var barcode = (req.Barcode ?? "").Trim();
        if (barcode.Length == 0) return BadRequest(new { error = "باركود فارغ." });
        if (req.MerchantId == Guid.Empty) return BadRequest(new { error = "الفرع مطلوب." });

        // المطابقة ضدّ السعر الفعّال: سعر العرض إن وُجد، وإلّا العادي.
        var effectiveExpected = req.ExpectedSpecialPrice ?? req.ExpectedPrice;
        var isMatch = effectiveExpected.HasValue && effectiveExpected.Value == req.ActualPrice;

        var row = new PriceValidation
        {
            MerchantId           = req.MerchantId,
            MerchantName         = req.MerchantName ?? "",
            BranchId             = req.BranchId,
            ProductId            = req.ProductId,
            Barcode              = barcode,
            ProductName          = req.ProductName,
            ExpectedPrice        = req.ExpectedPrice,
            ExpectedSpecialPrice = req.ExpectedSpecialPrice,
            ActualPrice          = req.ActualPrice,
            IsMatch              = isMatch,
            Latitude      = req.Latitude,
            Longitude     = req.Longitude,
            Auditor       = req.Auditor,
        };
        _db.PriceValidations.Add(row);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, id = row.Id, isMatch });
    }

    /// <summary>الفروع التي ظهرت في سجلّ التحقّق (لمنتقي صفحة السجلّ) — الأحدث نشاطاً أولاً.</summary>
    [HttpGet("branches")]
    public async Task<IActionResult> Branches(CancellationToken ct)
    {
        // GROUP BY + التجميعات في SQL عبر نوع مجهول (EF لا يترجم الإسقاط مباشرةً إلى مُنشئ record)،
        // ثم الترتيب وبناء الـ DTO في الذاكرة — عدد الفروع صغير.
        var grouped = await _db.PriceValidations
            .GroupBy(v => new { v.MerchantId, v.MerchantName })
            .Select(g => new
            {
                g.Key.MerchantId,
                g.Key.MerchantName,
                Count = g.Count(),
                LastAt = g.Max(x => x.CreatedAt),
            })
            .ToListAsync(ct);

        var branches = grouped
            .OrderByDescending(b => b.LastAt)
            .Select(b => new ValidationBranchDto(b.MerchantId, b.MerchantName, b.Count, b.LastAt))
            .ToList();

        return Ok(branches);
    }

    /// <summary>
    /// لقطة حالة القاعدة: عدد الأصناف/المتاجر/الأسعار، آخر تحديث سعر وكم صنف تغيّر آخر 24 ساعة،
    /// وملخّص عمليات التحقّق — يعرضها التطبيق ليطمئنّ العامل لحداثة البيانات.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);

        var products  = await _db.Products.CountAsync(p => p.IsActive, ct);
        var merchants = await _db.Merchants.CountAsync(m => m.IsActive, ct);
        var priced    = await _db.MerchantProducts.CountAsync(ct);
        var offers    = await _db.MerchantProducts.CountAsync(mp => mp.SpecialPrice != null && mp.SpecialPrice > 0, ct);

        // آخر تحديث سعر (UpdatedAt إن وُجد وإلّا CreatedAt) + كم صنف تحدّث آخر 24 ساعة.
        var lastPriceUpdate = await _db.MerchantProducts
            .Select(mp => mp.UpdatedAt ?? mp.CreatedAt)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(ct);
        var updatedLast24h = await _db.MerchantProducts
            .CountAsync(mp => (mp.UpdatedAt ?? mp.CreatedAt) >= since, ct);

        var validations = await _db.PriceValidations.CountAsync(ct);
        var mismatches  = await _db.PriceValidations.CountAsync(v => !v.IsMatch, ct);
        var lastValidation = await _db.PriceValidations
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => (DateTimeOffset?)v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return Ok(new ValidationStatsDto(
            products, merchants, priced, offers,
            lastPriceUpdate == default ? null : lastPriceUpdate, updatedLast24h,
            validations, mismatches, lastValidation));
    }

    /// <summary>سجلّ تحقّقات فرع واحد، الأحدث أولاً.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] Guid merchantId, CancellationToken ct)
    {
        if (merchantId == Guid.Empty) return BadRequest(new { error = "الفرع مطلوب." });

        var rows = await _db.PriceValidations
            .Where(v => v.MerchantId == merchantId)
            .OrderByDescending(v => v.CreatedAt)
            .Take(500)
            .Select(v => new ValidationHistoryDto(
                v.Id, v.Barcode, v.ProductName,
                v.ExpectedPrice, v.ExpectedSpecialPrice, v.ActualPrice, v.IsMatch, v.Auditor, v.CreatedAt))
            .ToListAsync(ct);

        return Ok(rows);
    }
}
