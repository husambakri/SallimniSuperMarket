using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CustomerApp.Models;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.ViewModels;

/// <summary>صفحة تفاصيل المنتج: صورة كبيرة، السعر والتوفير، الوصف، وإضافة للسلة.</summary>
public partial class ProductDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiClient _api;
    private readonly CartService _cart;

    public ProductDetailViewModel(ApiClient api, CartService cart)
    {
        _api = api;
        _cart = cart;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    [NotifyPropertyChangedFor(nameof(HasSavings))]
    [NotifyPropertyChangedFor(nameof(SavingsText))]
    private ProductDetailDto? _product;

    public string Glyph => string.IsNullOrEmpty(Product?.Emoji) ? "🛒" : Product!.Emoji!;
    public bool HasSavings => (Product?.SavingsPercent ?? 0) > 0;
    public string SavingsText => $"وفّر {Product?.SavingsPercent ?? 0}%";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && Guid.TryParse(id?.ToString(), out var g))
            _ = LoadAsync(g);
    }

    private async Task LoadAsync(Guid id)
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try { Product = await _api.GetProductAsync(id); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddToCartAsync()
    {
        if (Product?.CheapestPriceInclTax is null) return;
        _cart.Add(new ProductDto
        {
            Id = Product.Id,
            NameAr = Product.NameAr,
            NameEn = Product.NameEn,
            Emoji = Product.Emoji,
            UnitSize = Product.UnitSize,
            CategoryId = Product.CategoryId,
            CheapestPriceInclTax = Product.CheapestPriceInclTax,
            RegularPriceInclTax = Product.RegularPriceInclTax,
            SavingsPercent = Product.SavingsPercent
        });
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
            await page.DisplayAlertAsync(LocalizationManager.Instance["app.title"], "تمت الإضافة للسلة", LocalizationManager.Instance["common.ok"]);
    }
}
