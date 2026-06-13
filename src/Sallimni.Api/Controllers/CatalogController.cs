using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Dtos;
using Sallimni.Infrastructure;
using Sallimni.Infrastructure.Services;

namespace Sallimni.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly SallimniDbContext _db;
    private readonly BarcodeService _barcode;

    public CatalogController(SallimniDbContext db, BarcodeService barcode)
    {
        _db = db;
        _barcode = barcode;
    }

    /// <summary>الفئات (شبكة الأصناف) مع عدد المنتجات وأيقونة كل فئة.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<CatalogCategoryDto>>> GetCategories(CancellationToken ct)
        => await _db.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new CatalogCategoryDto(c.Id, c.NameAr, c.NameEn, c.Icon, c.Products.Count(p => p.IsActive)))
            .ToListAsync(ct);

    /// <summary>المنتجات (اختيارياً ضمن فئة أو ببحث نصّي) مع أرخص سعر، أعلى سعر، ونسبة التوفير.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<List<ProductDto>>> GetProducts(
        [FromQuery] Guid? categoryId, [FromQuery] string? q, CancellationToken ct)
    {
        q = q?.Trim();
        var query = _db.Products.Where(p => p.IsActive);
        if (categoryId != null) query = query.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(p => p.NameAr.Contains(q) || p.NameEn.Contains(q) || (p.Barcode != null && p.Barcode.Contains(q)));

        var products = await query
            .OrderBy(p => p.NameAr)
            .Select(p => new ProdRow(p.Id, p.NameAr, p.NameEn, p.Barcode, p.UnitSize, p.ImageUrl, p.Emoji, p.TaxClass, p.CategoryId))
            .ToListAsync(ct);

        return await BuildProductDtosAsync(products, ct);
    }

    /// <summary>أوفر العروض: المنتجات الأعلى نسبة توفير (لشريط العروض في الرئيسية).</summary>
    [HttpGet("offers")]
    public async Task<ActionResult<List<ProductDto>>> GetOffers([FromQuery] int take = 10, CancellationToken ct = default)
    {
        var products = await _db.Products.Where(p => p.IsActive)
            .Select(p => new ProdRow(p.Id, p.NameAr, p.NameEn, p.Barcode, p.UnitSize, p.ImageUrl, p.Emoji, p.TaxClass, p.CategoryId))
            .ToListAsync(ct);

        var dtos = await BuildProductsListAsync(products, ct);
        return dtos
            .Where(p => p.SavingsPercent > 0)
            .OrderByDescending(p => p.SavingsPercent)
            .Take(take)
            .ToList();
    }

    private record ProdRow(Guid Id, string NameAr, string NameEn, string? Barcode, string? UnitSize,
        string? ImageUrl, string? Emoji, Domain.Enums.TaxClass TaxClass, Guid CategoryId);

    private async Task<List<ProductDto>> BuildProductsListAsync(List<ProdRow> products, CancellationToken ct)
    {
        var ids = products.Select(p => p.Id).ToList();
        var agg = await _db.MerchantProducts
            .Where(mp => ids.Contains(mp.ProductId) && mp.IsAvailable && mp.StockQty > 0)
            .GroupBy(mp => mp.ProductId)
            .Select(g => new { ProductId = g.Key, Min = g.Min(x => x.Price), Max = g.Max(x => x.Price) })
            .ToDictionaryAsync(x => x.ProductId, ct);

        return products.Select(p =>
        {
            agg.TryGetValue(p.Id, out var a);
            decimal? min = a?.Min;
            decimal? max = a?.Max;
            var savings = (max is > 0 && min is not null) ? (int)Math.Round((max.Value - min.Value) / max.Value * 100m) : 0;
            return new ProductDto(p.Id, p.NameAr, p.NameEn, p.Barcode, p.UnitSize, p.ImageUrl, p.Emoji,
                p.TaxClass, p.CategoryId, min, max, savings);
        }).ToList();
    }

    private async Task<ActionResult<List<ProductDto>>> BuildProductDtosAsync(List<ProdRow> products, CancellationToken ct)
        => await BuildProductsListAsync(products, ct);

    /// <summary>تفاصيل منتج للعرض في صفحة المنتج.</summary>
    [HttpGet("products/{id:guid}")]
    public async Task<ActionResult<ProductDetailDto>> GetProduct(Guid id, CancellationToken ct)
    {
        var p = await _db.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (p is null) return NotFound();

        var offers = await _db.MerchantProducts
            .Where(mp => mp.ProductId == id && mp.IsAvailable && mp.StockQty > 0)
            .Select(mp => mp.Price).ToListAsync(ct);
        decimal? min = offers.Count > 0 ? offers.Min() : null;
        decimal? max = offers.Count > 0 ? offers.Max() : null;
        var savings = (max is > 0 && min is not null) ? (int)Math.Round((max.Value - min.Value) / max.Value * 100m) : 0;

        return new ProductDetailDto(p.Id, p.NameAr, p.NameEn, p.Barcode, p.UnitSize, p.ImageUrl, p.Emoji,
            p.Description, p.TaxClass, p.CategoryId, p.Category?.NameAr ?? "", min, max, savings);
    }

    /// <summary>تقديم صورة الصنف المخزّنة في القاعدة (أو 404 لترجع التطبيقات للرمز التعبيري).</summary>
    [HttpGet("products/{id:guid}/image")]
    public async Task<IActionResult> GetProductImage(Guid id, CancellationToken ct)
    {
        var img = await _db.Products
            .Where(p => p.Id == id && p.ImageData != null)
            .Select(p => new { p.ImageData, p.ImageContentType })
            .FirstOrDefaultAsync(ct);
        if (img?.ImageData is null) return NotFound();
        Response.Headers.CacheControl = "public,max-age=86400";
        return File(img.ImageData, img.ImageContentType ?? "image/jpeg");
    }

    /// <summary>فحص السعر بالباركود (Scan-to-Compare) — سعرنا + نسبة التوفير.</summary>
    [HttpGet("barcode/{code}")]
    public async Task<ActionResult<BarcodeLookupDto>> Lookup(string code, [FromQuery] Guid? customerId, CancellationToken ct)
    {
        var r = await _barcode.LookupAsync(code, customerId, logScan: true, ct);
        return new BarcodeLookupDto(r.Found, r.ProductId, r.NameAr, r.NameEn, r.ImageUrl, r.Emoji,
            r.OurPriceInclTax, r.RegularPriceInclTax, r.SavingsPercent);
    }

    [HttpGet("merchants")]
    public async Task<IActionResult> GetMerchants(CancellationToken ct)
        => Ok(await _db.Merchants.Select(m => new { m.Id, m.Name, m.IsSalesTaxRegistered }).ToListAsync(ct));

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(CancellationToken ct)
        => Ok(await _db.Customers
            .Select(c => new { c.Id, c.Name, Addresses = c.Addresses.Select(a => new { a.Id, a.Label, a.IsDefault }) })
            .ToListAsync(ct));
}
