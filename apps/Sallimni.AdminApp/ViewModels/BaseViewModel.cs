using CommunityToolkit.Mvvm.ComponentModel;

namespace Sallimni.AdminApp.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;

    public bool IsNotBusy => !IsBusy;
}
