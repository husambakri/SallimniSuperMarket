using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;

namespace Sallimni.Infrastructure.Services;

// ===== نماذج عرض للسائق =====
public record DriverStopRow(Guid StopId, int Sequence, string Label, double Latitude, double Longitude,
    bool IsCompleted, decimal? CodAmount, int ItemCount, DateTimeOffset? EstimatedArrivalAt);

public record DriverTaskRow(Guid TaskId, TaskType Type, Domain.Enums.TaskStatus Status,
    Guid WaveId, DateTimeOffset CreatedAt, List<DriverStopRow> Stops);

/// <summary>
/// خدمات السائق (قسم 5): تنفيذ مهام التجميع والتوزيع، مسح QR على كل مرحلة، والتحصيل عند التسليم.
/// </summary>
public class DriverService
{
    private readonly SallimniDbContext _db;
    public DriverService(SallimniDbContext db) => _db = db;

    public async Task<List<Driver>> GetDriversAsync(CancellationToken ct = default)
        => await _db.Drivers.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<List<DriverTaskRow>> GetTasksAsync(Guid driverId, CancellationToken ct = default)
    {
        var tasks = await _db.DeliveryTasks
            .Where(t => t.DriverId == driverId)
            .Include(t => t.Stops).ThenInclude(s => s.Merchant)
            .Include(t => t.Stops).ThenInclude(s => s.Order)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var rows = new List<DriverTaskRow>();
        foreach (var t in tasks)
        {
            var stops = new List<DriverStopRow>();
            foreach (var s in t.Stops.OrderBy(x => x.Sequence))
            {
                string label;
                decimal? cod = null;
                int itemCount;
                if (t.Type == TaskType.Collection)
                {
                    label = s.Merchant?.Name ?? "متجر";
                    itemCount = await _db.SubOrders
                        .Where(so => so.MerchantId == s.MerchantId && so.Order!.WaveId == t.WaveId)
                        .SelectMany(so => so.Items).CountAsync(ct);
                }
                else
                {
                    label = "زبون";
                    var order = s.Order;
                    cod = order?.GrandTotal;
                    itemCount = order is null ? 0 : await _db.OrderItems
                        .CountAsync(i => i.SubOrder!.OrderId == order.Id, ct);
                }
                stops.Add(new DriverStopRow(s.Id, s.Sequence, label, s.Latitude, s.Longitude, s.IsCompleted, cod, itemCount, s.EstimatedArrivalAt));
            }
            rows.Add(new DriverTaskRow(t.Id, t.Type, t.Status, t.WaveId, t.CreatedAt, stops));
        }
        return rows;
    }

    /// <summary>بدء المهمة: تجميع → InProgress؛ توزيع → تحميل (مستودع→سائق) ووسم OutForDelivery.</summary>
    public async Task StartTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.DeliveryTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException("المهمة غير موجودة.");
        task.Status = Domain.Enums.TaskStatus.InProgress;
        task.StartedAt ??= DateTimeOffset.UtcNow;

        if (task.Type == TaskType.Distribution)
        {
            var subs = await _db.SubOrders.Where(s => s.Order!.WaveId == task.WaveId && s.Status == SubOrderStatus.AtHub).ToListAsync(ct);
            foreach (var s in subs) s.Status = SubOrderStatus.OutForDelivery;
            _db.ScanEvents.Add(new ScanEvent { Stage = ScanStage.HubToDriver, QrCode = task.Id.ToString(), DriverId = task.DriverId, TaskId = task.Id });
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>مسح استلام من متجر (متجر→سائق): وسم الطلبات الفرعية للمتجر PickedUp وإكمال المحطة.</summary>
    public async Task ScanCollectionStopAsync(Guid stopId, CancellationToken ct = default)
    {
        var stop = await _db.RouteStops.Include(s => s.Task).FirstOrDefaultAsync(s => s.Id == stopId, ct)
            ?? throw new InvalidOperationException("المحطة غير موجودة.");
        if (stop.Task!.Type != TaskType.Collection) throw new InvalidOperationException("ليست محطة تجميع.");

        var subs = await _db.SubOrders
            .Where(so => so.MerchantId == stop.MerchantId && so.Order!.WaveId == stop.Task.WaveId)
            .ToListAsync(ct);
        foreach (var so in subs) so.Status = SubOrderStatus.PickedUp;

        _db.ScanEvents.Add(new ScanEvent
        {
            Stage = ScanStage.MerchantToDriver,
            QrCode = stop.MerchantId?.ToString() ?? stop.Id.ToString(),
            DriverId = stop.Task.DriverId,
            TaskId = stop.TaskId
        });

        stop.IsCompleted = true;
        stop.ActualArrivalAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>مسح تسليم لزبون (سائق→زبون) + تحصيل COD: وسم الطلب مُسلَّماً ومحصَّلاً.</summary>
    public async Task ScanDeliveryStopAsync(Guid stopId, decimal collectedAmount, CancellationToken ct = default)
    {
        var stop = await _db.RouteStops.Include(s => s.Task).Include(s => s.Order!).ThenInclude(o => o.SubOrders)
            .FirstOrDefaultAsync(s => s.Id == stopId, ct)
            ?? throw new InvalidOperationException("المحطة غير موجودة.");
        if (stop.Task!.Type != TaskType.Distribution) throw new InvalidOperationException("ليست محطة توزيع.");
        if (stop.Order is null) throw new InvalidOperationException("لا طلب لهذه المحطة.");

        stop.Order.Status = OrderStatus.DeliveredAndPaid;
        stop.Order.DeliveredAt = DateTimeOffset.UtcNow;
        foreach (var so in stop.Order.SubOrders) so.Status = SubOrderStatus.Delivered;

        _db.ScanEvents.Add(new ScanEvent
        {
            Stage = ScanStage.DriverToCustomer,
            QrCode = stop.OrderId?.ToString() ?? stop.Id.ToString(),
            OrderId = stop.OrderId,
            DriverId = stop.Task.DriverId,
            TaskId = stop.TaskId
        });

        stop.IsCompleted = true;
        stop.ActualArrivalAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>إنهاء المهمة: تجميع → تسليم للمستودع (سائق→مستودع) ووسم AtHub؛ ثم Completed.</summary>
    public async Task CompleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.DeliveryTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException("المهمة غير موجودة.");

        if (task.Type == TaskType.Collection)
        {
            var subs = await _db.SubOrders.Where(s => s.Order!.WaveId == task.WaveId && s.Status == SubOrderStatus.PickedUp).ToListAsync(ct);
            foreach (var s in subs) s.Status = SubOrderStatus.AtHub;
            _db.ScanEvents.Add(new ScanEvent { Stage = ScanStage.DriverToHub, QrCode = task.Id.ToString(), DriverId = task.DriverId, TaskId = task.Id });
        }

        task.Status = Domain.Enums.TaskStatus.Completed;
        task.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
