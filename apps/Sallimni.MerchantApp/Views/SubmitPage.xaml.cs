using Sallimni.MerchantApp.ViewModels;

namespace Sallimni.MerchantApp.Views;

public partial class SubmitPage : ContentPage
{
    private readonly SubmitViewModel _vm;

    public SubmitPage(SubmitViewModel vm)
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
