using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Seeding;
using Sallimni.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// قاعدة البيانات: على Railway تأتي عبر DATABASE_URL؛ محليّاً من appsettings.
var connectionString = ResolveConnectionString(builder.Configuration);
// Redis (كاش أقل سعر): REDIS_URL على Railway، أو ConnectionStrings:Redis محليّاً. غيابه ⇒ بديل صامت.
var redisConnectionString = ResolveRedisConnectionString(builder.Configuration);
builder.Services.AddSallimniInfrastructure(connectionString, redisConnectionString);

// تسخين كاش أقل سعر عند الإقلاع + تفعيل AOF + تجديد دوري.
builder.Services.AddHostedService<Sallimni.Api.Services.PriceCacheWarmUpService>();

// مُجمِّع متاجر البقالة الأردنية (مقارنة أسعار حيّة عند مسح الباركود — للتجربة).
builder.Services.AddSingleton(new JordanGrocery.GroceryAggregator());

// مهمّة خلفية تفهرس كتالوجات متاجر طلبات دوريًّا (تُقرأ من الفهرس وقت المسح).
builder.Services.AddHostedService<Sallimni.Api.Services.TalabatIndexService>();

// مهمّة خلفية تسحب كامل كتالوجات المتاجر المستقلّة (ياسر مول…) إلى الفهرس.
builder.Services.AddHostedService<Sallimni.Api.Services.StoreCatalogIndexService>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// المنفذ: Railway يحقن PORT؛ نربط على 0.0.0.0 لقبول الاتصالات الخارجية.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Swagger متاح في كل البيئات لتسهيل تجربة الـ API المنشور.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
// صفحات الويب العامّة (سياسة الخصوصية، الشروط، حذف الحساب) — قسم 15.
app.UseStaticFiles();
app.MapControllers();

// فحص صحّة للنشر + جذر يوجّه إلى Swagger.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "sallimni-api" }));
app.MapGet("/", () => Results.Redirect("/swagger"));

// الهجرات والبذر تعمل في الخلفية: لا تؤخّر إقلاع الخادم ولا تُسقطه إن تأخّرت القاعدة،
// فيستجيب /health فوراً وينجح فحص النشر، وتكتمل الهجرة بمجرّد توفّر DATABASE_URL.
_ = MigrateAndSeedAsync(app);

app.Run();


// ===== مساعدات =====

static string ResolveConnectionString(IConfiguration config)
{
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(dbUrl))
        return ConvertDatabaseUrl(dbUrl);

    return config.GetConnectionString("Sallimni")
        ?? "Host=localhost;Port=5432;Database=sallimni;Username=postgres;Password=postgres";
}

// يحلّ اتصال Redis: REDIS_URL (redis://[:pass@]host:port) من البيئة، أو ConnectionStrings:Redis.
// يرجع null إن لم يُضبط شيء — فيعمل الخادم بكاش صامت دون Redis.
static string? ResolveRedisConnectionString(IConfiguration config)
{
    var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
    if (!string.IsNullOrWhiteSpace(redisUrl))
        return ConvertRedisUrl(redisUrl);

    var fromConfig = config.GetConnectionString("Redis");
    return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig;
}

// يحوّل redis://default:pass@host:port (أو rediss:// مع TLS) إلى صيغة StackExchange.Redis.
static string ConvertRedisUrl(string url)
{
    var uri = new Uri(url);
    var port = uri.Port > 0 ? uri.Port : 6379;
    var opts = $"{uri.Host}:{port},abortConnect=false";

    if (!string.IsNullOrEmpty(uri.UserInfo))
    {
        var parts = uri.UserInfo.Split(':', 2);
        if (parts.Length == 2 && parts[1].Length > 0)
            opts += $",password={Uri.UnescapeDataString(parts[1])}";
        if (parts[0].Length > 0 && !string.Equals(parts[0], "default", StringComparison.OrdinalIgnoreCase))
            opts += $",user={Uri.UnescapeDataString(parts[0])}";
    }

    if (string.Equals(uri.Scheme, "rediss", StringComparison.OrdinalIgnoreCase))
        opts += ",ssl=true";

    return opts;
}

// يحوّل DATABASE_URL (postgresql://user:pass@host:port/db) إلى سلسلة اتصال Npgsql.
static string ConvertDatabaseUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);
    var user = Uri.UnescapeDataString(userInfo[0]);
    var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var db = uri.AbsolutePath.TrimStart('/');
    var dbPort = uri.Port > 0 ? uri.Port : 5432;
    // SSL Mode=Prefer يعمل لكلٍّ من الشبكة الداخلية (بلا SSL) والاتصال الخارجي (مع SSL).
    return $"Host={uri.Host};Port={dbPort};Database={db};Username={user};Password={pass};" +
           "SSL Mode=Prefer;Trust Server Certificate=true";
}

// تُشغَّل في الخلفية: تُعيد المحاولة حتى تتوفّر القاعدة دون أن تُسقط التطبيق إطلاقاً.
static async Task MigrateAndSeedAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();
            await db.Database.MigrateAsync();
            await DataSeeder.SeedAsync(db, logger);
            logger.LogInformation("Database migrated and seeded successfully.");
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning("DB not ready (attempt {Attempt}/30): {Message}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
    logger.LogError("Database migration did not complete after retries. Ensure DATABASE_URL is linked.");
}
