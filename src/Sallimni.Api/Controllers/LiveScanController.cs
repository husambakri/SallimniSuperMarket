using JordanGrocery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Controllers;

/// <summary>
/// مقارنة سعر حيّة عند مسح الباركود. يدمج:
///  • متاجر الباركود الحيّة (Yaser، C-Town، ...) — استعلام مباشر وقت الطلب.
///  • متاجر طلبات — تُقرأ من فهرس مُحدَّث دوريًّا (TalabatPriceIndex) فورًا.
/// يُرجع قائمة نتائج (بطاقة لكل متجر) مرتّبة بالأرخص.
/// </summary>
[ApiController]
[Route("api/scan-compare")]
public class LiveScanController : ControllerBase
{
    private readonly GroceryAggregator _agg;
    private readonly SallimniDbContext _db;

    public LiveScanController(GroceryAggregator agg, SallimniDbContext db)
    {
        _agg = agg;
        _db = db;
    }

    public record LiveScanResult(
        string Store, string Name, decimal Price, decimal Special, decimal EffectivePrice,
        bool InStock, string StockStatus, string ImageUrl, string ProductUrl);

    [HttpGet("{barcode}")]
    public async Task<IActionResult> Compare(string barcode, CancellationToken ct)
    {
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0) return BadRequest(new { error = "باركود فارغ." });

        // طلبات: قراءة فوريّة من الفهرس المُحدَّث دوريًّا.
        var indexedTask = _db.TalabatPriceIndex
            .Where(e => e.Barcode == barcode)
            .ToListAsync(ct);

        // متاجر الباركود الحيّة، بحدّ زمني ~12ث حتى لا يتعلّق الردّ على متجر بطيء.
        var liveTask = _agg.SearchAllAsync(barcode);
        var done = await Task.WhenAny(liveTask, Task.Delay(TimeSpan.FromSeconds(12), ct));
        var liveTimedOut = done != liveTask;
        var live = liveTimedOut ? new List<ProductInfo>() : await liveTask;

        var indexed = await indexedTask;

        var results = live
            .Select(p => ToResult(p.Store, p.Name, p.Price, p.Special, p.InStock, p.StockStatus, p.ImageUrl, p.ProductUrl))
            .Concat(indexed.Select(e => ToResult(e.StoreName, e.Name, e.Price, e.Special, e.InStock,
                e.InStock ? "In Stock" : "Out Of Stock", e.ImageUrl, e.ProductUrl)))
            .OrderBy(r => r.EffectivePrice)
            .ToList();

        // عدد المتاجر المغطّاة: الحيّة + عدد فروع طلبات المفهرَسة.
        var talabatStores = await _db.TalabatPriceIndex.Select(e => e.BranchId).Distinct().CountAsync(ct);
        var storesQueried = _agg.StoreNames.Count + talabatStores;

        return Ok(new { barcode, timedOut = liveTimedOut, storesQueried, count = results.Count, results });
    }

    private static LiveScanResult ToResult(string store, string name, decimal price, decimal special,
        bool inStock, string stockStatus, string imageUrl, string productUrl)
    {
        var effective = special > 0 ? special : price;
        return new LiveScanResult(store, name, price, special, effective, inStock, stockStatus, imageUrl, productUrl);
    }
}
