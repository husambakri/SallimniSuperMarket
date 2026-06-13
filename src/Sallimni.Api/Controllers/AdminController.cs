using Microsoft.AspNetCore.Mvc;
using Sallimni.Api.Dtos;
using Sallimni.Domain.Enums;
using Sallimni.Infrastructure.Services;

namespace Sallimni.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AdminService _admin;
    public AdminController(AdminService admin) => _admin = admin;

    // ===== الكتالوج =====
    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories(CancellationToken ct)
        => (await _admin.GetCategoriesAsync(ct)).Select(c => new CategoryDto(c.Id, c.NameAr, c.NameEn)).ToList();

    [HttpPost("categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        var c = await _admin.CreateCategoryAsync(req.NameAr, req.NameEn, req.Icon, ct);
        return new CategoryDto(c.Id, c.NameAr, c.NameEn);
    }

    /// <summary>قائمة الأصناف (للإدارة).</summary>
    [HttpGet("products")]
    public async Task<ActionResult<List<AdminProductDto>>> GetProducts(CancellationToken ct)
        => (await _admin.GetProductsAsync(ct)).Select(p => new AdminProductDto(
            p.Id, p.NameAr, p.NameEn, p.Barcode, p.UnitSize, p.Emoji, p.ImageUrl, p.TaxClass,
            p.CategoryId, p.CategoryNameAr, p.MerchantCount, p.IsActive)).ToList();

    /// <summary>تأسيس بطاقة صنف رئيسية.</summary>
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest req, CancellationToken ct)
    {
        try
        {
            var p = await _admin.CreateProductAsync(req.NameAr, req.NameEn, req.Barcode, req.UnitSize,
                req.Emoji, req.Description, req.CategoryId, req.TaxClass, ct);
            return Ok(new { p.Id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>تعديل بطاقة صنف.</summary>
    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest req, CancellationToken ct)
    {
        try
        {
            await _admin.UpdateProductAsync(id, req.NameAr, req.NameEn, req.Barcode, req.UnitSize,
                req.Emoji, req.Description, req.CategoryId, req.TaxClass, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>حذف صنف (إيقاف تفعيل).</summary>
    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct)
    {
        try { await _admin.DeleteProductAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>رفع صورة صنف (multipart).</summary>
    [HttpPost("products/{id:guid}/image")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "لا ملف." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "حجم الصورة يتجاوز 5MB." });
        if (!file.ContentType.StartsWith("image/")) return BadRequest(new { error = "الملف ليس صورة." });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        try
        {
            await _admin.SetProductImageAsync(id, ms.ToArray(), file.ContentType, ct);
            return Ok(new { imageUrl = $"/api/catalog/products/{id}/image" });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>تعديل تصنيف.</summary>
    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest req, CancellationToken ct)
    {
        try { await _admin.UpdateCategoryAsync(id, req.NameAr, req.NameEn, req.Icon, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>حذف تصنيف (ممنوع إن كان فيه أصناف فعّالة).</summary>
    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        try { await _admin.DeleteCategoryAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ===== اعتماد طلبات التجار =====
    [HttpGet("submissions")]
    public async Task<ActionResult<List<AdminSubmissionDto>>> GetSubmissions(CancellationToken ct)
        => (await _admin.GetPendingSubmissionsAsync(ct)).Select(s => new AdminSubmissionDto(
            s.Id, s.MerchantId, s.NameAr, s.NameEn, s.Barcode, s.UnitSize, s.SuggestedTaxClass, s.CreatedAt)).ToList();

    [HttpPost("submissions/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSubmissionRequest req, CancellationToken ct)
    {
        try
        {
            var p = await _admin.ApproveSubmissionAsync(id, req.CategoryId, ct);
            return Ok(new { productId = p.Id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("submissions/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectSubmissionRequest req, CancellationToken ct)
    {
        try { await _admin.RejectSubmissionAsync(id, req.Note, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ===== الموجات والمهام =====
    [HttpGet("waves")]
    public async Task<ActionResult<List<WaveSummaryDto>>> GetWaves(CancellationToken ct)
        => (await _admin.GetWavesAsync(ct)).Select(w => new WaveSummaryDto(
            w.WaveId, (int)w.Status, w.CollectionStartAt, w.DistributionStartAt,
            w.OrderCount, w.SubOrderCount, w.HasCollectionTask, w.HasDistributionTask)).ToList();

    [HttpPost("waves/{waveId:guid}/collection-task")]
    public async Task<IActionResult> CreateCollectionTask(Guid waveId, CancellationToken ct)
    {
        try { var t = await _admin.CreateCollectionTaskAsync(waveId, ct); return Ok(new { taskId = t.Id }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("waves/{waveId:guid}/distribution-task")]
    public async Task<IActionResult> CreateDistributionTask(Guid waveId, CancellationToken ct)
    {
        try { var t = await _admin.CreateDistributionTaskAsync(waveId, ct); return Ok(new { taskId = t.Id }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<List<TaskDto>>> GetTasks(CancellationToken ct)
        => (await _admin.GetTasksAsync(ct)).Select(t => new TaskDto(
            t.TaskId, (int)t.Type, (int)t.Status, t.WaveId, t.DriverId, t.DriverName,
            t.Stops.Select(s => new TaskStopDto(s.Sequence, s.Label, s.Latitude, s.Longitude, s.IsCompleted)).ToList())).ToList();

    [HttpPost("tasks/{taskId:guid}/assign")]
    public async Task<IActionResult> Assign(Guid taskId, [FromBody] AssignDriverRequest req, CancellationToken ct)
    {
        try { await _admin.AssignDriverAsync(taskId, req.DriverId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("drivers")]
    public async Task<ActionResult<List<DriverDto>>> GetDrivers(CancellationToken ct)
        => (await _admin.GetDriversAsync(ct)).Select(d => new DriverDto(d.Id, d.Name, d.Phone)).ToList();

    // ===== الإعدادات =====
    [HttpGet("commission")]
    public async Task<ActionResult<CommissionConfigDto2>> GetCommission(CancellationToken ct)
    {
        var c = await _admin.GetCommissionConfigAsync(ct);
        return new CommissionConfigDto2(c.DefaultRate);
    }

    [HttpPut("commission")]
    public async Task<IActionResult> UpdateCommission([FromBody] UpdateCommissionRequest req, CancellationToken ct)
    {
        await _admin.UpdateCommissionRateAsync(req.DefaultRate, ct);
        return NoContent();
    }

    [HttpGet("wave-config")]
    public async Task<ActionResult<WaveConfigDto>> GetWaveConfig(CancellationToken ct)
    {
        var c = await _admin.GetWaveConfigAsync(ct);
        return new WaveConfigDto(c.WaveIntervalMinutes, c.DistributionGapMinutes,
            c.DefaultPrepMinutes, c.DefaultTransitMinutes, c.MaxCustomersPerDriver);
    }

    [HttpPut("wave-config")]
    public async Task<IActionResult> UpdateWaveConfig([FromBody] WaveConfigDto req, CancellationToken ct)
    {
        await _admin.UpdateWaveConfigAsync(req.WaveIntervalMinutes, req.DistributionGapMinutes,
            req.DefaultPrepMinutes, req.DefaultTransitMinutes, req.MaxCustomersPerDriver, ct);
        return NoContent();
    }

    // ===== التسويات =====
    [HttpGet("settlements")]
    public async Task<ActionResult<List<SettlementRowDto>>> GetSettlements(CancellationToken ct)
        => (await _admin.GetSettlementsAsync(ct)).Select(s => new SettlementRowDto(
            s.SubOrderId, s.MerchantId, s.MerchantName, (int)s.Status,
            s.SubtotalInclTax, s.CommissionAmount, s.MerchantPayout)).ToList();

    [HttpPost("settlements/{subOrderId:guid}/settle")]
    public async Task<IActionResult> Settle(Guid subOrderId, CancellationToken ct)
    {
        try { await _admin.MarkSettledAsync(subOrderId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
