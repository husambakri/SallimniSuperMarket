using Sallimni.CustomerApp.ViewModels;
using BarcodeScanning;

namespace Sallimni.CustomerApp.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _vm;
    private bool _handling;                        // يمنع المعالجة المتزامنة
    private DateTime _lastHit = DateTime.MinValue;  // Throttle: لا نعالج أكثر من ~4 مرّات/ثانية
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(250);

    public ScanPage(ScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // يُطلَق على خيط خلفي (المحرّك الأصلي ML Kit/Vision يحلّل الإطارات بعيداً عن خيط الواجهة).
    // نطبّق Throttle + حارس تزامن، ثم ننقل الفحص/تحديث الواجهة لخيط الواجهة فقط.
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
