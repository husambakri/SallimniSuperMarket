namespace Sallimni.CustomerApp.Services;

/// <summary>تغذية راجعة بصرية موحّدة عند الإضافة للسلة (نبضة + علامة ✓ مؤقتة).</summary>
public static class AddFeedback
{
    public static async void Bounce(object? sender)
    {
        if (sender is not Button b) return;
        var original = b.Text;
        b.Text = "✓";
        await b.ScaleToAsync(1.35, 90, Easing.CubicOut);
        await b.ScaleToAsync(1.0, 90, Easing.CubicIn);
        await Task.Delay(550);
        b.Text = original;
    }
}
