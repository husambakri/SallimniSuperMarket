using System.ComponentModel;
using System.Globalization;

namespace Sallimni.MerchantApp.Services;

/// <summary>مدير الترجمة (عربي/إنجليزي) لتطبيق التاجر — تبديل اللغة يحدّث الارتباطات حيّاً.</summary>
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
        => Strings.Table.TryGetValue(key, out var pair)
            ? (_lang == "ar" ? pair.Ar : pair.En)
            : key;

    public void Toggle() => Language = _lang == "ar" ? "en" : "ar";
}

internal static class Strings
{
    public record Pair(string Ar, string En);

    public static readonly Dictionary<string, Pair> Table = new()
    {
        ["app.title"] = new("سلّمني تاجر", "Sallimni Merchant"),
        ["tab.inventory"] = new("المخزون والأسعار", "Inventory"),
        ["tab.orders"] = new("الطلبات الواردة", "Orders"),
        ["tab.submit"] = new("إضافة صنف", "Add product"),
        ["tab.settings"] = new("الإعدادات", "Settings"),

        ["inv.title"] = new("ربط مخزونك وأسعارك", "Link your stock & prices"),
        ["inv.linked"] = new("مربوط", "Linked"),
        ["inv.notlinked"] = new("غير مربوط", "Not linked"),
        ["inv.price"] = new("السعر (شامل الضريبة)", "Price (incl. tax)"),
        ["inv.stock"] = new("الكمية", "Stock"),
        ["inv.available"] = new("متاح للبيع", "Available"),
        ["inv.save"] = new("حفظ", "Save"),
        ["inv.saved"] = new("تم الحفظ", "Saved"),
        ["common.currency"] = new("د.أ", "JOD"),

        ["orders.title"] = new("الطلبات الفرعية الواردة", "Incoming sub-orders"),
        ["orders.empty"] = new("لا طلبات حالياً", "No orders yet"),
        ["orders.total"] = new("الإجمالي", "Total"),
        ["orders.payout"] = new("صافي لك (بعد العمولة)", "Your payout (after commission)"),
        ["orders.startprep"] = new("بدء التحضير", "Start preparing"),
        ["orders.ready"] = new("جاهز للاستلام", "Ready for pickup"),
        ["orders.status"] = new("الحالة", "Status"),
        ["orders.order"] = new("طلب", "Order"),

        ["submit.title"] = new("طلب إضافة صنف جديد", "Request a new product"),
        ["submit.hint"] = new("يدخل الطلب طابور اعتماد الإدارة", "Goes to the admin approval queue"),
        ["submit.namear"] = new("الاسم بالعربية", "Arabic name"),
        ["submit.nameen"] = new("الاسم بالإنجليزية", "English name"),
        ["submit.barcode"] = new("الباركود", "Barcode"),
        ["submit.unit"] = new("الوحدة/الحجم", "Unit/size"),
        ["submit.tax"] = new("شريحة الضريبة المقترحة", "Suggested tax class"),
        ["submit.send"] = new("إرسال الطلب", "Submit"),
        ["submit.sent"] = new("تم إرسال الطلب للإدارة", "Sent to admin"),
        ["submit.mine"] = new("طلباتي السابقة", "My submissions"),

        ["settings.title"] = new("الإعدادات", "Settings"),
        ["settings.language"] = new("اللغة", "Language"),
        ["settings.arabic"] = new("العربية", "Arabic"),
        ["settings.english"] = new("English", "English"),
        ["settings.merchant"] = new("المتجر الحالي", "Current merchant"),
        ["settings.taxreg"] = new("مسجّل بضريبة المبيعات", "Sales-tax registered"),
        ["settings.server"] = new("عنوان الخادم", "Server URL"),
        ["settings.delete"] = new("حذف الحساب", "Delete account"),
        ["settings.delete_confirm"] = new("سيتم حذف حسابك وبياناتك نهائياً. متابعة؟", "Your account and data will be permanently deleted. Continue?"),
        ["settings.deleted"] = new("تم حذف حسابك", "Your account was deleted"),

        ["status.0"] = new("بانتظار التحضير", "Pending"),
        ["status.1"] = new("قيد التحضير", "Preparing"),
        ["status.2"] = new("استلمه السائق", "Picked up"),
        ["status.3"] = new("في المستودع", "At hub"),
        ["status.4"] = new("خرج للتوصيل", "Out for delivery"),
        ["status.5"] = new("مُسلَّم", "Delivered"),
        ["status.6"] = new("مُسوّى", "Settled"),
        ["status.100"] = new("ملغى", "Cancelled"),

        ["subm.0"] = new("بانتظار الاعتماد", "Pending"),
        ["subm.1"] = new("معتمَد", "Approved"),
        ["subm.2"] = new("مرفوض", "Rejected"),

        ["common.yes"] = new("نعم", "Yes"),
        ["common.no"] = new("لا", "No"),
        ["common.ok"] = new("حسناً", "OK"),
        ["common.loading"] = new("جارٍ التحميل…", "Loading…"),
        ["common.error"] = new("خطأ", "Error"),
    };
}
