using Microsoft.AspNetCore.Mvc;
using Sallimni.Api.Dtos;
using Sallimni.Infrastructure.Services;

namespace Sallimni.Api.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController : ControllerBase
{
    private readonly DriverService _drivers;
    public DriversController(DriverService drivers) => _drivers = drivers;

    /// <summary>قائمة السائقين (لاختيار السائق الحالي).</summary>
    [HttpGet]
    public async Task<ActionResult<List<DriverInfoDto>>> GetAll(CancellationToken ct)
        => (await _drivers.GetDriversAsync(ct)).Select(d => new DriverInfoDto(d.Id, d.Name, d.Phone)).ToList();

    /// <summary>مهام السائق (تجميع/توزيع) مع محطّاتها.</summary>
    [HttpGet("{driverId:guid}/tasks")]
    public async Task<ActionResult<List<DriverTaskDto>>> GetTasks(Guid driverId, CancellationToken ct)
        => (await _drivers.GetTasksAsync(driverId, ct)).Select(t => new DriverTaskDto(
            t.TaskId, (int)t.Type, (int)t.Status, t.WaveId, t.CreatedAt,
            t.Stops.Select(s => new DriverStopDto(s.StopId, s.Sequence, s.Label, s.Latitude, s.Longitude,
                s.IsCompleted, s.CodAmount, s.ItemCount, s.EstimatedArrivalAt)).ToList())).ToList();

    [HttpPost("tasks/{taskId:guid}/start")]
    public async Task<IActionResult> Start(Guid taskId, CancellationToken ct)
    {
        try { await _drivers.StartTaskAsync(taskId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>مسح QR استلام من متجر (متجر→سائق).</summary>
    [HttpPost("stops/{stopId:guid}/pickup")]
    public async Task<IActionResult> Pickup(Guid stopId, CancellationToken ct)
    {
        try { await _drivers.ScanCollectionStopAsync(stopId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>مسح QR تسليم لزبون + تحصيل (سائق→زبون).</summary>
    [HttpPost("stops/{stopId:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid stopId, [FromBody] DeliverRequest req, CancellationToken ct)
    {
        try { await _drivers.ScanDeliveryStopAsync(stopId, req.CollectedAmount, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>إنهاء المهمة (تجميع → تسليم للمستودع).</summary>
    [HttpPost("tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid taskId, CancellationToken ct)
    {
        try { await _drivers.CompleteTaskAsync(taskId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
