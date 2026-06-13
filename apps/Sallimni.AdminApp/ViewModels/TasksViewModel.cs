using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.AdminApp.Models;
using Sallimni.AdminApp.Services;

namespace Sallimni.AdminApp.ViewModels;

/// <summary>إنشاء مهام التوصيل (تجميع/توزيع) لكل موجة وإسناد السائقين (قسم 5).</summary>
public partial class TasksViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    public TasksViewModel(ApiClient api) => _api = api;

    public ObservableCollection<WaveSummaryDto> Waves { get; } = new();
    public ObservableCollection<TaskDto> Tasks { get; } = new();
    public ObservableCollection<DriverDto> Drivers { get; } = new();

    [ObservableProperty] private DriverDto? _selectedDriver;

    public bool IsEmpty => Waves.Count == 0 && !IsBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            var drivers = await _api.GetDriversAsync();
            Drivers.Clear();
            foreach (var d in drivers) Drivers.Add(d);
            SelectedDriver ??= Drivers.FirstOrDefault();

            var waves = await _api.GetWavesAsync();
            Waves.Clear();
            foreach (var w in waves) Waves.Add(w);

            var tasks = await _api.GetTasksAsync();
            Tasks.Clear();
            foreach (var t in tasks) Tasks.Add(t);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand]
    private async Task CreateCollectionAsync(WaveSummaryDto wave)
    {
        if (wave is null) return;
        ErrorMessage = null;
        try { await _api.CreateCollectionTaskAsync(wave.WaveId); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateDistributionAsync(WaveSummaryDto wave)
    {
        if (wave is null) return;
        ErrorMessage = null;
        try { await _api.CreateDistributionTaskAsync(wave.WaveId); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task AssignAsync(TaskDto task)
    {
        if (task is null) return;
        var driver = SelectedDriver ?? Drivers.FirstOrDefault();
        if (driver is null) { ErrorMessage = "لا يوجد سائق."; return; }
        ErrorMessage = null;
        try { await _api.AssignDriverAsync(task.TaskId, driver.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
