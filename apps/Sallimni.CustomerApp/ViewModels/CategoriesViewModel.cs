using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CustomerApp.Models;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.ViewModels;

/// <summary>الصفحة الرئيسية: ترويسة + بحث + شريط عروض (أعلى توفير) + شبكة الأصناف.</summary>
public partial class CategoriesViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppState _state;
    private readonly CartService _cart;

    public CategoriesViewModel(ApiClient api, AppState state, CartService cart)
    {
        _api = api;
        _state = state;
        _cart = cart;
    }

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<ProductDto> Offers { get; } = new();
    public CartService Cart => _cart;

    [ObservableProperty] private string _searchText = "";
    public bool HasOffers => Offers.Count > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            await EnsureCustomerAsync();
            var cats = await _api.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            var offers = await _api.GetOffersAsync(10);
            Offers.Clear();
            foreach (var o in offers) Offers.Add(o);
            OnPropertyChanged(nameof(HasOffers));
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task EnsureCustomerAsync()
    {
        if (_state.CurrentCustomer is not null) return;
        var customers = await _api.GetCustomersAsync();
        var cust = customers.FirstOrDefault();
        if (cust is not null)
        {
            _state.CurrentCustomer = cust;
            _state.SelectedAddress = cust.Addresses.FirstOrDefault(a => a.IsDefault) ?? cust.Addresses.FirstOrDefault();
        }
    }

    [RelayCommand]
    private async Task OpenCategoryAsync(CategoryDto category)
    {
        if (category is null) return;
        await Shell.Current.GoToAsync($"products?categoryId={category.Id}&name={Uri.EscapeDataString(category.NameAr)}");
    }

    [RelayCommand]
    private async Task OpenProductAsync(ProductDto product)
    {
        if (product is null) return;
        await Shell.Current.GoToAsync($"productdetail?id={product.Id}");
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var q = (SearchText ?? "").Trim();
        if (q.Length == 0) return;
        await Shell.Current.GoToAsync($"products?q={Uri.EscapeDataString(q)}&name={Uri.EscapeDataString("نتائج: " + q)}");
    }
}
