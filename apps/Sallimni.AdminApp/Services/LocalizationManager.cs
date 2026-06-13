using System.ComponentModel;
using System.Globalization;

namespace Sallimni.AdminApp.Services;

/// <summary>مدير الترجمة (عربي/إنجليزي) لتطبيق الإدارة.</summary>
public class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _lang = "ar";

    public string Language
    {
        get => _lang;
        set
        {
            if (_lang == value) return;
            _lang = value;
            var culture = new CultureInfo(value);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRtl)));
        }
    }

    public bool IsRtl => _lang == "ar";

    public string this[string key]
        => Strings.Table.TryGetValue(key, out var pair) ? (_lang == "ar" ? pair.Ar : pair.En) : key;

    public void Toggle() => Language = _lang == "ar" ? "en" : "ar";
}

internal static class Strings
{
    public record Pair(string Ar, string En);

    public static readonly Dictionary<string, Pair> Table = new()
    {
        ["app.title"] = new("سلّمني إدارة", "Sallimni Admin"),
        ["tab.approvals"] = new("الاعتمادات", "Approvals"),
        ["tab.tasks"] = new("المهام", "Tasks"),
        ["tab.settlements"] = new("التسويات", "Settlements"),
        ["tab.settings"] = new("الإعدادات", "Settings"),
        ["common.currency"] = new("د.أ", "JOD"),

        ["appr.title"] = new("طلبات إضافة الأصناف", "Product requests"),
        ["appr.empty"] = new("لا طلبات معلّقة", "No pending requests"),
        ["appr.barcode"] = new("باركود", "Barcode"),
        ["appr.tax"] = new("ضريبة", "Tax"),
        ["appr.category"] = new("التصنيف", "Category"),
        ["appr.approve"] = new("اعتماد", "Approve"),
        ["appr.reject"] = new("رفض", "Reject"),

        ["tasks.title"] = new("الموجات ومهام التوصيل", "Waves & delivery tasks"),
        ["tasks.wave"] = new("موجة", "Wave"),
        ["tasks.orders"] = new("طلبات", "orders"),
        ["tasks.subs"] = new("فرعية", "sub-orders"),
        ["tasks.collection"] = new("إنشاء مهمة تجميع", "Create collection task"),
        ["tasks.distribution"] = new("إنشاء مهمة توزيع", "Create distribution task"),
        ["tasks.collectionDone"] = new("مهمة تجميع ✔", "Collection ✔"),
        ["tasks.distributionDone"] = new("مهمة توزيع ✔", "Distribution ✔"),
        ["tasks.list"] = new("المهام", "Tasks"),
        ["tasks.assign"] = new("إسناد سائق", "Assign driver"),
        ["tasks.stops"] = new("محطات", "stops"),
        ["tasks.colltype"] = new("تجميع: متاجر ← مستودع", "Collection: stores → hub"),
        ["tasks.disttype"] = new("توزيع: مستودع ← زبائن", "Distribution: hub → customers"),

        ["sett.title"] = new("تسويات التجار", "Merchant settlements"),
        ["sett.subtotal"] = new("الإجمالي", "Subtotal"),
        ["sett.commission"] = new("العمولة", "Commission"),
        ["sett.payout"] = new("المستحق للتاجر", "Merchant payout"),
        ["sett.settle"] = new("تسوية ودفع", "Settle & pay"),
        ["sett.settled"] = new("مُسوّى", "Settled"),

        ["cfg.title"] = new("الإعدادات", "Settings"),
        ["cfg.commission"] = new("نسبة العمولة الافتراضية", "Default commission rate"),
        ["cfg.commissionHint"] = new("مثال 0.10 = 10%", "e.g. 0.10 = 10%"),
        ["cfg.wave"] = new("إعداد الموجات (دقائق)", "Wave config (minutes)"),
        ["cfg.interval"] = new("مدة الموجة", "Wave interval"),
        ["cfg.gap"] = new("فجوة التوزيع", "Distribution gap"),
        ["cfg.prep"] = new("تحضير التاجر", "Prep time"),
        ["cfg.transit"] = new("زمن التوصيل", "Transit time"),
        ["cfg.maxcust"] = new("حد زبائن السائق", "Max customers/driver"),
        ["cfg.save"] = new("حفظ", "Save"),
        ["cfg.saved"] = new("تم الحفظ", "Saved"),
        ["cfg.language"] = new("اللغة", "Language"),
        ["cfg.arabic"] = new("العربية", "Arabic"),
        ["cfg.english"] = new("English", "English"),

        ["common.ok"] = new("حسناً", "OK"),
        ["common.yes"] = new("نعم", "Yes"),
        ["common.no"] = new("لا", "No"),
        ["common.loading"] = new("جارٍ التحميل…", "Loading…"),
        ["common.done"] = new("تم", "Done"),
    };
}
