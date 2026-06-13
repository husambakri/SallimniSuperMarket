using Sallimni.AdminApp.ViewModels;

namespace Sallimni.AdminApp.Views;

public partial class ProductEditPage : ContentPage
{
    private readonly ProductEditViewModel _vm;
    public ProductEditPage(ProductEditViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override void OnAppearing() { base.OnAppearing(); _vm.Load(); }
}
