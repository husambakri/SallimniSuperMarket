using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sallimni.Infrastructure;

/// <summary>مصنع وقت التصميم — يتيح إنشاء الهجرات عبر `dotnet ef` دون تشغيل الـ API.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SallimniDbContext>
{
    public SallimniDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("SALLIMNI_DB")
                   ?? "Host=localhost;Port=5432;Database=sallimni;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<SallimniDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new SallimniDbContext(options);
    }
}
