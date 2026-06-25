using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.ValidationApp.Models;
using Sallimni.ValidationApp.Services;

namespace Sallimni.ValidationApp.ViewModels;

/// <summary>
/// شاشة المسح: امسح الباركود → نحدّد أقرب فرع من موقعك → نعرض سعرنا المخزّن فيه →
/// إن طابق اكبس تأكيد، وإن اختلف عدّل القيمة الحقيقية ثم تأكيد. كل تأكيد يسجّل صفّاً
/// تاريخياً في الخادم (لا يُعدّل السعر الحيّ).
/// </summary>
public partial class ValidationViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ValidationViewModel(ApiClient api)
    {
        _api = api;
        _auditor = Preferences.Get("auditor", "");
    }

    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);
    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    /// <summary>اسم العامل (يُحفظ على الجهاز ويُرفق بكل تسجيل).</summary>
    [ObservableProperty] private string _auditor;
    partial void OnAuditorChanged(string value) => Preferences.Set("auditor", value ?? "");

    // بطاقة التحقّق (تظهر بعد المسح الناجح).
    [ObservableProperty] private bool _hasLookup;
    [ObservableProperty] private string? _branchName;
    [ObservableProperty] private string? _branchDistanceText;
    [ObservableProperty] private string? _productName;
    [ObservableProperty] private bool _hasOurPrice;
    public bool HasNoOurPrice => !HasOurPrice;
    partial void OnHasOurPriceChanged(bool value) => OnPropertyChanged(nameof(HasNoOurPrice));
    [ObservableProperty] private string? _expectedPriceText;
    [ObservableProperty] private string _actualPriceInput = "";

    private ValidationLookupDto? _lookup;
    private double _lat, _lng;

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
            ErrorMessage = null;
            StatusMessage = null;
            HasLookup = false;     // أخفِ البطاقة السابقة لتظهر الكاميرا.
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
        await LookupAsync();
    }

    /// <summary>استعلام الفرع والسعر المخزّن (يدوي أو بعد المسح).</summary>
    [RelayCommand]
    private async Task LookupAsync()
    {
        var code = (Barcode ?? "").Trim();
        if (code.Length == 0 || IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        HasLookup = false;
        _lookup = null;

        try
        {
            var me = await GetLocationAsync();
            if (me is null)
            {
                ErrorMessage = "تعذّر تحديد الموقع — فعّل خدمة الموقع لمعرفة الفرع.";
                return;
            }
            _lat = me.Latitude;
            _lng = me.Longitude;

            var res = await _api.LookupAsync(code, _lat, _lng);
            if (res is null || !res.BranchFound)
            {
                ErrorMessage = "لا يوجد فرع لنا قريب من موقعك الحالي.";
                return;
            }

            _lookup = res;
            BranchName = res.MerchantName;
            BranchDistanceText = res.DistanceText;
            ProductName = res.ProductFound ? res.ProductName : "صنف غير معروف لنا بهذا الباركود";
            HasOurPrice = res.HasOurPrice;
            ExpectedPriceText = res.ExpectedPriceText;
            // نملأ الحقل بسعرنا المخزّن: إن طابق الواقع يكبس تأكيد فقط، وإلّا يعدّله.
            ActualPriceInput = res.HasOurPrice ? res.ExpectedPrice!.Value.ToString("0.00", CultureInfo.InvariantCulture) : "";
            HasLookup = true;
        }
        catch
        {
            ErrorMessage = "تعذّر جلب البيانات. تحقّق من الاتصال وحاول مجددًا.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>يسجّل التحقّق: يحسب التطابق ويرسل صفّاً تاريخياً، ثم يجهّز للمسح التالي.</summary>
    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (_lookup is null || IsBusy) return;

        var raw = (ActualPriceInput ?? "").Trim().Replace('٫', '.').Replace(',', '.');
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var actual) || actual < 0)
        {
            ErrorMessage = "أدخل سعراً صحيحاً (مثال 1.25).";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _api.RecordAsync(new ValidationRecordRequest
            {
                MerchantId    = _lookup.MerchantId!.Value,
                MerchantName  = _lookup.MerchantName ?? "",
                BranchId      = _lookup.BranchId,
                ProductId     = _lookup.ProductId,
                Barcode       = (Barcode ?? "").Trim(),
                ProductName   = _lookup.ProductFound ? _lookup.ProductName : null,
                ExpectedPrice = _lookup.HasOurPrice ? _lookup.ExpectedPrice : null,
                ActualPrice   = actual,
                Latitude      = _lat,
                Longitude     = _lng,
                Auditor       = string.IsNullOrWhiteSpace(Auditor) ? null : Auditor.Trim(),
            });

            var match = _lookup.HasOurPrice && _lookup.ExpectedPrice == actual;
            StatusMessage = match ? "✓ سُجِّل: مطابق" : "✓ سُجِّل: مختلف — حُفظ الواقع";
            // تجهيز للمسح التالي.
            HasLookup = false;
            _lookup = null;
            Barcode = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>يطلب إذن الموقع ويجلب الموقع الحالي (آخر معروف أوّلاً للسرعة).</summary>
    private async Task<Location?> GetLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted) return null;

            return await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));
        }
        catch { return null; }
    }
}
