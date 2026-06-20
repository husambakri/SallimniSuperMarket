using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Seeding;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Controllers;

/// <summary>عمليات صيانة لمرّة واحدة (استيراد الكتالوج الحقيقي). محميّة بكلمة تأكيد.</summary>
[ApiController]
[Route("api/maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly SallimniDbContext _db;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(SallimniDbContext db, ILogger<MaintenanceController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>حالة الكتالوج الحالية (للتحقّق قبل/بعد الاستيراد).</summary>
    [HttpGet("catalog-status")]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(new
        {
            merchants = await _db.Merchants.CountAsync(ct),
            categories = await _db.Categories.CountAsync(ct),
            products = await _db.Products.CountAsync(ct),
            merchantProducts = await _db.MerchantProducts.CountAsync(ct),
            seedFileExists = System.IO.File.Exists(DataSeeder.DefaultCatalogPath),
        });

    /// <summary>
    /// استيراد الكتالوج الحقيقي من ملف البذرة. مع <c>reset=true</c> يمسح الكتالوج
    /// والطلبات القائمة أولاً (يُبقي الزبائن/السائقين/الإعدادات). يتطلّب <c>confirm=RESET</c>.
    /// </summary>
    [HttpPost("import-catalog")]
    public async Task<IActionResult> ImportCatalog(
        [FromQuery] string? confirm, [FromQuery] bool reset = true, CancellationToken ct = default)
    {
        if (confirm != "RESET")
            return BadRequest(new { error = "أضِف ?confirm=RESET للتأكيد (عملية تكتب على القاعدة)." });

        if (!System.IO.File.Exists(DataSeeder.DefaultCatalogPath))
            return BadRequest(new { error = $"ملف البذرة غير موجود: {DataSeeder.DefaultCatalogPath}" });

        if (reset)
        {
            _logger.LogWarning("Maintenance: مسح الكتالوج والطلبات قبل الاستيراد…");
            // CASCADE يتكفّل بترتيب المفاتيح الأجنبية. يُبقي Customers/Drivers/Addresses/Configs.
            await _db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE " +
                "\"OrderItems\",\"SubOrders\",\"Orders\"," +
                "\"Settlements\",\"ScanEvents\",\"RouteStops\",\"DeliveryTasks\",\"Waves\"," +
                "\"MerchantProducts\",\"HubProducts\",\"ProductSubmissions\",\"BarcodeScans\"," +
                "\"Products\",\"Categories\",\"Merchants\" " +
                "RESTART IDENTITY CASCADE;", ct);
        }
        else if (await _db.Products.CountAsync(ct) > 0)
        {
            return BadRequest(new { error = "الكتالوج غير فارغ. استخدم reset=true للاستبدال." });
        }

        await DataSeeder.SeedEssentialsAsync(_db);
        var summary = await CatalogImporter.ImportAsync(_db, DataSeeder.DefaultCatalogPath, _logger, ct);

        return Ok(new
        {
            ok = true,
            imported = new
            {
                merchants = summary.Merchants,
                categories = summary.Categories,
                products = summary.Products,
                merchantProducts = summary.MerchantProducts,
            }
        });
    }
}
