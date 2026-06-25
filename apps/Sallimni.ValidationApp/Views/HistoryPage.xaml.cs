using Sallimni.ValidationApp.ViewModels;

namespace Sallimni.ValidationApp.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // نحدّث الفروع كلّما ظهرت الصفحة (يلتقط تسجيلات جديدة من تبويب المسح).
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.LoadBranchesCommand.CanExecute(null))
            _vm.LoadBranchesCommand.Execute(null);
    }
}
