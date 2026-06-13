using Sallimni.AdminApp.ViewModels;

namespace Sallimni.AdminApp.Views;

public partial class SettlementsPage : ContentPage
{
    private readonly SettlementsViewModel _vm;
    public SettlementsPage(SettlementsViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadCommand.ExecuteAsync(null); }
}
