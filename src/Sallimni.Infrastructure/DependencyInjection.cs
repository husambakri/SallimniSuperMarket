using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sallimni.Application.Services;
using Sallimni.Infrastructure.Services;

namespace Sallimni.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSallimniInfrastructure(
        this IServiceCollection services, string connectionString)
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

        return services;
    }
}
