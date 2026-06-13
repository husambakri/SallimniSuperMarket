using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CustomerApp.Models;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.ViewModels;

public partial class CartViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly CartService _cart;
    private readonly AppState _state;

    public CartViewModel(ApiClient api, CartService cart, AppState state)
    {
        _api = api;
        _cart = cart;
        _state = state;
    }

    public CartService Cart => _cart;
    public AppState State => _state;

    [RelayCommand]
    private void Increment(CartItem item) => _cart.Increment(item);

    [RelayCommand]
    private void Decrement(CartItem item) => _cart.Decrement(item);

    [RelayCommand]
    private void Remove(CartItem item) => _cart.Remove(item);

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (IsBusy || _cart.IsEmpty) return;
        if (_state.CurrentCustomer is null || _state.SelectedAddress is null)
        {
            ErrorMessage = "لا يوجد زبون/عنوان محدّد.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var req = new PlaceOrderRequest
            {
                CustomerId = _state.CurrentCustomer.Id,
                AddressId = _state.SelectedAddress.Id,
                PaymentMethod = _state.PaymentMethod,
                Items = _cart.Items.Select(i => new CartLineDto
                {
                    ProductId = i.Product.Id,
                    Quantity = i.Quantity
                }).ToList()
            };

            var order = await _api.PlaceOrderAsync(req);
            _state.LastOrder = order;
            _cart.Clear();
            await Shell.Current.GoToAsync("order");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }
}
