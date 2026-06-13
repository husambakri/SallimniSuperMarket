using Sallimni.DriverApp.ViewModels;

namespace Sallimni.DriverApp.Views;

public partial class TasksPage : ContentPage
{
    private readonly TasksViewModel _vm;
    public TasksPage(TasksViewModel vm) { InitializeComponent(); BindingContext = _vm = vm; }
    protected override async void OnAppearing() { base.OnAppearing(); await _vm.LoadCommand.ExecuteAsync(null); }
}
