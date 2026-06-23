using BarcodeScanning;
using Microsoft.Extensions.Logging;
using Sallimni.CompareApp.Services;
using Sallimni.CompareApp.ViewModels;

namespace Sallimni.CompareApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeScanning()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // الخادم السحابي (نقطة مقارنة السعر).
        const string baseUrl = "https://sallimnisupermarket-production.up.railway.app/";
        builder.Services.AddHttpClient<ApiClient>(c =>
        {
            c.BaseAddress = new Uri(baseUrl);
            c.Timeout = TimeSpan.FromSeconds(20);
        });

        builder.Services.AddSingleton<CompareViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
