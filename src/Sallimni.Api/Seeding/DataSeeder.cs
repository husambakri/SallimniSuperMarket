using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Seeding;

/// <summary>
/// البذرة الأولية: الإعدادات (العمولة/الموجات) + سائق وزبون تجريبي بعناوين،
/// ثم استيراد الكتالوج الحقيقي (طلبات-الأردن) عبر <see cref="CatalogImporter"/>.
/// تعمل مرّة واحدة على قاعدة فارغة (الحارس: وجود تجّار).
/// </summary>
public static class DataSeeder
{
    /// <summary>المسار الافتراضي لملف بيانات الكتالوج (يُنسخ بجانب الـ DLL).</summary>
    public static string DefaultCatalogPath =>
        Path.Combine(AppContext.BaseDirectory, "Seeding", "catalog-seed.tsv");

    public static async Task SeedAsync(SallimniDbContext db, ILogger logger)
    {
        if (await db.Merchants.AnyAsync()) return;

        await SeedEssentialsAsync(db);
        await CatalogImporter.ImportAsync(db, DefaultCatalogPath, logger);
    }

    /// <summary>الإعدادات والأطراف التجريبية اللازمة لتشغيل التطبيقات (بلا كتالوج).</summary>
    public static async Task SeedEssentialsAsync(SallimniDbContext db)
    {
        if (!await db.CommissionConfigs.AnyAsync())
            db.CommissionConfigs.Add(new CommissionConfig { DefaultRate = 0.10m, MerchantId = null });
        if (!await db.WaveConfigs.AnyAsync())
            db.WaveConfigs.Add(new WaveConfig());

        if (!await db.Drivers.AnyAsync())
            db.Drivers.Add(new Driver { Name = "سائق 1", Phone = "0790000000" });

        if (!await db.Customers.AnyAsync())
        {
            var cust = new Customer { Name = "زبون تجريبي", Phone = "0791111111" };
            db.Customers.Add(cust);
            db.Addresses.AddRange(
                new Address { Customer = cust, Label = "البيت", Line = "عمان - شارع المثال", Latitude = 31.980, Longitude = 35.900, IsDefault = true },
                new Address { Customer = cust, Label = "العمل", Line = "عمان - الدوار الخامس", Latitude = 31.965, Longitude = 35.885 },
                new Address { Customer = cust, Label = "بيت الأهل", Line = "عمان - الجاردنز", Latitude = 31.995, Longitude = 35.870 });
        }

        await db.SaveChangesAsync();
    }
}
