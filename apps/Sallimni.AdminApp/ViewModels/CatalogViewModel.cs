using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.AdminApp.Models;
using Sallimni.AdminApp.Services;

namespace Sallimni.AdminApp.ViewModels;

/// <summary>خيار شريحة ضريبة للعرض في القائمة.</summary>
public record TaxOption(int Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// تأسيس الأصناف (قسم 2/3): الإدارة تنشئ التصنيفات وبطاقات الأصناف الرئيسية.
/// </summary>
public partial class CatalogViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppConfig _config;
    private readonly CatalogState _state;

    public CatalogViewModel(ApiClient api, AppConfig config, CatalogState state)
    {
        _api = api;
        _config = config;
        _state = state;
    }

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<AdminProductDto> Products { get; } = new();

    public List<TaxOption> TaxOptions { get; } = new()
    {
        new(-1, "معفى"), new(0, "0%"), new(2, "2%"), new(4, "4%"),
        new(5, "5%"), new(10, "10%"), new(16, "16%")
    };

    // نموذج إضافة صنف
    [ObservableProperty] private string _nameAr = "";
    [ObservableProperty] private string _nameEn = "";
    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _unitSize = "";
    [ObservableProperty] private string _emoji = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private CategoryDto? _selectedCategory;
    [ObservableProperty] private TaxOption? _selectedTax;

    // نموذج إضافة تصنيف
    [ObservableProperty] private string _catNameAr = "";
    [ObservableProperty] private string _catNameEn = "";
    [ObservableProperty] private string _catIcon = "";

    // صور مختارة بانتظار الإنشاء (تُرفع بعد الحصول على المعرّف)
    private (byte[] Bytes, string Name, string Type)? _pendingProductImage;
    private (byte[] Bytes, string Name, string Type)? _pendingCategoryImage;
    [ObservableProperty] private string? _productImageName;
    [ObservableProperty] private string? _categoryImageName;

    public bool HasProducts => Products.Count > 0;

    private static async Task<(byte[] Bytes, string Name, string Type)?> PickImageAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "اختر صورة",
            FileTypes = FilePickerFileType.Images
        });
        if (file is null) return null;
        await using var s = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        var type = string.IsNullOrEmpty(file.ContentType) ? "image/jpeg" : file.ContentType;
        return (ms.ToArray(), file.FileName, type);
    }

    [RelayCommand]
    private async Task PickProductImageAsync()
    {
        try
        {
            var img = await PickImageAsync();
            if (img is null) return;
            _pendingProductImage = img;
            ProductImageName = img.Value.Name;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task PickCategoryImageAsync()
    {
        try
        {
            var img = await PickImageAsync();
            if (img is null) return;
            _pendingCategoryImage = img;
            CategoryImageName = img.Value.Name;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            var cats = await _api.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);
            _state.Categories = cats;
            SelectedCategory ??= Categories.FirstOrDefault();
            SelectedTax ??= TaxOptions.Last(); // 16% افتراضي

            var prods = await _api.GetProductsAsync();
            Products.Clear();
            foreach (var p in prods)
            {
                p.FullImageUrl = _config.ResolveImageUrl(p.ImageUrl);
                Products.Add(p);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; OnPropertyChanged(nameof(HasProducts)); }
    }

    [RelayCommand]
    private async Task AddProductAsync()
    {
        StatusMessage = null; ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(NameAr)) { ErrorMessage = "الاسم بالعربية مطلوب."; return; }
        if (SelectedCategory is null) { ErrorMessage = "اختر تصنيفاً."; return; }
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var newId = await _api.CreateProductAsync(new CreateProductRequest
            {
                NameAr = NameAr.Trim(),
                NameEn = NameEn.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                UnitSize = string.IsNullOrWhiteSpace(UnitSize) ? null : UnitSize.Trim(),
                Emoji = string.IsNullOrWhiteSpace(Emoji) ? null : Emoji.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                CategoryId = SelectedCategory.Id,
                TaxClass = SelectedTax?.Value ?? 16
            });
            // رفع الصورة المختارة (إن وُجدت) بعد إنشاء الصنف.
            if (_pendingProductImage is { } img && newId != Guid.Empty)
            {
                using var ms = new MemoryStream(img.Bytes);
                await _api.UploadProductImageAsync(newId, ms, img.Name, img.Type);
            }
            StatusMessage = "تمت إضافة الصنف ✔";
            NameAr = NameEn = Barcode = UnitSize = Emoji = Description = "";
            _pendingProductImage = null; ProductImageName = null;
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        StatusMessage = null; ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(CatNameAr)) { ErrorMessage = "اسم التصنيف بالعربية مطلوب."; return; }
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var newCatId = await _api.CreateCategoryAsync(new CreateCategoryRequest
            {
                NameAr = CatNameAr.Trim(),
                NameEn = CatNameEn.Trim(),
                Icon = string.IsNullOrWhiteSpace(CatIcon) ? null : CatIcon.Trim()
            });
            if (_pendingCategoryImage is { } img && newCatId != Guid.Empty)
            {
                using var ms = new MemoryStream(img.Bytes);
                await _api.UploadCategoryImageAsync(newCatId, ms, img.Name, img.Type);
            }
            StatusMessage = "تمت إضافة التصنيف ✔";
            CatNameAr = CatNameEn = CatIcon = "";
            _pendingCategoryImage = null; CategoryImageName = null;
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    /// <summary>فتح صفحة تعديل صنف.</summary>
    [RelayCommand]
    private async Task OpenProductAsync(AdminProductDto product)
    {
        if (product is null) return;
        _state.SelectedProduct = product;
        await Shell.Current.GoToAsync("productedit");
    }

    /// <summary>حذف تصنيف (بتأكيد).</summary>
    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryDto category)
    {
        if (category is null) return;
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;
        var ok = await page.DisplayAlertAsync("حذف التصنيف", $"حذف \"{category.NameAr}\"؟", "نعم", "لا");
        if (!ok) return;
        try { await _api.DeleteCategoryAsync(category.Id); await LoadAsync(); }
        catch (Exception ex) { await page.DisplayAlertAsync("خطأ", ex.Message, "حسناً"); }
    }
}
