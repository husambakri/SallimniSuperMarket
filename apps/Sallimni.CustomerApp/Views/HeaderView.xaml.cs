using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.Views;

/// <summary>ترويسة موحّدة (بحث + سلة بعدّاد) تُوضع أعلى صفحات التصفّح/الإضافة للسلة.</summary>
public partial class HeaderView : ContentView
{
    private CartService? _cart;

    public HeaderView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _cart ??= Application.Current?.Handler?.MauiContext?.Services?.GetService<CartService>();
        if (_cart is not null)
        {
            _cart.PropertyChanged += OnCartChanged;
            UpdateBadge();
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_cart is not null) _cart.PropertyChanged -= OnCartChanged;
    }

    private void OnCartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartService.Count) or null)
            MainThread.BeginInvokeOnMainThread(UpdateBadge);
    }

    private void UpdateBadge()
    {
        var n = _cart?.Count ?? 0;
        CountLabel.Text = n.ToString();
        Badge.IsVisible = n > 0;
    }

    private async void OnOpenCart(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//cart");

    private async void OnSearch(object? sender, EventArgs e)
    {
        var q = SearchEntry.Text?.Trim();
        if (string.IsNullOrEmpty(q)) return;
        await Shell.Current.GoToAsync($"products?q={Uri.EscapeDataString(q)}&name={Uri.EscapeDataString("نتائج: " + q)}");
    }
}
