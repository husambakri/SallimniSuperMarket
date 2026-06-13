using Microsoft.Extensions.Logging;
using Sallimni.MerchantApp.Services;
using Sallimni.MerchantApp.ViewModels;
using Sallimni.MerchantApp.Views;

namespace Sallimni.MerchantApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
		void Log(object? ex) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {ex}\n\n");
		AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject);
		TaskScheduler.UnobservedTaskException += (_, e) => Log(e.Exception);

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// عنوان الخادم: محاكي Android عبر 10.0.2.2، وويندوز عبر localhost.
#if ANDROID
		var baseUrl = "http://10.0.2.2:5080/";
#else
		var baseUrl = "http://localhost:5080/";
#endif
		builder.Services.AddSingleton(new AppConfig { BaseUrl = baseUrl });
		builder.Services.AddHttpClient<ApiClient>(c =>
		{
			c.BaseAddress = new Uri(baseUrl);
			c.Timeout = TimeSpan.FromSeconds(20);
		});

		builder.Services.AddSingleton<AppState>();

		builder.Services.AddTransient<InventoryViewModel>();
		builder.Services.AddTransient<OrdersViewModel>();
		builder.Services.AddTransient<SubmitViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();

		builder.Services.AddTransient<InventoryPage>();
		builder.Services.AddTransient<OrdersPage>();
		builder.Services.AddTransient<SubmitPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

public class AppConfig
{
	public string BaseUrl { get; set; } = "http://localhost:5080/";
}
