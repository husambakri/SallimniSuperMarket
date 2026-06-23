using BarcodeScanning;
using Sallimni.CompareApp.ViewModels;

namespace Sallimni.CompareApp;

public partial class MainPage : ContentPage
{
    private readonly CompareViewModel _vm;
    private bool _handling;                          // يمنع المعالجة المتزامنة
    private DateTime _lastHit = DateTime.MinValue;    // throttle ~4 مرّات/ثانية
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(250);

    public MainPage(CompareViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // يُطلَق على خيط خلفي (المحرّك الأصلي يحلّل الإطارات) — نطبّق throttle + حارس تزامن.
    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        if (_handling) return;
        var now = DateTime.UtcNow;
        if (now - _lastHit < ThrottleWindow) return;

        var code = e.BarcodeResults?.FirstOrDefault()?.DisplayValue;
        if (string.IsNullOrWhiteSpace(code)) return;

        _lastHit = now;
        _handling = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await _vm.OnBarcodeScannedAsync(code); }
            finally { _handling = false; }
        });
    }
}
