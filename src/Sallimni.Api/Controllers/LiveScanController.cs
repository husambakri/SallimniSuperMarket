using JordanGrocery;
using Microsoft.AspNetCore.Mvc;

namespace Sallimni.Api.Controllers;

/// <summary>
/// مقارنة سعر حيّة عند مسح الباركود عبر متاجر البقالة الأردنية (JordanGroceryClients).
/// للتجربة: يُرجع قائمة نتائج (بطاقة لكل متجر) مرتّبة بالأرخص.
/// </summary>
[ApiController]
[Route("api/scan-compare")]
public class LiveScanController : ControllerBase
{
    private readonly GroceryAggregator _agg;
    public LiveScanController(GroceryAggregator agg) => _agg = agg;

    public record LiveScanResult(
        string Store, string Name, decimal Price, decimal Special, decimal EffectivePrice,
        bool InStock, string StockStatus, string ImageUrl, string ProductUrl);

    [HttpGet("{barcode}")]
    public async Task<IActionResult> Compare(string barcode, CancellationToken ct)
    {
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0) return BadRequest(new { error = "باركود فارغ." });

        // حدّ زمني إجمالي ~12ث حتى لا تتعلّق الاستجابة على متجر بطيء.
        var scan = _agg.ScanCompareAsync(barcode);
        var done = await Task.WhenAny(scan, Task.Delay(TimeSpan.FromSeconds(12), ct));

        var stores = _agg.StoreNames.Count;
        if (done != scan)
            return Ok(new { barcode, timedOut = true, storesQueried = stores, count = 0, results = Array.Empty<LiveScanResult>() });

        // العروض مرتّبة أصلاً تصاعديًا بالسعر الفعلي من ScanCompareAsync.
        var comparison = await scan;
        var results = comparison.Offers
            .Select(o => new LiveScanResult(o.Store, o.Name, o.Price, o.Special, o.EffectivePrice,
                o.InStock, o.StockStatus, o.ImageUrl, o.ProductUrl))
            .ToList();

        return Ok(new { barcode, timedOut = false, storesQueried = stores, count = results.Count, results });
    }
}
