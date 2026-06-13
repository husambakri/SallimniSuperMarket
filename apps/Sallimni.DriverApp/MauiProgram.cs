using Microsoft.Extensions.Logging;
using Sallimni.DriverApp.Services;
using Sallimni.DriverApp.ViewModels;
using Sallimni.DriverApp.Views;

namespace Sallimni.DriverApp;

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

		builder.Services.AddTransient<TasksViewModel>();
		builder.Services.AddTransient<TaskDetailViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();

		builder.Services.AddTransient<TasksPage>();
		builder.Services.AddTransient<TaskDetailPage>();
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
