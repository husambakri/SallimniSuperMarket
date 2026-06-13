using Sallimni.MerchantApp.ViewModels;

namespace Sallimni.MerchantApp.Views;

public partial class InventoryPage : ContentPage
{
    private readonly InventoryViewModel _vm;

    public InventoryPage(InventoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Rows.Count == 0)
            await _vm.LoadCommand.ExecuteAsync(null);
    }
}
