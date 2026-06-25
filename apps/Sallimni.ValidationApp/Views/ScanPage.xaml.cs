using BarcodeScanning;
using Sallimni.ValidationApp.ViewModels;

namespace Sallimni.ValidationApp.Views;

public partial class ScanPage : ContentPage
{
    private readonly ValidationViewModel _vm;
    private bool _handling;                            // يمنع المعالجة المتزامنة
    private DateTime _lastHit = DateTime.MinValue;     // throttle ~4 مرّات/ثانية
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(250);

    public ScanPage(ValidationViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // يُطلَق على خيط خلفي — نطبّق throttle + حارس تزامن.
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
