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
    private readonly AppConfig _config;

    public ProductDetailViewModel(ApiClient api, CartService cart, AppConfig config)
    {
        _api = api;
        _cart = cart;
        _config = config;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    [NotifyPropertyChangedFor(nameof(HasSavings))]
    [NotifyPropertyChangedFor(nameof(SavingsText))]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(ImageUrl))]
    private ProductDetailDto? _product;

    public string Glyph => string.IsNullOrEmpty(Product?.Emoji) ? "🛒" : Product!.Emoji!;
    public bool HasSavings => (Product?.SavingsPercent ?? 0) > 0;
    public string SavingsText => $"وفّر {Product?.SavingsPercent ?? 0}%";
    public bool HasImage => Product is not null && !string.IsNullOrEmpty(Product.FullImageUrl);
    public string? ImageUrl => Product?.FullImageUrl;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && Guid.TryParse(id?.ToString(), out var g))
            _ = LoadAsync(g);
    }

    private async Task LoadAsync(Guid id)
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            var p = await _api.GetProductAsync(id);
            if (p is not null && !string.IsNullOrEmpty(p.ImageUrl))
                p.FullImageUrl = _config.BaseUrl.TrimEnd('/') + p.ImageUrl;
            Product = p;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddToCart()
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
        // التغذية الراجعة البصرية تتمّ في الواجهة (نبضة الزرّ) بدل نافذة منبثقة.
    }
}
