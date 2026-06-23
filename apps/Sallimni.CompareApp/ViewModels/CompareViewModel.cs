using System.Collections.ObjectModel;
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
    [ObservableProperty] private int _resultCount;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    // ملخّص المنتج (من أرخص نتيجة).
    [ObservableProperty] private bool _hasProduct;
    [ObservableProperty] private string? _productName;
    [ObservableProperty] private string? _productImageUrl;
    [ObservableProperty] private bool _productHasImage;
    [ObservableProperty] private string? _cheapestPriceText;

    public ObservableCollection<LiveScanDto> Results { get; } = new();

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
        Results.Clear();

        try
        {
            var resp = await _api.ScanCompareAsync(code);
            var list = resp?.Results ?? new List<LiveScanDto>();

            // الأرخص = أقلّ سعر فعلي بين المتوفّر (وإلا أوّل نتيجة) — يُميَّز ويُحسب توفيره.
            var cheapest = list.Where(r => r.InStock).OrderBy(r => r.EffectivePrice).FirstOrDefault()
                           ?? list.FirstOrDefault();
            if (cheapest is not null)
            {
                cheapest.IsCheapest = true;
                if (list.Count > 1)
                {
                    var save = list.Max(r => r.EffectivePrice) - cheapest.EffectivePrice;
                    if (save > 0) cheapest.Note = $"وفّر {save:0.00} د.أ";
                }

                ProductName = cheapest.Name;
                ProductImageUrl = cheapest.HasImage ? cheapest.ImageUrl : null;
                ProductHasImage = cheapest.HasImage;
                CheapestPriceText = cheapest.PriceText;
                HasProduct = true;
            }

            foreach (var r in list) Results.Add(r);
            ResultCount = list.Count;

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
}
