using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;

namespace Sallimni.Infrastructure;

public class SallimniDbContext : DbContext
{
    public SallimniDbContext(DbContextOptions<SallimniDbContext> options) : base(options) { }

    // الكتالوج
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<MerchantProduct> MerchantProducts => Set<MerchantProduct>();
    public DbSet<ProductSubmission> ProductSubmissions => Set<ProductSubmission>();
    public DbSet<HubProduct> HubProducts => Set<HubProduct>();
    public DbSet<BarcodeScan> BarcodeScans => Set<BarcodeScan>();

    // الأطراف
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();

    // الطلبات
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<SubOrder> SubOrders => Set<SubOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // اللوجستيات
    public DbSet<Wave> Waves => Set<Wave>();
    public DbSet<DeliveryTask> DeliveryTasks => Set<DeliveryTask>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<ScanEvent> ScanEvents => Set<ScanEvent>();

    // المالية والإعدادات
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<CommissionConfig> CommissionConfigs => Set<CommissionConfig>();
    public DbSet<WaveConfig> WaveConfigs => Set<WaveConfig>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // كل القيم المالية بدقّة 18,4 (تكفي للأسعار والنسب).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SallimniDbContext).Assembly);
    }
}
