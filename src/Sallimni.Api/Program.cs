using Microsoft.EntityFrameworkCore;
using Sallimni.Api.Seeding;
using Sallimni.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// قاعدة البيانات: على Railway تأتي عبر DATABASE_URL؛ محليّاً من appsettings.
var connectionString = ResolveConnectionString(builder.Configuration);
builder.Services.AddSallimniInfrastructure(connectionString);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// المنفذ: Railway يحقن PORT؛ نربط على 0.0.0.0 لقبول الاتصالات الخارجية.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// تطبيق الهجرات + بذر بيانات أولية (يبذر فقط إن كانت القاعدة فارغة) — مع إعادة محاولة لإقلاع القاعدة.
await MigrateAndSeedAsync(app);

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

static async Task MigrateAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SallimniDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            await DataSeeder.SeedAsync(db);
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning("DB not ready (attempt {Attempt}/10): {Message}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
