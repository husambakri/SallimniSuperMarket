using CommunityToolkit.Mvvm.ComponentModel;

namespace Sallimni.DriverApp.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private string? _errorMessage;
    public bool IsNotBusy => !IsBusy;
}
