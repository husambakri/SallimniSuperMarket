using Sallimni.AdminApp.ViewModels;

namespace Sallimni.AdminApp.Views;

public partial class CatalogPage : ContentPage
{
    private readonly CatalogViewModel _vm;
    public CatalogPage(CatalogViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadCommand.ExecuteAsync(null); }
}
