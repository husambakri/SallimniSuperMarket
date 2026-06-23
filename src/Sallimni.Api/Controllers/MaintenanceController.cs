using JordanGrocery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Seeding;
using Sallimni.Domain.Entities;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Controllers;

/// <summary>عمليات صيانة لمرّة واحدة (استيراد الكتالوج الحقيقي). محميّة بكلمة تأكيد.</summary>
[ApiController]
[Route("api/maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly SallimniDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(
        SallimniDbContext db, IServiceScopeFactory scopeFactory, ILogger<MaintenanceController> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
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
            storeBranches = await _db.StoreBranches.CountAsync(ct),          // دليل الفروع (لأقرب فرع)
            talabatRowsWithCoords = await _db.TalabatPriceIndex.CountAsync(e => e.Latitude != null, ct),
            seedFileExists = System.IO.File.Exists(DataSeeder.DefaultCatalogPath),
        });

    /// <summary>
    /// يشغّل اكتشاف فروع طلبات ويملأ دليل الفروع فوراً (في الخلفية) — يجعل المسافات تظهر
    /// دون انتظار دورة الفهرسة الكاملة. يتطلّب <c>confirm=RUN</c>.
    /// </summary>
    [HttpPost("refresh-branches")]
    public IActionResult RefreshBranches([FromQuery] string? confirm)
    {
        if (confirm != "RUN")
            return BadRequest(new { error = "أضِف ?confirm=RUN للتشغيل." });

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("[Maintenance] بدء اكتشاف الفروع لملء الدليل…");
                var (_, branches) = await TalabatDiscovery.DiscoverCityStoresAsync("amman");
                if (branches.Count == 0) { _logger.LogWarning("[Maintenance] لم تُكتشف فروع."); return; }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();
                db.StoreBranches.RemoveRange(await db.StoreBranches.ToListAsync());
                foreach (var b in branches)
                    db.StoreBranches.Add(new StoreBranch
                    {
                        StoreNameNorm = TalabatDiscovery.NormalizeName(b.Name),
                        StoreName = b.Name,
                        BranchId = b.BranchId,
                        Latitude = b.Latitude!.Value,
                        Longitude = b.Longitude!.Value,
                    });
                await db.SaveChangesAsync();
                _logger.LogInformation("[Maintenance] دليل الفروع امتلأ: {N} فرعًا", branches.Count);
            }
            catch (Exception ex) { _logger.LogError(ex, "[Maintenance] فشل ملء دليل الفروع"); }
        });

        return Accepted(new { ok = true, message = "بدأ اكتشاف الفروع في الخلفية. تحقّق عبر catalog-status بعد دقائق." });
    }

    /// <summary>
    /// يعيد فهرسة متجر مستقلّ واحد فوراً (في الخلفية) — لتطبيق إصلاحات السعر دون انتظار
    /// الدورة اليوميّة. store=ctown (افتراضي) أو yaser. يتطلّب <c>confirm=RUN</c>.
    /// </summary>
    [HttpPost("refresh-store")]
    public IActionResult RefreshStore([FromQuery] string? confirm, [FromQuery] string store = "ctown")
    {
        if (confirm != "RUN")
            return BadRequest(new { error = "أضِف ?confirm=RUN للتشغيل." });

        ICatalogStoreClient client = store.ToLowerInvariant() switch
        {
            "yaser" or "yasermall" => new YaserMallClient(),
            _ => new CTownClient(),
        };

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("[Maintenance] إعادة فهرسة {Store}…", client.StoreName);
                var products = await client.GetAllProductsAsync();
                if (products.Count == 0) { _logger.LogWarning("[Maintenance] {Store}: 0 منتج", client.StoreName); return; }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();
                var branchId = "store:" + client.StoreName;
                db.TalabatPriceIndex.RemoveRange(await db.TalabatPriceIndex.Where(e => e.BranchId == branchId).ToListAsync());
                var now = DateTimeOffset.UtcNow;
                foreach (var p in products)
                    db.TalabatPriceIndex.Add(new TalabatPriceEntry
                    {
                        BranchId = branchId, StoreName = client.StoreName, Barcode = p.Barcode, Name = p.Name,
                        Price = p.Price, Special = p.Special, InStock = p.InStock,
                        ImageUrl = p.ImageUrl, ProductUrl = p.ProductUrl, UpdatedAt = now,
                    });
                await db.SaveChangesAsync();
                _logger.LogInformation("[Maintenance] {Store} → {N} منتج", client.StoreName, products.Count);
            }
            catch (Exception ex) { _logger.LogError(ex, "[Maintenance] فشل إعادة فهرسة {Store}", client.StoreName); }
        });

        return Accepted(new { ok = true, store = client.StoreName, message = "بدأت إعادة الفهرسة في الخلفية. تحقّق بعد دقائق." });
    }

    public record BranchIngest(string StoreName, string BranchId, double Latitude, double Longitude);

    /// <summary>
    /// يبذر دليل الفروع من قائمة جاهزة (تُجمع من بيئة غير محجوبة) — بديل موثوق حين تحجب
    /// طلبات IP السيرفر. يستبدل الدليل بالكامل. يتطلّب <c>confirm=RUN</c>.
    /// </summary>
    [HttpPost("ingest-branches")]
    public async Task<IActionResult> IngestBranches(
        [FromQuery] string? confirm, [FromBody] List<BranchIngest> branches, CancellationToken ct)
    {
        if (confirm != "RUN")
            return BadRequest(new { error = "أضِف ?confirm=RUN للتأكيد." });
        if (branches is null || branches.Count == 0)
            return BadRequest(new { error = "لا فروع في الجسم." });

        // يستبدل فروع طلبات فقط — لا يمسّ المتاجر المستقلّة.
        _db.StoreBranches.RemoveRange(await _db.StoreBranches.Where(b => b.Source == "talabat").ToListAsync(ct));
        foreach (var b in branches)
            _db.StoreBranches.Add(new StoreBranch
            {
                StoreNameNorm = TalabatDiscovery.NormalizeName(b.StoreName),
                StoreName = b.StoreName,
                BranchId = b.BranchId,
                Latitude = b.Latitude,
                Longitude = b.Longitude,
                Source = "talabat",
            });
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true, count = branches.Count });
    }

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
