using Sallimni.CustomerApp.Services;
using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _vm;

    public CategoriesPage(CategoriesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Categories.Count == 0)
            await _vm.LoadCommand.ExecuteAsync(null);
    }

    private void OnAddTapped(object? sender, EventArgs e) => AddFeedback.Bounce(sender);
}
