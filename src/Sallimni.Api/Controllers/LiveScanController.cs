using JordanGrocery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sallimni.Application.Services;
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

    private record CachedScan(List<LiveScanResult> Results, int StoresQueried);

    [HttpGet("{barcode}")]
    public async Task<IActionResult> Compare(
        string barcode, [FromQuery] double? lat, [FromQuery] double? lng, CancellationToken ct)
    {
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0) return BadRequest(new { error = "باركود فارغ." });

        // أسعار الفهرس تُخزَّن مؤقّتًا بالباركود؛ أمّا أقرب فرع فيُحسب لكل طلب حسب موقع المستخدم.
        var cacheKey = "scan:" + barcode;
        if (!_cache.TryGetValue(cacheKey, out CachedScan? cached) || cached is null)
        {
            var indexed = await _db.TalabatPriceIndex
                .Where(e => e.Barcode == barcode)
                .ToListAsync(ct);

            var baseResults = indexed
                .Select(e => ToResult(e.StoreName, e.Name, e.Price, e.Special, e.InStock,
                    e.InStock ? "In Stock" : "Out Of Stock", e.ImageUrl, e.ProductUrl, e.Latitude, e.Longitude))
                .OrderBy(r => r.EffectivePrice)
                .ToList();

            var storesQueried = await _db.TalabatPriceIndex.Select(e => e.BranchId).Distinct().CountAsync(ct);
            cached = new CachedScan(baseResults, storesQueried);
            _cache.Set(cacheKey, cached, CacheTtl);
        }

        var results = await ApplyNearestBranchAsync(cached.Results, lat, lng, ct);

        var payload = new { barcode, timedOut = false, storesQueried = cached.StoresQueried, count = results.Count, results };
        return Ok(payload);
    }

    /// <summary>
    /// يستبدل إحداثيات كل نتيجة بإحداثيات أقرب فرع لنفس المتجر من دليل الفروع (حسب موقع
    /// المستخدم) — فيُظهر التطبيق مسافة أقرب فرع لا الفرع المفهرَس عشوائيًّا. بلا موقع: بلا تغيير.
    /// </summary>
    private async Task<List<LiveScanResult>> ApplyNearestBranchAsync(
        List<LiveScanResult> results, double? lat, double? lng, CancellationToken ct)
    {
        if (lat is null || lng is null || results.Count == 0) return results;

        var names = results.Select(r => TalabatDiscovery.NormalizeName(r.Store)).Distinct().ToList();
        var branches = await _db.StoreBranches.Where(b => names.Contains(b.StoreNameNorm)).ToListAsync(ct);
        if (branches.Count == 0) return results;

        var byStore = branches.GroupBy(b => b.StoreNameNorm).ToDictionary(g => g.Key, g => g.ToList());
        var me = new GeoPoint(lat.Value, lng.Value);

        return results.Select(r =>
        {
            if (byStore.TryGetValue(TalabatDiscovery.NormalizeName(r.Store), out var bs))
            {
                var nearest = bs.OrderBy(b => GeoUtils.DistanceKm(me, new GeoPoint(b.Latitude, b.Longitude))).First();
                return r with { Latitude = nearest.Latitude, Longitude = nearest.Longitude };
            }
            return r;
        }).ToList();
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
