using CommunityToolkit.Mvvm.ComponentModel;

namespace Sallimni.CustomerApp.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private string? _errorMessage;

    public bool IsNotBusy => !IsBusy;
}
