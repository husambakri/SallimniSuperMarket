using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class ProductsPage : ContentPage
{
    public ProductsPage(ProductsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
