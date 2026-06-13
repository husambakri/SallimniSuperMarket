using Sallimni.Application.Models;
using Sallimni.Domain.Enums;

namespace Sallimni.Application.Services;

/// <summary>
/// محرّك تقسيم الطلب — قلب النظام (قسم 4).
/// يسند كل صنف للتاجر <b>الأرخص</b> المتوفر، مع <b>Fallback</b> للأرخص الذي يليه عند النفاد،
/// ويثبّت <b>لقطة سعر</b> لكل صنف لحظة التأكيد. منطق نقي بلا اعتماد على قاعدة بيانات.
/// </summary>
public class OrderSplitEngine
{
    /// <param name="cart">أسطر السلّة.</param>
    /// <param name="products">بطاقات الأصناف (للاسم وفئة الضريبة).</param>
    /// <param name="offers">عروض التجار لكل صنف (السعر شامل الضريبة + المخزون).</param>
    /// <param name="commissionRateResolver">دالة تُرجع نسبة العمولة لتاجر معيّن.</param>
    public SplitResult Split(
        IEnumerable<CartLine> cart,
        IReadOnlyDictionary<Guid, ProductInfo> products,
        IEnumerable<MerchantOffer> offers,
        Func<Guid, decimal> commissionRateResolver)
    {
        var result = new SplitResult();

        // عروض كل صنف مرتّبة بالأرخص (السعر شامل الضريبة)، ثم الأعلى مخزوناً عند التعادل.
        var offersByProduct = offers
            .Where(o => o.IsAvailable && o.StockQty > 0)
            .GroupBy(o => o.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(o => o.PriceInclTax).ThenByDescending(o => o.StockQty).ToList());

        // تجميع الأسناد لكل تاجر.
        var byMerchant = new Dictionary<Guid, SplitSubOrder>();

        foreach (var line in cart)
        {
            if (line.Quantity <= 0) continue;

            if (!products.TryGetValue(line.ProductId, out var product))
            {
                result.Unfulfilled.Add(new UnfulfilledLine(line.ProductId, line.Quantity, "صنف غير معروف"));
                continue;
            }

            if (!offersByProduct.TryGetValue(line.ProductId, out var candidates) || candidates.Count == 0)
            {
                result.Unfulfilled.Add(new UnfulfilledLine(line.ProductId, line.Quantity, "غير متوفر لدى أي تاجر"));
                continue;
            }

            // الأرخص الذي يكفي مخزونه الكمّية المطلوبة (Fallback عند عدم الكفاية).
            var cheapest = candidates[0];
            var chosen = candidates.FirstOrDefault(o => o.StockQty >= line.Quantity);

            if (chosen is null)
            {
                // لا تاجر يملك الكمّية كاملة — يُسند للأرخص المتوفر (تسليم جزئي يُعالَج لاحقاً عند الفرز).
                chosen = cheapest;
                result.Unfulfilled.Add(new UnfulfilledLine(
                    line.ProductId, line.Quantity - chosen.StockQty,
                    "كمية غير كافية لدى الأرخص — جزء غير متوفر"));
            }

            var reassigned = chosen.MerchantId != cheapest.MerchantId;
            var unitTax = TaxCalculator.TaxFromInclusive(chosen.PriceInclTax, product.TaxClass);

            if (!byMerchant.TryGetValue(chosen.MerchantId, out var sub))
            {
                sub = new SplitSubOrder { MerchantId = chosen.MerchantId };
                byMerchant[chosen.MerchantId] = sub;
            }

            sub.Items.Add(new SplitItem(
                product.ProductId,
                product.Name,
                Math.Min(line.Quantity, chosen.StockQty),
                chosen.PriceInclTax,
                product.TaxClass,
                unitTax,
                reassigned));
        }

        // احتساب العمولة لكل طلب فرعي على الإجمالي شامل الضريبة (قرار 2).
        foreach (var sub in byMerchant.Values)
        {
            sub.CommissionRate = commissionRateResolver(sub.MerchantId);
            sub.CommissionAmount = Math.Round(sub.SubtotalInclTax * sub.CommissionRate, 2, MidpointRounding.AwayFromZero);
            sub.MerchantPayout = sub.SubtotalInclTax - sub.CommissionAmount;
            result.SubOrders.Add(sub);
        }

        return result;
    }
}
