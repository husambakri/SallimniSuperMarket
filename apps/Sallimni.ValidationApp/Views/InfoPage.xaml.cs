using Sallimni.ValidationApp.ViewModels;

namespace Sallimni.ValidationApp.Views;

public partial class InfoPage : ContentPage
{
    private readonly InfoViewModel _vm;

    public InfoPage(InfoViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // نحدّث اللقطة كلّما ظهرت الصفحة.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.RefreshCommand.CanExecute(null))
            _vm.RefreshCommand.Execute(null);
    }
}
