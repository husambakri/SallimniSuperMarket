using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class OrderPage : ContentPage
{
    private readonly OrderViewModel _vm;

    public OrderPage(OrderViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }
}
