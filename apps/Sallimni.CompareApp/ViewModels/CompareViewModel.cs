using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CompareApp.Models;
using Sallimni.CompareApp.Services;

namespace Sallimni.CompareApp.ViewModels;

/// <summary>
/// شاشة واحدة: مسح الباركود ومقارنة سعره عبر متاجر البقالة (حيّ + مفهرَس).
/// مسح الكاميرا عبر BarcodeScanning.Native.Maui، والبيانات من /api/scan-compare.
/// </summary>
public partial class CompareViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public CompareViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasCompared;
    [ObservableProperty] private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    // بطاقة النتيجة الواحدة (الأرخص) — كل حقولها مشتقّة من أرخص عرض.
    [ObservableProperty] private bool _hasProduct;
    [ObservableProperty] private string? _productName;
    [ObservableProperty] private string? _productImageUrl;
    [ObservableProperty] private bool _productHasImage;
    [ObservableProperty] private string? _cheapestPriceText;
    [ObservableProperty] private string? _storeName;
    [ObservableProperty] private bool _inStock;
    [ObservableProperty] private string _availabilityText = "";
    [ObservableProperty] private string? _savingsText;
    [ObservableProperty] private bool _hasSavings;
    [ObservableProperty] private string? _distanceText;
    [ObservableProperty] private bool _hasDistance;

    private Location? _userLocation; // موقع المستخدم (يُجلب مرّة ويُخزَّن).

    /// <summary>تشغيل/إيقاف مسح الكاميرا (يطلب الإذن عند التشغيل).</summary>
    [RelayCommand]
    private async Task ToggleScanAsync()
    {
        if (!IsScanning)
        {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                ErrorMessage = "تعذّر الوصول للكاميرا — فعّل الإذن من إعدادات الجهاز.";
                return;
            }
            // أخفِ نتيجة المسح السابقة حتى تظهر الكاميرا (وإلّا غطّتها البطاقة) — يتيح إعادة المسح.
            ErrorMessage = null;
            HasProduct = false;
            HasCompared = false;
        }
        IsScanning = !IsScanning;
    }

    /// <summary>تُستدعى من الصفحة عند التقاط باركود من الكاميرا.</summary>
    public async Task OnBarcodeScannedAsync(string code)
    {
        code = (code ?? "").Trim();
        if (code.Length == 0) return;
        IsScanning = false;
        Barcode = code;
        await CompareAsync();
    }

    [RelayCommand]
    private async Task CompareAsync()
    {
        var code = (Barcode ?? "").Trim();
        if (code.Length == 0 || IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;
        HasCompared = false;
        HasProduct = false;
        HasSavings = false;
        SavingsText = null;
        HasDistance = false;
        DistanceText = null;

        try
        {
            var resp = await _api.ScanCompareAsync(code);
            var list = resp?.Results ?? new List<LiveScanDto>();

            // نتيجة واحدة فقط: الأرخص = أقلّ سعر فعلي بين المتوفّر (وإلا أوّل نتيجة).
            // التوفير يُحسب من كامل المتاجر قبل التصفية، ثم نعرض الأرخص وحده.
            var cheapest = list.Where(r => r.InStock).OrderBy(r => r.EffectivePrice).FirstOrDefault()
                           ?? list.FirstOrDefault();
            if (cheapest is not null)
            {
                ProductName = cheapest.Name;
                ProductImageUrl = cheapest.HasImage ? cheapest.ImageUrl : null;
                ProductHasImage = cheapest.HasImage;
                CheapestPriceText = cheapest.PriceText;
                StoreName = cheapest.Store;
                InStock = cheapest.InStock;
                AvailabilityText = cheapest.AvailabilityText;
                HasProduct = true;

                if (list.Count > 1)
                {
                    var save = list.Max(r => r.EffectivePrice) - cheapest.EffectivePrice;
                    if (save > 0)
                    {
                        SavingsText = $"وفّر {save:0.00} د.أ";
                        HasSavings = true;
                    }
                }

                await SetDistanceAsync(cheapest);
            }

            if (list.Count == 0)
                ErrorMessage = "لم يُعثر على المنتج في أي متجر.";
        }
        catch
        {
            ErrorMessage = "تعذّر جلب النتائج. تحقّق من الاتصال وحاول مجددًا.";
        }
        finally
        {
            IsBusy = false;
            HasCompared = true;
        }
    }

    /// <summary>يحسب كم يبعد المتجر الأرخص عن المستخدم ويضبط نصّ المسافة (best-effort).</summary>
    private async Task SetDistanceAsync(LiveScanDto cheapest)
    {
        if (!cheapest.HasLocation) return;               // المتجر بلا إحداثيات.
        var me = await EnsureUserLocationAsync();
        if (me is null) return;                          // تعذّر تحديد الموقع — نتجاهل بصمت.

        var km = Location.CalculateDistance(
            me.Latitude, me.Longitude,
            cheapest.Latitude!.Value, cheapest.Longitude!.Value, DistanceUnits.Kilometers);

        DistanceText = km < 1 ? $"يبعد {km * 1000:0} م عنك" : $"يبعد {km:0.0} كم عنك";
        HasDistance = true;
    }

    /// <summary>يطلب إذن الموقع ويجلبه مرّة واحدة ثم يخزّنه (آخر موقع معروف أوّلاً للسرعة).</summary>
    private async Task<Location?> EnsureUserLocationAsync()
    {
        if (_userLocation is not null) return _userLocation;
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted) return null;

            _userLocation = await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));
            return _userLocation;
        }
        catch { return null; }
    }
}
