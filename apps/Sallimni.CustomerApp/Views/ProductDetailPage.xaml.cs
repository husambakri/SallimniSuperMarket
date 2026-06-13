using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class ProductDetailPage : ContentPage
{
    public ProductDetailPage(ProductDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
