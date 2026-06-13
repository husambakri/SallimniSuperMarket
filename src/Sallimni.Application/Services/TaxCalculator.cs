using Sallimni.Domain.Enums;

namespace Sallimni.Application.Services;

/// <summary>
/// أدوات الضريبة. الأسعار في النظام مخزّنة <b>شاملة الضريبة</b> (قرار 3)،
/// والضريبة تُشتقّ منها للعرض في الفاتورة فقط.
/// </summary>
public static class TaxCalculator
{
    /// <summary>نسبة الضريبة العشرية لشريحة معيّنة (16% → 0.16). معفى/0% → صفر.</summary>
    public static decimal RateOf(TaxClass taxClass) => taxClass switch
    {
        TaxClass.Exempt => 0m,
        TaxClass.Zero => 0m,
        TaxClass.Two => 0.02m,
        TaxClass.Four => 0.04m,
        TaxClass.Five => 0.05m,
        TaxClass.Ten => 0.10m,
        TaxClass.Sixteen => 0.16m,
        _ => 0.16m
    };

    /// <summary>
    /// قيمة الضريبة المضمّنة داخل سعر شامل الضريبة.
    /// tax = priceInclTax * rate / (1 + rate). مقرّبة لمنزلتين.
    /// </summary>
    public static decimal TaxFromInclusive(decimal priceInclTax, TaxClass taxClass)
    {
        var rate = RateOf(taxClass);
        if (rate == 0m) return 0m;
        var tax = priceInclTax * rate / (1m + rate);
        return Math.Round(tax, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>السعر قبل الضريبة = الشامل - الضريبة.</summary>
    public static decimal NetFromInclusive(decimal priceInclTax, TaxClass taxClass)
        => priceInclTax - TaxFromInclusive(priceInclTax, taxClass);
}
