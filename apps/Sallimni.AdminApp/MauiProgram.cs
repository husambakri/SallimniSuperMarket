using Microsoft.Extensions.Logging;
using Sallimni.AdminApp.Services;
using Sallimni.AdminApp.ViewModels;
using Sallimni.AdminApp.Views;

namespace Sallimni.AdminApp;

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

		// الخادم السحابي على Railway (للتطوير المحلّي بدّله بـ http://localhost:5080/ أو 10.0.2.2 للمحاكي).
		var baseUrl = "https://sallimnisupermarket-production.up.railway.app/";
		builder.Services.AddSingleton(new AppConfig { BaseUrl = baseUrl });
		builder.Services.AddSingleton<CatalogState>();
		builder.Services.AddHttpClient<ApiClient>(c =>
		{
			c.BaseAddress = new Uri(baseUrl);
			c.Timeout = TimeSpan.FromSeconds(20);
		});

		builder.Services.AddTransient<CatalogViewModel>();
		builder.Services.AddTransient<ProductEditViewModel>();
		builder.Services.AddTransient<ApprovalsViewModel>();
		builder.Services.AddTransient<TasksViewModel>();
		builder.Services.AddTransient<SettlementsViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();

		builder.Services.AddTransient<CatalogPage>();
		builder.Services.AddTransient<ProductEditPage>();
		builder.Services.AddTransient<ApprovalsPage>();
		builder.Services.AddTransient<TasksPage>();
		builder.Services.AddTransient<SettlementsPage>();
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

	/// <summary>
	/// يحوّل رابط صورة الخادم إلى رابط مطلق: الروابط المطلقة (http/https — صور طلبات)
	/// تُعاد كما هي، والمسارات النسبية (/api/...) يُسبَق إليها عنوان الخادم.
	/// </summary>
	public string? ResolveImageUrl(string? imageUrl)
	{
		if (string.IsNullOrEmpty(imageUrl)) return null;
		if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
		    imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return imageUrl;
		return BaseUrl.TrimEnd('/') + imageUrl;
	}
}
