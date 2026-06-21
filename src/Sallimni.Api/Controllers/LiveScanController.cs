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

        // حدّ زمني إجمالي ~20ث حتى لا تتعلّق الاستجابة على متجر بطيء.
        var search = _agg.SearchAllAsync(barcode);
        var done = await Task.WhenAny(search, Task.Delay(TimeSpan.FromSeconds(12), ct));

        var stores = _agg.StoreNames.Count;
        if (done != search)
            return Ok(new { barcode, timedOut = true, storesQueried = stores, count = 0, results = Array.Empty<LiveScanResult>() });

        var results = (await search)
            .Select(p =>
            {
                var eff = p.Special > 0 ? p.Special : p.Price;
                return new LiveScanResult(p.Store, p.Name, p.Price, p.Special, eff,
                    p.InStock, p.StockStatus, p.ImageUrl, p.ProductUrl);
            })
            .OrderBy(r => r.EffectivePrice)
            .ToList();

        return Ok(new { barcode, timedOut = false, storesQueried = stores, count = results.Count, results });
    }
}
