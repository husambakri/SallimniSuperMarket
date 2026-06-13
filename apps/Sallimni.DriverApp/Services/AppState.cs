using CommunityToolkit.Mvvm.ComponentModel;
using Sallimni.DriverApp.Models;

namespace Sallimni.DriverApp.Services;

/// <summary>حالة مشتركة: السائق الحالي (يُختار من قائمة السائقين في الإصدار الأول).</summary>
public partial class AppState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DriverId))]
    private DriverInfoDto? _currentDriver;

    public List<DriverInfoDto> Drivers { get; private set; } = new();
    public Guid? DriverId => CurrentDriver?.Id;

    public async Task EnsureDriverAsync(ApiClient api, CancellationToken ct = default)
    {
        if (CurrentDriver is not null) return;
        Drivers = await api.GetDriversAsync(ct);
        CurrentDriver = Drivers.FirstOrDefault();
    }
}
