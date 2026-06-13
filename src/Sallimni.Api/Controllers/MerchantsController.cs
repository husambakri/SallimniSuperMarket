using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Dtos;
using Sallimni.Domain.Enums;
using Sallimni.Infrastructure;
using Sallimni.Infrastructure.Services;

namespace Sallimni.Api.Controllers;

[ApiController]
[Route("api/merchants")]
public class MerchantsController : ControllerBase
{
    private readonly SallimniDbContext _db;
    private readonly MerchantService _merchants;

    public MerchantsController(SallimniDbContext db, MerchantService merchants)
    {
        _db = db;
        _merchants = merchants;
    }

    /// <summary>قائمة التجار (للتجربة — لاختيار التاجر الحالي).</summary>
    [HttpGet]
    public async Task<ActionResult<List<MerchantInfoDto>>> GetAll(CancellationToken ct)
        => await _db.Merchants
            .Select(m => new MerchantInfoDto(m.Id, m.Name, m.IsSalesTaxRegistered))
            .ToListAsync(ct);

    [HttpGet("{merchantId:guid}")]
    public async Task<ActionResult<MerchantInfoDto>> Get(Guid merchantId, CancellationToken ct)
    {
        var m = await _db.Merchants.FindAsync(new object?[] { merchantId }, ct);
        return m is null ? NotFound() : new MerchantInfoDto(m.Id, m.Name, m.IsSalesTaxRegistered);
    }

    /// <summary>كتالوج التاجر مع سعره ومخزونه (لربط/تعديل الأسعار).</summary>
    [HttpGet("{merchantId:guid}/products")]
    public async Task<ActionResult<List<MerchantCatalogRowDto>>> GetProducts(Guid merchantId, CancellationToken ct)
    {
        var rows = await _merchants.GetCatalogAsync(merchantId, ct);
        return rows.Select(r => new MerchantCatalogRowDto(
            r.ProductId, r.NameAr, r.NameEn, r.Barcode, r.UnitSize, r.TaxClass,
            r.IsLinked, r.Price, r.StockQty, r.IsAvailable)).ToList();
    }

    /// <summary>ربط/تحديث سعر التاجر وكميته (السعر شامل الضريبة).</summary>
    [HttpPut("{merchantId:guid}/products/{productId:guid}")]
    public async Task<IActionResult> UpsertProduct(
        Guid merchantId, Guid productId, [FromBody] UpsertMerchantProductRequest req, CancellationToken ct)
    {
        try
        {
            await _merchants.UpsertProductAsync(merchantId, productId, req.Price, req.StockQty, req.IsAvailable, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>الطلبات الفرعية الواردة للتاجر.</summary>
    [HttpGet("{merchantId:guid}/suborders")]
    public async Task<ActionResult<List<MerchantSubOrderDto>>> GetSubOrders(Guid merchantId, CancellationToken ct)
    {
        var rows = await _merchants.GetSubOrdersAsync(merchantId, ct);
        return rows.Select(s => new MerchantSubOrderDto(
            s.SubOrderId, s.OrderId, (int)s.Status, s.SubtotalInclTax, s.MerchantPayout, s.CreatedAt,
            s.Items.Select(i => new MerchantSubOrderItemDto(i.Name, i.Quantity, i.UnitPriceInclTax)).ToList())).ToList();
    }

    /// <summary>تحديث حالة طلب فرعي (بدء التحضير / جاهز).</summary>
    [HttpPut("suborders/{subOrderId:guid}/status")]
    public async Task<IActionResult> UpdateSubOrderStatus(
        Guid subOrderId, [FromBody] UpdateSubOrderStatusRequest req, CancellationToken ct)
    {
        try
        {
            await _merchants.UpdateSubOrderStatusAsync(subOrderId, (SubOrderStatus)req.Status, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>طلب إضافة صنف جديد (طابور اعتماد الإدارة).</summary>
    [HttpPost("{merchantId:guid}/submissions")]
    public async Task<ActionResult<SubmissionDto>> CreateSubmission(
        Guid merchantId, [FromBody] CreateSubmissionRequest req, CancellationToken ct)
    {
        var s = await _merchants.CreateSubmissionAsync(
            merchantId, req.NameAr, req.NameEn, req.Barcode, req.UnitSize, req.SuggestedTaxClass, ct);
        return new SubmissionDto(s.Id, s.NameAr, s.NameEn, s.Barcode, s.UnitSize, s.SuggestedTaxClass, (int)s.Status, s.CreatedAt);
    }

    [HttpGet("{merchantId:guid}/submissions")]
    public async Task<ActionResult<List<SubmissionDto>>> GetSubmissions(Guid merchantId, CancellationToken ct)
    {
        var subs = await _merchants.GetSubmissionsAsync(merchantId, ct);
        return subs.Select(s => new SubmissionDto(
            s.Id, s.NameAr, s.NameEn, s.Barcode, s.UnitSize, s.SuggestedTaxClass, (int)s.Status, s.CreatedAt)).ToList();
    }
}
