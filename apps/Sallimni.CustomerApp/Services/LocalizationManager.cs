using System.ComponentModel;
using System.Globalization;

namespace Sallimni.CustomerApp.Services;

/// <summary>
/// مدير الترجمة (عربي/إنجليزي) — قسم 12 (دعم العربية/الإنجليزية).
/// مفرد يعرّض مفاتيح النصوص عبر indexer؛ تبديل اللغة يحدّث كل الارتباطات حيّاً.
/// </summary>
public class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _lang = "ar";

    /// <summary>اللغة الحالية: "ar" أو "en".</summary>
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
            // تحديث كل النصوص (المرتبطة بـ Language عبر المحوِّل) + اتجاه الكتابة.
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
        ["app.title"] = new("سلّمني", "Sallimni"),
        ["tab.catalog"] = new("الأصناف", "Catalog"),
        ["tab.scan"] = new("فحص السعر", "Scan"),
        ["tab.cart"] = new("السلّة", "Cart"),
        ["tab.settings"] = new("الإعدادات", "Settings"),

        ["tab.shop"] = new("تسوّق", "Shop"),
        ["home.search"] = new("ابحث عن منتج…", "Search products…"),
        ["home.offers"] = new("أوفر العروض", "Best deals"),
        ["home.freedelivery"] = new("توصيل مجاني", "Free delivery"),
        ["home.cart"] = new("السلّة", "Cart"),
        ["catalog.title"] = new("وفّر مع سلّمني", "Save with Sallimni"),
        ["catalog.subtitle"] = new("تسوّق حسب الصنف ووفّر على كل منتج", "Shop by category and save on every item"),
        ["catalog.cheapest"] = new("أرخص سعر", "Best price"),
        ["catalog.add"] = new("أضف للسلة", "Add"),
        ["catalog.unavailable"] = new("غير متوفر", "Unavailable"),
        ["catalog.products"] = new("منتجات", "products"),
        ["detail.description"] = new("الوصف", "Description"),
        ["detail.regular"] = new("السعر العادي", "Regular price"),
        ["detail.add"] = new("أضف إلى السلة", "Add to cart"),
        ["common.currency"] = new("د.أ", "JOD"),

        ["scan.title"] = new("فحص السعر بالباركود", "Scan to Compare"),
        ["scan.hint"] = new("امسح/أدخل رقم الباركود لرؤية سعرنا فوراً", "Scan or enter a barcode to see our price"),
        ["scan.placeholder"] = new("رقم الباركود", "Barcode number"),
        ["scan.button"] = new("افحص السعر", "Check price"),
        ["scan.ourprice"] = new("سعرنا", "Our price"),
        ["scan.notfound"] = new("غير متوفر حالياً", "Not available right now"),
        ["scan.camera_note"] = new("مسح الكاميرا الحيّ يُفعَّل على الأجهزة (يتطلب إذن كاميرا).",
            "Live camera scan is enabled on devices (requires camera permission)."),

        ["cart.title"] = new("سلّتي", "My Cart"),
        ["cart.empty"] = new("سلّتك فارغة", "Your cart is empty"),
        ["cart.total"] = new("الإجمالي (شامل الضريبة)", "Total (incl. tax)"),
        ["cart.checkout"] = new("تأكيد الطلب", "Place order"),
        ["cart.address"] = new("عنوان التوصيل", "Delivery address"),
        ["cart.payment"] = new("الدفع عند الاستلام", "Pay on delivery"),
        ["cart.cash"] = new("كاش", "Cash"),
        ["cart.cliq"] = new("كليك", "CliQ"),

        ["order.title"] = new("طلبي", "My Order"),
        ["order.placed"] = new("تم تأكيد طلبك", "Order placed"),
        ["order.eta"] = new("الوصول المتوقّع", "Estimated arrival"),
        ["order.wave_note"] = new("سيتم تجميع طلبك ضمن الموجة القادمة", "Your order will be collected in the next wave"),
        ["order.split_note"] = new("وزّعنا أصنافك على التجار الأرخص لتوفير المال", "We split your items across the cheapest merchants to save you money"),
        ["order.from"] = new("من", "from"),
        ["order.commission"] = new("عمولة المنصّة", "Platform commission"),
        ["order.unfulfilled"] = new("أصناف غير متوفرة", "Unavailable items"),
        ["order.track"] = new("تتبّع الطلب", "Track order"),
        ["order.status"] = new("الحالة", "Status"),

        ["settings.title"] = new("الإعدادات", "Settings"),
        ["settings.language"] = new("اللغة", "Language"),
        ["settings.arabic"] = new("العربية", "Arabic"),
        ["settings.english"] = new("English", "English"),
        ["settings.customer"] = new("الحساب التجريبي", "Demo account"),
        ["settings.delete"] = new("حذف الحساب", "Delete account"),
        ["settings.delete_confirm"] = new("سيتم حذف حسابك وبياناتك نهائياً. متابعة؟", "Your account and data will be permanently deleted. Continue?"),
        ["settings.deleted"] = new("تم حذف حسابك", "Your account was deleted"),
        ["settings.legal"] = new("القانونية", "Legal"),
        ["settings.privacy"] = new("سياسة الخصوصية", "Privacy Policy"),
        ["settings.terms"] = new("شروط الاستخدام", "Terms of Use"),
        ["settings.server"] = new("عنوان الخادم", "Server URL"),

        ["common.yes"] = new("نعم", "Yes"),
        ["common.no"] = new("لا", "No"),
        ["common.ok"] = new("حسناً", "OK"),
        ["common.error"] = new("خطأ", "Error"),
        ["common.loading"] = new("جارٍ التحميل…", "Loading…"),
        ["common.retry"] = new("إعادة المحاولة", "Retry"),
    };
}
