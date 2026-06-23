using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sallimni.Application.Abstractions;
using StackExchange.Redis;

namespace Sallimni.Infrastructure.Services;

/// <summary>
/// كاش أقل سعر بطبقتين:
///   • L1 — ذاكرة داخل العملية (IMemoryCache) لتفادي ذهاب/إياب Redis للباركودات الساخنة.
///   • L2 — Redis، المرجع الموثوق المحفوظ بـ AOF، مفهرس بالباركود مع TTL (أسعار نشطة فقط).
///
/// عند تغيّر السعر (Event Handler) نكتب L2 ثم نَنشُر الباركود على قناة Pub/Sub، فتُبطِل
/// كل نسخ الخادم مدخلها في L1 فوراً — فلا يبقى سعر قديم في ذاكرة أي نسخة.
///
/// مفرد (Singleton): يملك اتصال Redis ويشترك في قناة الإبطال مرّة واحدة عند الإنشاء.
/// </summary>
public sealed class RedisLowestPriceCache : ILowestPriceCache, IDisposable
{
    private const string KeyPrefix = "sallimni:lp:";              // sallimni:lp:{barcode}
    private const string InvalidationChannel = "sallimni:lp:invalidate";

    // L2: تبقى الأسعار النشطة فقط؛ يُعاد التأكيد عند كل تغيير وعند Warm-up اليومي.
    private static readonly TimeSpan L2Ttl = TimeSpan.FromDays(14);
    // L1: نافذة قصيرة جداً — الإبطال الفوري يأتي من Pub/Sub، وهذه حدٌّ احتياطي.
    private static readonly TimeSpan L1Ttl = TimeSpan.FromSeconds(60);

    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _l1;
    private readonly ILogger<RedisLowestPriceCache> _logger;

    public RedisLowestPriceCache(
        IConnectionMultiplexer redis, IMemoryCache l1, ILogger<RedisLowestPriceCache> logger)
    {
        _redis = redis;
        _l1 = l1;
        _logger = logger;

        // اشتراك إبطال L1 عبر النسخ: أي نسخة تغيّر سعراً تنشر الباركود، فنُسقطه من L1 هنا.
        try
        {
            _redis.GetSubscriber().Subscribe(
                RedisChannel.Literal(InvalidationChannel),
                (_, value) =>
                {
                    var barcode = (string?)value;
                    if (!string.IsNullOrEmpty(barcode)) _l1.Remove(L1Key(barcode));
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PriceCache] تعذّر الاشتراك في قناة الإبطال — سيعتمد L1 على TTL فقط");
        }
    }

    public async Task<LowestPrice?> GetAsync(string barcode, CancellationToken ct = default)
    {
        barcode = Normalize(barcode);
        if (barcode.Length == 0) return null;

        if (_l1.TryGetValue(L1Key(barcode), out LowestPrice? cached))
            return cached;

        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(KeyPrefix + barcode);
            if (raw.IsNullOrEmpty) return null;

            var value = Deserialize(raw!);
            if (value is not null)
                _l1.Set(L1Key(barcode), value, L1Ttl);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[PriceCache] فشل قراءة {Barcode} من Redis — رجوع للقاعدة", barcode);
            return null;
        }
    }

    public async Task SetAsync(string barcode, LowestPrice value, CancellationToken ct = default)
    {
        barcode = Normalize(barcode);
        if (barcode.Length == 0) return;

        _l1.Set(L1Key(barcode), value, L1Ttl);
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(KeyPrefix + barcode, Serialize(value), L2Ttl);
            // إبطال L1 في بقيّة النسخ (تقرأ القيمة الجديدة من L2 عند الحاجة).
            await db.PublishAsync(RedisChannel.Literal(InvalidationChannel), barcode);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[PriceCache] فشل كتابة {Barcode} في Redis", barcode);
        }
    }

    public async Task RemoveAsync(string barcode, CancellationToken ct = default)
    {
        barcode = Normalize(barcode);
        if (barcode.Length == 0) return;

        _l1.Remove(L1Key(barcode));
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(KeyPrefix + barcode);
            await db.PublishAsync(RedisChannel.Literal(InvalidationChannel), barcode);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[PriceCache] فشل حذف {Barcode} من Redis", barcode);
        }
    }

    public async Task EnsureDurabilityAsync(CancellationToken ct = default)
    {
        try
        {
            // تفعيل AOF على الخادم حتى لا تضيع الأسعار عند إعادة التشغيل (best-effort:
            // مزوّدات Redis المُدارة قد تمنع CONFIG — يكفي عندها ضبطها من redis.conf).
            var endpoint = _redis.GetEndPoints().FirstOrDefault();
            if (endpoint is null) return;
            var server = _redis.GetServer(endpoint);
            await server.ConfigSetAsync("appendonly", "yes");
            await server.ConfigSetAsync("appendfsync", "everysec");
            _logger.LogInformation("[PriceCache] تم تفعيل AOF (appendonly=yes, appendfsync=everysec)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PriceCache] تعذّر تفعيل AOF عبر CONFIG — اضبطه في redis.conf");
        }
    }

    // ===== مساعدات =====

    private static string L1Key(string barcode) => KeyPrefix + barcode;
    private static string Normalize(string? barcode) => (barcode ?? string.Empty).Trim();

    // تنسيق مدمج بسيط: price|regular|merchantGuid (يتفادى كلفة JSON لقيمة صغيرة ثابتة).
    private static string Serialize(LowestPrice v) =>
        string.Create(CultureInfo.InvariantCulture, $"{v.Price}|{v.RegularPrice}|{v.MerchantId:N}");

    private static LowestPrice? Deserialize(string raw)
    {
        var parts = raw.Split('|');
        if (parts.Length != 3) return null;
        if (decimal.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var price) &&
            decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var regular) &&
            Guid.TryParse(parts[2], out var merchantId))
            return new LowestPrice(price, regular, merchantId);
        return null;
    }

    public void Dispose()
    {
        try { _redis.GetSubscriber().Unsubscribe(RedisChannel.Literal(InvalidationChannel)); }
        catch { /* تجاهُل عند الإغلاق */ }
    }
}
