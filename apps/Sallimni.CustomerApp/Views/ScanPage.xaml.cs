using Sallimni.CustomerApp.ViewModels;
using ZXing.Net.Maui;

namespace Sallimni.CustomerApp.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _vm;
    private bool _handling; // يمنع المعالجة المتكرّرة لنفس المسح

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

    // تُطلَق على خيط خلفي عند التقاط الكاميرا لباركود؛ نأخذ أول نتيجة ونفحص السعر.
    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var code = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(code) || _handling) return;
        _handling = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await _vm.OnBarcodeScannedAsync(code); }
            finally { _handling = false; }
        });
    }
}
