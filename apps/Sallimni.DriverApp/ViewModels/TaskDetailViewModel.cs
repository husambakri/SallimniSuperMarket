using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.DriverApp.Models;
using Sallimni.DriverApp.Services;

namespace Sallimni.DriverApp.ViewModels;

/// <summary>
/// تفاصيل مهمة: بدء، مسح QR لكل محطة (استلام من متجر / تسليم وتحصيل من زبون)، ثم إنهاء.
/// </summary>
public partial class TaskDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiClient _api;
    private readonly AppState _state;
    private Guid _taskId;

    public TaskDetailViewModel(ApiClient api, AppState state)
    {
        _api = api;
        _state = state;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeKey))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    private DriverTaskDto? _task;

    public ObservableCollection<DriverStopDto> Stops { get; } = new();

    public string TypeKey => Task?.IsCollection == true ? "tasks.collection" : "tasks.distribution";
    public bool CanStart => Task is not null && (Task.Status == 0 || Task.Status == 1);   // Created/Assigned
    public bool CanComplete => Task is not null && Task.Status == 2 && Stops.All(s => s.IsCompleted); // InProgress & all done

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("taskId", out var t) && Guid.TryParse(t?.ToString(), out var g))
            _taskId = g;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy || _state.DriverId is null) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            var tasks = await _api.GetTasksAsync(_state.DriverId.Value);
            Task = tasks.FirstOrDefault(x => x.TaskId == _taskId);
            Stops.Clear();
            if (Task is not null) foreach (var s in Task.Stops) Stops.Add(s);
            RaiseGuards();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (Task is null) return;
        ErrorMessage = null;
        try { await _api.StartTaskAsync(Task.TaskId); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task ScanAsync(DriverStopDto stop)
    {
        if (stop is null || Task is null) return;
        ErrorMessage = null;
        try
        {
            if (Task.IsCollection)
                await _api.PickupAsync(stop.StopId);
            else
                await _api.DeliverAsync(stop.StopId, stop.CodAmount ?? 0m);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (Task is null) return;
        ErrorMessage = null;
        try { await _api.CompleteTaskAsync(Task.TaskId); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void RaiseGuards()
    {
        OnPropertyChanged(nameof(TypeKey));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanComplete));
    }
}
