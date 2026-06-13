using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class CartPage : ContentPage
{
    public CartPage(CartViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
