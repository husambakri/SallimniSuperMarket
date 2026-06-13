using Microsoft.EntityFrameworkCore;
using Sallimni.Application.Services;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;

namespace Sallimni.Infrastructure.Services;

// ===== نماذج عرض للإدارة =====
public record WaveSummary(Guid WaveId, WaveStatus Status, DateTimeOffset CollectionStartAt,
    DateTimeOffset DistributionStartAt, int OrderCount, int SubOrderCount,
    bool HasCollectionTask, bool HasDistributionTask);

public record TaskStopRow(int Sequence, string Label, double Latitude, double Longitude, bool IsCompleted);

public record TaskRow(Guid TaskId, TaskType Type, Domain.Enums.TaskStatus Status,
    Guid WaveId, Guid? DriverId, string? DriverName, List<TaskStopRow> Stops);

public record SettlementRow(Guid SubOrderId, Guid MerchantId, string MerchantName,
    SubOrderStatus Status, decimal SubtotalInclTax, decimal CommissionAmount, decimal MerchantPayout);

public record AdminProductRow(Guid Id, string NameAr, string NameEn, string? Barcode, string? UnitSize,
    string? Emoji, int TaxClass, Guid CategoryId, string CategoryNameAr, int MerchantCount, bool IsActive);

/// <summary>
/// خدمات الإدارة (قسم 2): تأسيس الأصناف واعتماد طلبات التجار، إنشاء مهام التوصيل،
/// ضبط العمولة والموجات، والتسويات.
/// </summary>
public class AdminService
{
    private readonly SallimniDbContext _db;
    public AdminService(SallimniDbContext db) => _db = db;

    // موقع المستودع المركزي (بين دائرتَي التجار والزبائن) + ثوابت المسار.
    private static readonly GeoPoint Hub = new(31.955, 35.915);
    private const double AvgSpeedKmh = 30.0;
    private const double ServiceMinutesPerStop = 5.0;

    // ===== الكتالوج واعتماد الطلبات =====

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
        => await _db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.NameAr).ToListAsync(ct);

    public async Task<Category> CreateCategoryAsync(string nameAr, string nameEn, string? icon, CancellationToken ct = default)
    {
        var nextSort = (await _db.Categories.MaxAsync(c => (int?)c.SortOrder, ct) ?? 0) + 1;
        var c = new Category
        {
            NameAr = nameAr, NameEn = nameEn,
            Icon = string.IsNullOrWhiteSpace(icon) ? "📦" : icon, SortOrder = nextSort
        };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    /// <summary>تأسيس بطاقة صنف رئيسية (تملكها الإدارة — قسم 3).</summary>
    public async Task<Product> CreateProductAsync(
        string nameAr, string nameEn, string? barcode, string? unitSize, string? emoji,
        string? description, Guid categoryId, TaxClass taxClass, CancellationToken ct = default)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == categoryId, ct))
            throw new InvalidOperationException("التصنيف غير موجود.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new InvalidOperationException("الاسم بالعربية مطلوب.");
        var p = new Product
        {
            NameAr = nameAr, NameEn = nameEn, Barcode = barcode, UnitSize = unitSize,
            Emoji = string.IsNullOrWhiteSpace(emoji) ? "🛒" : emoji,
            Description = description, CategoryId = categoryId, TaxClass = taxClass
        };
        _db.Products.Add(p);
        await _db.SaveChangesAsync(ct);
        return p;
    }

    /// <summary>قائمة الأصناف للإدارة (مع اسم التصنيف وعدد التجار الذين يبيعونها).</summary>
    public async Task<List<AdminProductRow>> GetProductsAsync(CancellationToken ct = default)
        => await _db.Products
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminProductRow(
                p.Id, p.NameAr, p.NameEn, p.Barcode, p.UnitSize, p.Emoji,
                (int)p.TaxClass, p.CategoryId, p.Category!.NameAr,
                p.MerchantProducts.Count(mp => mp.IsAvailable), p.IsActive))
            .ToListAsync(ct);

    public async Task<List<ProductSubmission>> GetPendingSubmissionsAsync(CancellationToken ct = default)
        => await _db.ProductSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

    /// <summary>اعتماد طلب صنف: يُنشئ بطاقة Product رئيسية ويربط الطلب بها.</summary>
    public async Task<Product> ApproveSubmissionAsync(
        Guid submissionId, Guid categoryId, CancellationToken ct = default)
    {
        var s = await _db.ProductSubmissions.FirstOrDefaultAsync(x => x.Id == submissionId, ct)
            ?? throw new InvalidOperationException("الطلب غير موجود.");
        if (s.Status != SubmissionStatus.Pending)
            throw new InvalidOperationException("الطلب غير معلّق.");

        var catId = categoryId != Guid.Empty
            ? categoryId
            : (await _db.Categories.Select(c => c.Id).FirstOrDefaultAsync(ct));
        if (catId == Guid.Empty)
            throw new InvalidOperationException("لا يوجد تصنيف لإسناد الصنف إليه.");

        var product = new Product
        {
            NameAr = s.NameAr, NameEn = s.NameEn, Barcode = s.Barcode,
            UnitSize = s.UnitSize, CategoryId = catId, TaxClass = s.SuggestedTaxClass
        };
        _db.Products.Add(product);

        s.Status = SubmissionStatus.Approved;
        s.ApprovedProductId = product.Id;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return product;
    }

    public async Task RejectSubmissionAsync(Guid submissionId, string? note, CancellationToken ct = default)
    {
        var s = await _db.ProductSubmissions.FirstOrDefaultAsync(x => x.Id == submissionId, ct)
            ?? throw new InvalidOperationException("الطلب غير موجود.");
        s.Status = SubmissionStatus.Rejected;
        s.ReviewNote = note;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ===== الموجات والمهام =====

    public async Task<List<WaveSummary>> GetWavesAsync(CancellationToken ct = default)
    {
        var waves = await _db.Waves
            .Include(w => w.Orders).ThenInclude(o => o.SubOrders)
            .Include(w => w.Tasks)
            .OrderByDescending(w => w.CollectionStartAt)
            .ToListAsync(ct);

        return waves.Select(w => new WaveSummary(
            w.Id, w.Status, w.CollectionStartAt, w.DistributionStartAt,
            w.Orders.Count, w.Orders.Sum(o => o.SubOrders.Count),
            w.Tasks.Any(t => t.Type == TaskType.Collection),
            w.Tasks.Any(t => t.Type == TaskType.Distribution))).ToList();
    }

    /// <summary>مهمة تجميع: محطة لكل متجر فائز في الموجة (متاجر ← مستودع).</summary>
    public async Task<DeliveryTask> CreateCollectionTaskAsync(Guid waveId, CancellationToken ct = default)
    {
        var wave = await _db.Waves
            .Include(w => w.Orders).ThenInclude(o => o.SubOrders).ThenInclude(s => s.Merchant)
            .FirstOrDefaultAsync(w => w.Id == waveId, ct)
            ?? throw new InvalidOperationException("الموجة غير موجودة.");

        if (await _db.DeliveryTasks.AnyAsync(t => t.WaveId == waveId && t.Type == TaskType.Collection, ct))
            throw new InvalidOperationException("مهمة التجميع موجودة لهذه الموجة.");

        var merchants = wave.Orders
            .SelectMany(o => o.SubOrders)
            .Select(s => s.Merchant!)
            .DistinctBy(m => m.Id)
            .ToList();
        if (merchants.Count == 0)
            throw new InvalidOperationException("لا طلبات في الموجة.");

        var task = new DeliveryTask { Type = TaskType.Collection, WaveId = waveId };
        var stopsByMerchant = merchants.ToDictionary(
            m => m.Id,
            m => new RouteStop { MerchantId = m.Id, Latitude = m.Latitude ?? 0, Longitude = m.Longitude ?? 0 });

        // تحسين ترتيب زيارة المتاجر بالجار الأقرب انطلاقاً من المستودع.
        var route = RouteOptimizer.Optimize(
            Hub,
            merchants.Select(m => new RouteStopInput(m.Id, new GeoPoint(m.Latitude ?? 0, m.Longitude ?? 0))).ToList(),
            AvgSpeedKmh, ServiceMinutesPerStop);
        foreach (var r in route.Stops)
        {
            var stop = stopsByMerchant[r.Id];
            stop.Sequence = r.Sequence;
            task.Stops.Add(stop);
        }

        _db.DeliveryTasks.Add(task);
        wave.Status = WaveStatus.Collecting;
        await _db.SaveChangesAsync(ct);
        return task;
    }

    /// <summary>
    /// مهمة توزيع: محطة لكل زبون (مستودع ← زبائن). يُحسّن الترتيب بالجار الأقرب،
    /// ويحسب وقت وصول تقديري لكل محطة، ويحدّث وقت تسليم كل طلب من تقدير المتوسط لقيمة المسار (قسم 6.2).
    /// </summary>
    public async Task<DeliveryTask> CreateDistributionTaskAsync(Guid waveId, CancellationToken ct = default)
    {
        var wave = await _db.Waves
            .Include(w => w.Orders)
            .FirstOrDefaultAsync(w => w.Id == waveId, ct)
            ?? throw new InvalidOperationException("الموجة غير موجودة.");

        if (await _db.DeliveryTasks.AnyAsync(t => t.WaveId == waveId && t.Type == TaskType.Distribution, ct))
            throw new InvalidOperationException("مهمة التوزيع موجودة لهذه الموجة.");
        if (wave.Orders.Count == 0)
            throw new InvalidOperationException("لا طلبات في الموجة.");

        var task = new DeliveryTask { Type = TaskType.Distribution, WaveId = waveId };
        var stopsByOrder = wave.Orders.ToDictionary(
            o => o.Id,
            o => new RouteStop { OrderId = o.Id, Latitude = o.DeliveryLatitude, Longitude = o.DeliveryLongitude });
        var ordersById = wave.Orders.ToDictionary(o => o.Id);

        // تحسين المسار + وقت وصول تراكمي انطلاقاً من المستودع وقت بدء موجة التوزيع.
        var route = RouteOptimizer.Optimize(
            Hub,
            wave.Orders.Select(o => new RouteStopInput(o.Id, new GeoPoint(o.DeliveryLatitude, o.DeliveryLongitude))).ToList(),
            AvgSpeedKmh, ServiceMinutesPerStop, wave.DistributionStartAt);

        foreach (var r in route.Stops)
        {
            var stop = stopsByOrder[r.Id];
            stop.Sequence = r.Sequence;
            stop.EstimatedArrivalAt = r.ArrivalAt;
            task.Stops.Add(stop);

            // تحديث وقت تسليم الطلب بقيمة المسار الفعلية (بدل تقدير T_transit المتوسط).
            if (r.ArrivalAt is not null && ordersById.TryGetValue(r.Id, out var order))
                order.EstimatedDeliveryAt = r.ArrivalAt;
        }

        _db.DeliveryTasks.Add(task);
        wave.Status = WaveStatus.Distributing;
        await _db.SaveChangesAsync(ct);
        return task;
    }

    public async Task AssignDriverAsync(Guid taskId, Guid driverId, CancellationToken ct = default)
    {
        var task = await _db.DeliveryTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException("المهمة غير موجودة.");
        if (!await _db.Drivers.AnyAsync(d => d.Id == driverId, ct))
            throw new InvalidOperationException("السائق غير موجود.");
        task.DriverId = driverId;
        task.Status = Domain.Enums.TaskStatus.Assigned;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<TaskRow>> GetTasksAsync(CancellationToken ct = default)
    {
        var tasks = await _db.DeliveryTasks
            .Include(t => t.Driver)
            .Include(t => t.Stops).ThenInclude(s => s.Merchant)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tasks.Select(t => new TaskRow(
            t.Id, t.Type, t.Status, t.WaveId, t.DriverId, t.Driver?.Name,
            t.Stops.OrderBy(s => s.Sequence).Select(s => new TaskStopRow(
                s.Sequence,
                s.Merchant?.Name ?? (s.OrderId != null ? "زبون" : "محطة"),
                s.Latitude, s.Longitude, s.IsCompleted)).ToList())).ToList();
    }

    public async Task<List<Driver>> GetDriversAsync(CancellationToken ct = default)
        => await _db.Drivers.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(ct);

    // ===== الإعدادات =====

    public async Task<CommissionConfig> GetCommissionConfigAsync(CancellationToken ct = default)
    {
        var cfg = await _db.CommissionConfigs.FirstOrDefaultAsync(c => c.MerchantId == null && c.IsActive, ct);
        if (cfg is null)
        {
            cfg = new CommissionConfig { DefaultRate = 0.10m };
            _db.CommissionConfigs.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    public async Task UpdateCommissionRateAsync(decimal rate, CancellationToken ct = default)
    {
        var cfg = await GetCommissionConfigAsync(ct);
        cfg.DefaultRate = rate;
        cfg.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<WaveConfig> GetWaveConfigAsync(CancellationToken ct = default)
    {
        var cfg = await _db.WaveConfigs.FirstOrDefaultAsync(c => c.IsActive, ct);
        if (cfg is null)
        {
            cfg = new WaveConfig();
            _db.WaveConfigs.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    public async Task UpdateWaveConfigAsync(
        int waveIntervalMinutes, int distributionGapMinutes, int defaultPrepMinutes,
        int defaultTransitMinutes, int maxCustomersPerDriver, CancellationToken ct = default)
    {
        var cfg = await GetWaveConfigAsync(ct);
        cfg.WaveIntervalMinutes = waveIntervalMinutes;
        cfg.DistributionGapMinutes = distributionGapMinutes;
        cfg.DefaultPrepMinutes = defaultPrepMinutes;
        cfg.DefaultTransitMinutes = defaultTransitMinutes;
        cfg.MaxCustomersPerDriver = maxCustomersPerDriver;
        cfg.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ===== التسويات =====

    /// <summary>ملخّص التسويات لكل طلب فرعي: المستحق للتاجر بعد العمولة وحالته.</summary>
    public async Task<List<SettlementRow>> GetSettlementsAsync(CancellationToken ct = default)
        => await _db.SubOrders
            .Include(s => s.Merchant)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SettlementRow(
                s.Id, s.MerchantId, s.Merchant!.Name, s.Status,
                s.SubtotalInclTax, s.CommissionAmount, s.MerchantPayout))
            .ToListAsync(ct);

    /// <summary>وسم طلب فرعي كمُسوّى (دفع التاجر بعد العمولة — قسم 7).</summary>
    public async Task MarkSettledAsync(Guid subOrderId, CancellationToken ct = default)
    {
        var sub = await _db.SubOrders.FirstOrDefaultAsync(s => s.Id == subOrderId, ct)
            ?? throw new InvalidOperationException("الطلب الفرعي غير موجود.");

        sub.Status = SubOrderStatus.Settled;
        sub.UpdatedAt = DateTimeOffset.UtcNow;

        // سجلّ تسوية موثّق.
        _db.Settlements.Add(new Settlement
        {
            SubOrderId = sub.Id,
            MerchantId = sub.MerchantId,
            CollectedAmount = sub.SubtotalInclTax,
            CommissionAmount = sub.CommissionAmount,
            MerchantPayout = sub.MerchantPayout,
            Status = SettlementStatus.Paid,
            PaidAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
