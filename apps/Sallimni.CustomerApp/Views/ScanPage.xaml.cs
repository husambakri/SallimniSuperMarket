using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class ScanPage : ContentPage
{
    public ScanPage(ScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
