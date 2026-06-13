using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Dtos;
using Sallimni.Application.Models;
using Sallimni.Infrastructure;
using Sallimni.Infrastructure.Services;

namespace Sallimni.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly SallimniDbContext _db;
    private readonly OrderService _orders;

    public OrdersController(SallimniDbContext db, OrderService orders)
    {
        _db = db;
        _orders = orders;
    }

    /// <summary>تأكيد الطلب: تقسيم بالأرخص + لقطة أسعار + ETA + إلحاق بموجة.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place([FromBody] PlaceOrderRequest req, CancellationToken ct)
    {
        var cart = req.Items.Select(i => new CartLine(i.ProductId, i.Quantity)).ToList();
        try
        {
            var result = await _orders.PlaceOrderAsync(
                req.CustomerId, req.AddressId, cart, DateTimeOffset.UtcNow, req.PaymentMethod, ct);

            var dto = await BuildOrderDtoAsync(result.Order.Id, ct);
            // إرفاق غير المتوفر من نتيجة التقسيم.
            var unfulfilled = result.Split.Unfulfilled
                .Select(u => new UnfulfilledDto(u.ProductId, u.Quantity, u.Reason)).ToList();
            dto = dto! with { Unfulfilled = unfulfilled };
            return CreatedAtAction(nameof(GetById), new { id = result.Order.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>تفاصيل الطلب الأب وطلباته الفرعية.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await BuildOrderDtoAsync(id, ct);
        return dto is null ? NotFound() : dto;
    }

    private async Task<OrderDto?> BuildOrderDtoAsync(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.SubOrders).ThenInclude(s => s.Merchant)
            .Include(o => o.SubOrders).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return null;

        var subs = order.SubOrders.Select(s => new SubOrderDto(
            s.Id, s.MerchantId, s.Merchant?.Name ?? "", s.SubtotalInclTax, s.TaxTotal,
            s.CommissionRate, s.CommissionAmount, s.MerchantPayout,
            s.Items.Select(it => new OrderItemDto(
                it.ProductId, it.ProductNameSnapshot, it.Quantity,
                it.UnitPriceInclTax, it.UnitTaxAmount, it.TaxClass, it.Status)).ToList()
        )).ToList();

        return new OrderDto(order.Id, order.Status, order.SubtotalInclTax, order.TaxTotal,
            order.GrandTotal, order.EstimatedDeliveryAt, order.WaveId, subs, new List<UnfulfilledDto>());
    }
}
