using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sallimni.Application.Abstractions;
using Sallimni.Application.Services;
using Sallimni.Infrastructure.Services;
using StackExchange.Redis;

namespace Sallimni.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSallimniInfrastructure(
        this IServiceCollection services, string connectionString, string? redisConnectionString = null)
    {
        services.AddDbContext<SallimniDbContext>(opt => opt.UseNpgsql(connectionString));

        // المحرّك النقي + خدمات التنسيق.
        services.AddScoped<OrderSplitEngine>();
        services.AddScoped<CommissionService>();
        services.AddScoped<WaveService>();
        services.AddScoped<OrderService>();
        services.AddScoped<BarcodeService>();
        services.AddScoped<MerchantService>();
        services.AddScoped<AdminService>();
        services.AddScoped<DriverService>();
        services.AddScoped<AccountService>();

        AddLowestPriceCache(services, redisConnectionString);

        return services;
    }

    /// <summary>
    /// كاش أقل سعر: Redis إن تَوفّر اتصاله، وإلّا بديل صامت يعمل من القاعدة. لا يُسقِط
    /// الإقلاع إن تعذّر الاتصال بـ Redis (abortConnect=false + إعادة محاولة لاحقاً).
    /// </summary>
    private static void AddLowestPriceCache(IServiceCollection services, string? redisConnectionString)
    {
        services.AddMemoryCache(); // طبقة L1 المشتركة لكاش الأسعار وغيره.

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<ILowestPriceCache, NoOpLowestPriceCache>();
            return;
        }

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PriceCache.Connect");
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;        // لا تُسقط الإقلاع إن غاب Redis لحظة البدء.
            options.ConnectRetry = 5;
            options.ConnectTimeout = 5000;
            try
            {
                return ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PriceCache] تعذّر الاتصال بـ Redis — سيعيد المحاولة في الخلفية");
                // مع AbortOnConnectFail=false يرجع اتصالاً يعيد المحاولة تلقائياً.
                return ConnectionMultiplexer.Connect(options);
            }
        });

        services.AddSingleton<ILowestPriceCache, RedisLowestPriceCache>();
    }
}
