using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Controllers;

/// <summary>
/// مقارنة سعر عند مسح الباركود من فهرس الأسعار (TalabatPriceIndex) — قراءة فوريّة،
/// تشمل المتاجر المفهرَسة فقط (طلبات + المتاجر المستقلّة المسحوبة). بلا استعلام حيّ.
/// </summary>
[ApiController]
[Route("api/scan-compare")]
public class LiveScanController : ControllerBase
{
    private readonly SallimniDbContext _db;
    private readonly IMemoryCache _cache;

    // مدّة تخزين نتيجة المسح — تسريع الاستعلامات المكرّرة (تسعيرة البقالة لا تتغيّر بالدقائق).
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public LiveScanController(SallimniDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public record LiveScanResult(
        string Store, string Name, decimal Price, decimal Special, decimal EffectivePrice,
        bool InStock, string StockStatus, string ImageUrl, string ProductUrl,
        double? Latitude, double? Longitude);

    [HttpGet("{barcode}")]
    public async Task<IActionResult> Compare(string barcode, CancellationToken ct)
    {
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0) return BadRequest(new { error = "باركود فارغ." });

        // تسريع: أعِد النتيجة المخزّنة فورًا إن وُجدت.
        var cacheKey = "scan:" + barcode;
        if (_cache.TryGetValue(cacheKey, out object? cachedPayload))
            return Ok(cachedPayload);

        // قراءة فوريّة من فهرس الأسعار (المتاجر المفهرَسة فقط — لا استعلام حيّ).
        var indexed = await _db.TalabatPriceIndex
            .Where(e => e.Barcode == barcode)
            .ToListAsync(ct);

        var results = indexed
            .Select(e => ToResult(e.StoreName, e.Name, e.Price, e.Special, e.InStock,
                e.InStock ? "In Stock" : "Out Of Stock", e.ImageUrl, e.ProductUrl, e.Latitude, e.Longitude))
            .OrderBy(r => r.EffectivePrice)
            .ToList();

        var storesQueried = await _db.TalabatPriceIndex.Select(e => e.BranchId).Distinct().CountAsync(ct);

        var payload = new { barcode, timedOut = false, storesQueried, count = results.Count, results };
        _cache.Set(cacheKey, payload, CacheTtl);
        return Ok(payload);
    }

    /// <summary>حالة فهرسة متاجر طلبات: كم متجر، كم منتج، وآخر تحديث لكل متجر (لمتابعة التقدّم).</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var perStore = await _db.TalabatPriceIndex
            .GroupBy(e => new { e.BranchId, e.StoreName })
            .Select(g => new
            {
                store       = g.Key.StoreName,
                branchId    = g.Key.BranchId,
                products    = g.Count(),
                lastUpdated = g.Max(x => x.UpdatedAt),
            })
            .OrderByDescending(x => x.lastUpdated)
            .ToListAsync(ct);

        return Ok(new
        {
            storesIndexed = perStore.Count,
            totalProducts = perStore.Sum(x => x.products),
            lastUpdated   = perStore.Count > 0 ? perStore.Max(x => x.lastUpdated) : null,
            stores        = perStore,
        });
    }

    private static LiveScanResult ToResult(string store, string name, decimal price, decimal special,
        bool inStock, string stockStatus, string imageUrl, string productUrl, double? lat, double? lng)
    {
        var effective = special > 0 ? special : price;
        return new LiveScanResult(store, name, price, special, effective, inStock, stockStatus, imageUrl, productUrl, lat, lng);
    }
}
