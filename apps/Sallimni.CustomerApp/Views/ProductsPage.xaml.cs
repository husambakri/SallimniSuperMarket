using Sallimni.CustomerApp.Services;
using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class ProductsPage : ContentPage
{
    public ProductsPage(ProductsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnAddTapped(object? sender, EventArgs e) => AddFeedback.Bounce(sender);
}
