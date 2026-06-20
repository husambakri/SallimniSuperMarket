using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CustomerApp.Models;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.ViewModels;

/// <summary>
/// فحص السعر بالباركود (Scan-to-Compare, قسم 4.1).
/// يُرسل رقم الباركود فقط للخادم، ويعرض سعرنا (أرخص تاجر شامل الضريبة) أو "غير متوفر".
/// مسح الكاميرا الحيّ يُضاف على الأجهزة عبر مكتبة مسح (ZXing/ML Kit) — هنا إدخال يدوي + نتيجة.
/// </summary>
public partial class ScanViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly CartService _cart;
    private readonly AppState _state;
    private readonly AppConfig _config;

    public ScanViewModel(ApiClient api, CartService cart, AppState state, AppConfig config)
    {
        _api = api;
        _cart = cart;
        _state = state;
        _config = config;
    }

    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private BarcodeLookupDto? _result;

    /// <summary>هل الكاميرا قيد المسح الآن (يُظهر معاينة الكاميرا الحيّة).</summary>
    [ObservableProperty] private bool _isScanning;

    /// <summary>تبديل تشغيل/إيقاف مسح الكاميرا (يطلب إذن الكاميرا عند التشغيل).</summary>
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
        }
        IsScanning = !IsScanning;
    }

    /// <summary>يُستدعى من الصفحة عند التقاط الكاميرا لباركود: يوقف المسح ويفحص السعر.</summary>
    public async Task OnBarcodeScannedAsync(string code)
    {
        code = (code ?? "").Trim();
        if (code.Length == 0) return;
        IsScanning = false;
        Barcode = code;
        await LookupAsync();
    }

    [RelayCommand]
    private async Task LookupAsync()
    {
        var code = (Barcode ?? "").Trim();
        if (code.Length == 0 || IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        HasResult = false;
        try
        {
            var res = await _api.LookupBarcodeAsync(code, _state.CurrentCustomer?.Id);
            if (res is not null) res.FullImageUrl = _config.ResolveImageUrl(res.ImageUrl);
            Result = res;
            HasResult = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    public bool CanAddResult => Result is { Found: true, ProductId: not null, OurPriceInclTax: not null };

    [RelayCommand]
    private void AddResultToCart()
    {
        if (!CanAddResult) return;
        _cart.Add(new ProductDto
        {
            Id = Result!.ProductId!.Value,
            NameAr = Result.NameAr ?? "",
            NameEn = Result.NameEn ?? "",
            ImageUrl = Result.ImageUrl,
            FullImageUrl = Result.FullImageUrl,
            Emoji = Result.Emoji,
            CheapestPriceInclTax = Result.OurPriceInclTax
        });
    }

    partial void OnResultChanged(BarcodeLookupDto? value) => OnPropertyChanged(nameof(CanAddResult));
}
