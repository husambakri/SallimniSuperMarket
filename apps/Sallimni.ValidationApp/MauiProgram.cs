using BarcodeScanning;
using Microsoft.Extensions.Logging;
using Sallimni.ValidationApp.Services;
using Sallimni.ValidationApp.ViewModels;
using Sallimni.ValidationApp.Views;

namespace Sallimni.ValidationApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // تسجيل أي استثناء غير معالَج في crash.log داخل بيانات التطبيق (تشخيص الكراش).
        var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
        void Log(object? ex) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {ex}\n\n");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => Log(e.Exception);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeScanning()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // الخادم السحابي (نقطة تحقّق الأسعار).
        const string baseUrl = "https://sallimnisupermarket-production.up.railway.app/";
        builder.Services.AddHttpClient<ApiClient>(c =>
        {
            c.BaseAddress = new Uri(baseUrl);
            c.Timeout = TimeSpan.FromSeconds(20);
        });

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<ValidationViewModel>();
        builder.Services.AddSingleton<ScanPage>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<InfoViewModel>();
        builder.Services.AddTransient<InfoPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
