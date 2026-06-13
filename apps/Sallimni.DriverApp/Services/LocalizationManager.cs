using System.ComponentModel;
using System.Globalization;

namespace Sallimni.DriverApp.Services;

/// <summary>مدير الترجمة (عربي/إنجليزي) لتطبيق السائق.</summary>
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
            var c = new CultureInfo(value);
            CultureInfo.CurrentUICulture = c; CultureInfo.CurrentCulture = c;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRtl)));
        }
    }
    public bool IsRtl => _lang == "ar";
    public string this[string key] => Strings.Table.TryGetValue(key, out var p) ? (_lang == "ar" ? p.Ar : p.En) : key;
}

internal static class Strings
{
    public record Pair(string Ar, string En);
    public static readonly Dictionary<string, Pair> Table = new()
    {
        ["app.title"] = new("سلّمني سائق", "Sallimni Driver"),
        ["tab.tasks"] = new("مهامي", "My tasks"),
        ["tab.settings"] = new("الإعدادات", "Settings"),

        ["tasks.title"] = new("مهام التوصيل", "Delivery tasks"),
        ["tasks.empty"] = new("لا مهام مُسندة", "No assigned tasks"),
        ["tasks.collection"] = new("تجميع: متاجر ← مستودع", "Collection: stores → hub"),
        ["tasks.distribution"] = new("توزيع: مستودع ← زبائن", "Distribution: hub → customers"),
        ["tasks.stops"] = new("محطات", "stops"),
        ["tasks.open"] = new("فتح المهمة", "Open task"),
        ["tasks.status"] = new("الحالة", "Status"),

        ["detail.start"] = new("بدء المهمة", "Start task"),
        ["detail.complete"] = new("إنهاء المهمة", "Complete task"),
        ["detail.pickup"] = new("استلام (مسح QR)", "Pick up (scan QR)"),
        ["detail.deliver"] = new("تسليم وتحصيل (مسح QR)", "Deliver & collect (scan QR)"),
        ["detail.done"] = new("تم ✔", "Done ✔"),
        ["detail.cod"] = new("تحصيل", "Collect"),
        ["detail.items"] = new("أصناف", "items"),
        ["detail.scanned"] = new("تم المسح", "Scanned"),
        ["detail.eta"] = new("وصول متوقّع", "ETA"),

        ["settings.title"] = new("الإعدادات", "Settings"),
        ["settings.driver"] = new("السائق الحالي", "Current driver"),
        ["settings.language"] = new("اللغة", "Language"),
        ["settings.arabic"] = new("العربية", "Arabic"),
        ["settings.english"] = new("English", "English"),
        ["settings.server"] = new("عنوان الخادم", "Server URL"),
        ["settings.delete"] = new("حذف الحساب", "Delete account"),
        ["settings.delete_confirm"] = new("سيتم حذف حسابك وبياناتك نهائياً. متابعة؟", "Your account and data will be permanently deleted. Continue?"),
        ["settings.deleted"] = new("تم حذف حسابك", "Your account was deleted"),
        ["settings.legal"] = new("القانونية", "Legal"),
        ["settings.privacy"] = new("سياسة الخصوصية", "Privacy Policy"),
        ["settings.terms"] = new("شروط الاستخدام", "Terms of Use"),
        ["common.error"] = new("خطأ", "Error"),

        ["status.0"] = new("منشأة", "Created"),
        ["status.1"] = new("مُسندة", "Assigned"),
        ["status.2"] = new("قيد التنفيذ", "In progress"),
        ["status.3"] = new("مكتملة", "Completed"),
        ["status.100"] = new("ملغاة", "Cancelled"),

        ["common.currency"] = new("د.أ", "JOD"),
        ["common.ok"] = new("حسناً", "OK"),
        ["common.loading"] = new("جارٍ التحميل…", "Loading…"),
    };
}
