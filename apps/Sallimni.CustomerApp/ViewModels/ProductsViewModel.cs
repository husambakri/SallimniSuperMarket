using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CustomerApp.Models;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.ViewModels;

/// <summary>منتجات فئة مختارة، مع بطاقات بشارة توفير وزرّ إضافة، وتصفّح لصفحة التفاصيل.</summary>
public partial class ProductsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiClient _api;
    private readonly CartService _cart;
    private readonly AppConfig _config;

    public ProductsViewModel(ApiClient api, CartService cart, AppConfig config)
    {
        _api = api;
        _cart = cart;
        _config = config;
    }

    public ObservableCollection<ProductDto> Products { get; } = new();
    public CartService Cart => _cart;

    [ObservableProperty] private string _title = "";
    private Guid? _categoryId;
    private string? _query;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _categoryId = null;
        _query = null;
        if (query.TryGetValue("categoryId", out var cid) && Guid.TryParse(cid?.ToString(), out var g))
            _categoryId = g;
        if (query.TryGetValue("q", out var qq))
            _query = Uri.UnescapeDataString(qq?.ToString() ?? "");
        if (query.TryGetValue("name", out var n))
            Title = Uri.UnescapeDataString(n?.ToString() ?? "");
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            var products = await _api.GetProductsAsync(_categoryId, _query);
            Products.Clear();
            foreach (var p in products)
            {
                p.FullImageUrl = _config.ResolveImageUrl(p.ImageUrl);
                Products.Add(p);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddToCart(ProductDto product)
    {
        if (product?.CheapestPriceInclTax is null) return;
        _cart.Add(product);
    }

    [RelayCommand]
    private async Task OpenProductAsync(ProductDto product)
    {
        if (product is null) return;
        await Shell.Current.GoToAsync($"productdetail?id={product.Id}");
    }
}
