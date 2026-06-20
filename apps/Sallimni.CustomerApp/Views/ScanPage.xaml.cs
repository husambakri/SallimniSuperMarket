using Sallimni.CustomerApp.ViewModels;
using ZXing.Net.Maui;

namespace Sallimni.CustomerApp.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _vm;
    private bool _handling;                       // يمنع المعالجة المتزامنة
    private DateTime _lastHit = DateTime.MinValue; // Throttle: لا نعالج أكثر من ~4 مرّات/ثانية
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(250);

    public ScanPage(ScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // بدون ضبط الصيغ يكون الكشف معطّلاً (الافتراضي = لا شيء). نُفعّل باركودات
        // البقالة أحادية البُعد (EAN-13/EAN-8/UPC-A/UPC-E/Code128/Code39) مع تدوير تلقائي.
        Reader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.OneDimensional,
            AutoRotate = true,
            Multiple = false
        };
    }

    // يُطلَق على خيط خلفي (المكتبة تفكّ الترميز بعيداً عن خيط الواجهة) عند التقاط باركود.
    // نطبّق Throttle + حارس تزامن، ثم ننقل تحديث الواجهة/الفحص لخيط الواجهة فقط.
    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_handling) return;
        var now = DateTime.UtcNow;
        if (now - _lastHit < ThrottleWindow) return; // تجاهل الإطارات المتلاحقة

        var code = e.Results?.FirstOrDefault()?.Value;
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
