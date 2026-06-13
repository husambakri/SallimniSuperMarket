using Sallimni.MerchantApp.ViewModels;

namespace Sallimni.MerchantApp.Views;

public partial class OrdersPage : ContentPage
{
    private readonly OrdersViewModel _vm;

    public OrdersPage(OrdersViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
