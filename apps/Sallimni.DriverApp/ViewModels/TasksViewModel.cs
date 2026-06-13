using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.DriverApp.Models;
using Sallimni.DriverApp.Services;

namespace Sallimni.DriverApp.ViewModels;

/// <summary>قائمة مهام السائق (تجميع/توزيع) مع التنقّل لصفحة التفاصيل.</summary>
public partial class TasksViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppState _state;

    public TasksViewModel(ApiClient api, AppState state)
    {
        _api = api;
        _state = state;
    }

    public ObservableCollection<DriverTaskDto> Tasks { get; } = new();
    public bool IsEmpty => Tasks.Count == 0 && !IsBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            await _state.EnsureDriverAsync(_api);
            if (_state.DriverId is null) { ErrorMessage = "لا يوجد سائق."; return; }
            var tasks = await _api.GetTasksAsync(_state.DriverId.Value);
            Tasks.Clear();
            foreach (var t in tasks) Tasks.Add(t);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand]
    private async Task OpenTaskAsync(DriverTaskDto task)
    {
        if (task is null) return;
        await Shell.Current.GoToAsync($"taskdetail?taskId={task.TaskId}");
    }
}
