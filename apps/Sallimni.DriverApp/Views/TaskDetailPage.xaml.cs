using Sallimni.DriverApp.ViewModels;

namespace Sallimni.DriverApp.Views;

public partial class TaskDetailPage : ContentPage
{
    public TaskDetailPage(TaskDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
