using Microsoft.Extensions.Logging;
using Sallimni.CustomerApp.Services;
using Sallimni.CustomerApp.ViewModels;
using Sallimni.CustomerApp.Views;
using BarcodeScanning;

namespace Sallimni.CustomerApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// تسجيل أي استثناء غير معالَج في مجلّد بيانات التطبيق (تشخيص).
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

		// الخادم السحابي على Railway (للتطوير المحلّي بدّله بـ http://localhost:5080/ أو 10.0.2.2 للمحاكي).
		var baseUrl = "https://sallimnisupermarket-production.up.railway.app/";
		builder.Services.AddSingleton(new AppConfig { BaseUrl = baseUrl });
		builder.Services.AddHttpClient<ApiClient>(c =>
		{
			c.BaseAddress = new Uri(baseUrl);
			c.Timeout = TimeSpan.FromSeconds(20);
		});

		// خدمات مشتركة (مفردة الحالة).
		builder.Services.AddSingleton<CartService>();
		builder.Services.AddSingleton<AppState>();

		// ViewModels.
		builder.Services.AddTransient<CategoriesViewModel>();
		builder.Services.AddTransient<ProductsViewModel>();
		builder.Services.AddTransient<ProductDetailViewModel>();
		builder.Services.AddTransient<ScanViewModel>();
		builder.Services.AddTransient<CartViewModel>();
		builder.Services.AddTransient<OrderViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();

		// Pages.
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<ProductsPage>();
		builder.Services.AddTransient<ProductDetailPage>();
		builder.Services.AddTransient<ScanPage>();
		builder.Services.AddTransient<CartPage>();
		builder.Services.AddTransient<OrderPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

/// <summary>إعداد بسيط لعنوان الخادم (قابل للعرض/التعديل في الإعدادات).</summary>
public class AppConfig
{
	public string BaseUrl { get; set; } = "http://localhost:5080/";

	/// <summary>
	/// يحوّل رابط صورة الخادم إلى رابط مطلق قابل للعرض:
	/// الروابط المطلقة (http/https — مثل صور طلبات) تُعاد كما هي،
	/// والمسارات النسبية (مثل /api/catalog/...) يُسبَق إليها عنوان الخادم.
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
