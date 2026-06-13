using Sallimni.CustomerApp.ViewModels;

namespace Sallimni.CustomerApp.Views;

public partial class ProductDetailPage : ContentPage
{
    public ProductDetailPage(ProductDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    /// <summary>تغذية راجعة للزرّ الكبير: نبضة + نص "✓ تمت الإضافة" مؤقتاً.</summary>
    private async void OnAddTapped(object? sender, EventArgs e)
    {
        if (sender is not Button b) return;
        var original = b.Text;
        b.Text = "✓ تمت الإضافة إلى السلة";
        await b.ScaleToAsync(1.05, 90, Easing.CubicOut);
        await b.ScaleToAsync(1.0, 90, Easing.CubicIn);
        await Task.Delay(900);
        b.Text = original;
    }
}
