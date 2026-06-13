using Microsoft.EntityFrameworkCore;
using Sallimni.Application.Models;
using Sallimni.Application.Services;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;

namespace Sallimni.Infrastructure.Services;

/// <summary>نتيجة تأكيد الطلب — الطلب الأب + ملخّص التقسيم وغير المتوفر.</summary>
public record PlaceOrderResult(Order Order, SplitResult Split, EtaEstimate Eta);

/// <summary>
/// تنسيق تأكيد الطلب: تحميل العروض ← محرّك التقسيم ← لقطة الأسعار ← ETA ← إلحاق بموجة ← حفظ.
/// كل المعالجة على الخادم (متطلب غير وظيفي، قسم 12).
/// </summary>
public class OrderService
{
    private readonly SallimniDbContext _db;
    private readonly OrderSplitEngine _engine;
    private readonly CommissionService _commission;
    private readonly WaveService _waves;

    public OrderService(
        SallimniDbContext db, OrderSplitEngine engine,
        CommissionService commission, WaveService waves)
    {
        _db = db;
        _engine = engine;
        _commission = commission;
        _waves = waves;
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(
        Guid customerId, Guid addressId, IReadOnlyList<CartLine> cart,
        DateTimeOffset now, PaymentMethod paymentMethod = PaymentMethod.Cash,
        CancellationToken ct = default)
    {
        if (cart.Count == 0)
            throw new InvalidOperationException("السلّة فارغة.");

        var address = await _db.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, ct)
            ?? throw new InvalidOperationException("العنوان غير موجود لهذا الزبون.");

        var productIds = cart.Select(l => l.ProductId).Distinct().ToList();

        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id, p => new ProductInfo(p.Id, p.NameAr, p.TaxClass), ct);

        var offers = await _db.MerchantProducts
            .Where(mp => productIds.Contains(mp.ProductId) && mp.IsAvailable && mp.StockQty > 0)
            .Select(mp => new MerchantOffer(mp.MerchantId, mp.ProductId, mp.Price, mp.StockQty, mp.IsAvailable))
            .ToListAsync(ct);

        var resolver = await _commission.BuildResolverAsync(ct);

        // قلب النظام: التقسيم بالأرخص + Fallback + لقطة السعر.
        var split = _engine.Split(cart, products, offers, resolver);
        if (split.SubOrders.Count == 0)
            throw new InvalidOperationException("تعذّر تقسيم الطلب — لا أصناف متوفرة.");

        // ETA عند الطلب (Wave-based) — أبطأ تاجر يحدّد max(T_prep).
        var cfg = await _waves.GetConfigAsync(ct);
        var maxPrep = cfg.DefaultPrepMinutes; // افتراضي موحّد؛ يمكن لاحقاً لكل تاجر
        var eta = EtaCalculator.Estimate(
            now, maxPrep, cfg.DefaultTransitMinutes,
            cfg.WaveIntervalMinutes, cfg.DistributionGapMinutes);

        var wave = await _waves.GetOrCreateWaveAsync(eta.CollectionWaveAt, cfg, ct);

        var order = new Order
        {
            CustomerId = customerId,
            AddressId = addressId,
            DeliveryLatitude = address.Latitude,
            DeliveryLongitude = address.Longitude,
            Status = OrderStatus.Split,
            Wave = wave,
            PaymentMethod = paymentMethod,
            PlacedAt = now,
            EstimatedDeliveryAt = eta.Eta,
            SubtotalInclTax = split.SubtotalInclTax,
            TaxTotal = split.TaxTotal,
            GrandTotal = split.GrandTotal
        };

        foreach (var s in split.SubOrders)
        {
            var sub = new SubOrder
            {
                MerchantId = s.MerchantId,
                Status = SubOrderStatus.Pending,
                SubtotalInclTax = s.SubtotalInclTax,
                TaxTotal = s.TaxTotal,
                CommissionRate = s.CommissionRate,
                CommissionAmount = s.CommissionAmount,
                MerchantPayout = s.MerchantPayout
            };
            foreach (var it in s.Items)
            {
                sub.Items.Add(new OrderItem
                {
                    ProductId = it.ProductId,
                    ProductNameSnapshot = it.ProductName,
                    Quantity = it.Quantity,
                    UnitPriceInclTax = it.UnitPriceInclTax,
                    TaxClass = it.TaxClass,
                    UnitTaxAmount = it.UnitTaxAmount,
                    Status = it.ReassignedDueToStock
                        ? OrderItemStatus.OutOfStockReassigned
                        : OrderItemStatus.Active
                });

                // خصم المخزون عند التاجر الفائز.
                var mp = await _db.MerchantProducts
                    .FirstOrDefaultAsync(x => x.MerchantId == s.MerchantId && x.ProductId == it.ProductId, ct);
                if (mp is not null)
                {
                    mp.StockQty = Math.Max(0, mp.StockQty - it.Quantity);
                    if (mp.StockQty == 0) mp.IsAvailable = false;
                }
            }
            order.SubOrders.Add(sub);
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        return new PlaceOrderResult(order, split, eta);
    }
}
