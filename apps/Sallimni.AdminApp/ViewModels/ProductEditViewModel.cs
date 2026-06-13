using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.AdminApp.Models;
using Sallimni.AdminApp.Services;

namespace Sallimni.AdminApp.ViewModels;

/// <summary>تعديل بطاقة صنف: كل الحقول + رفع صورة حقيقية + حذف.</summary>
public partial class ProductEditViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppConfig _config;
    private readonly CatalogState _state;
    private Guid _productId;

    public ProductEditViewModel(ApiClient api, AppConfig config, CatalogState state)
    {
        _api = api;
        _config = config;
        _state = state;
    }

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public List<TaxOption> TaxOptions { get; } = new()
    {
        new(-1, "معفى"), new(0, "0%"), new(2, "2%"), new(4, "4%"),
        new(5, "5%"), new(10, "10%"), new(16, "16%")
    };

    [ObservableProperty] private string _nameAr = "";
    [ObservableProperty] private string _nameEn = "";
    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _unitSize = "";
    [ObservableProperty] private string _emoji = "";
    [ObservableProperty] private CategoryDto? _selectedCategory;
    [ObservableProperty] private TaxOption? _selectedTax;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _previewImageUrl;
    public bool HasImage => !string.IsNullOrEmpty(PreviewImageUrl);

    public void Load()
    {
        Categories.Clear();
        foreach (var c in _state.Categories) Categories.Add(c);

        var p = _state.SelectedProduct;
        if (p is null) return;
        _productId = p.Id;
        NameAr = p.NameAr; NameEn = p.NameEn; Barcode = p.Barcode ?? "";
        UnitSize = p.UnitSize ?? ""; Emoji = p.Emoji ?? "";
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == p.CategoryId);
        SelectedTax = TaxOptions.FirstOrDefault(t => t.Value == p.TaxClass) ?? TaxOptions.Last();
        PreviewImageUrl = p.FullImageUrl;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = null; ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(NameAr)) { ErrorMessage = "الاسم بالعربية مطلوب."; return; }
        if (SelectedCategory is null) { ErrorMessage = "اختر تصنيفاً."; return; }
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.UpdateProductAsync(_productId, new CreateProductRequest
            {
                NameAr = NameAr.Trim(),
                NameEn = NameEn.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                UnitSize = string.IsNullOrWhiteSpace(UnitSize) ? null : UnitSize.Trim(),
                Emoji = string.IsNullOrWhiteSpace(Emoji) ? null : Emoji.Trim(),
                CategoryId = SelectedCategory.Id,
                TaxClass = SelectedTax?.Value ?? 16
            });
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        ErrorMessage = null;
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "اختر صورة الصنف",
                FileTypes = FilePickerFileType.Images
            });
            if (file is null) return;

            IsBusy = true;
            await using var stream = await file.OpenReadAsync();
            var contentType = string.IsNullOrEmpty(file.ContentType) ? "image/jpeg" : file.ContentType;
            await _api.UploadProductImageAsync(_productId, stream, file.FileName, contentType);

            // إعادة تحميل الصورة مع كاسر تخزين مؤقت.
            var baseUrl = _config.BaseUrl.TrimEnd('/');
            PreviewImageUrl = $"{baseUrl}/api/catalog/products/{_productId}/image?t={DateTime.UtcNow.Ticks}";
            StatusMessage = "تم رفع الصورة ✔";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;
        var ok = await page.DisplayAlertAsync("حذف الصنف", $"حذف \"{NameAr}\"؟ سيُخفى من المتجر.", "نعم", "لا");
        if (!ok) return;
        try
        {
            await _api.DeleteProductAsync(_productId);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { await page.DisplayAlertAsync("خطأ", ex.Message, "حسناً"); }
    }
}
