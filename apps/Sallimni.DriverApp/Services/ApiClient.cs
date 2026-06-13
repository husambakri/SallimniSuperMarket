using System.Net.Http.Json;
using System.Text.Json;
using Sallimni.DriverApp.Models;

namespace Sallimni.DriverApp.Services;

/// <summary>عميل HTTP لنقاط نهاية السائق في خادم سلّمني.</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    public ApiClient(HttpClient http) => _http = http;

    public async Task<List<DriverInfoDto>> GetDriversAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<DriverInfoDto>>("api/drivers", JsonOpts, ct) ?? new();

    public async Task<List<DriverTaskDto>> GetTasksAsync(Guid driverId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<DriverTaskDto>>($"api/drivers/{driverId}/tasks", JsonOpts, ct) ?? new();

    public async Task StartTaskAsync(Guid taskId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsync($"api/drivers/tasks/{taskId}/start", null, ct), ct);

    public async Task PickupAsync(Guid stopId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsync($"api/drivers/stops/{stopId}/pickup", null, ct), ct);

    public async Task DeliverAsync(Guid stopId, decimal collectedAmount, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsJsonAsync($"api/drivers/stops/{stopId}/deliver", new { CollectedAmount = collectedAmount }, JsonOpts, ct), ct);

    public async Task CompleteTaskAsync(Guid taskId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsync($"api/drivers/tasks/{taskId}/complete", null, ct), ct);

    public async Task DeleteAccountAsync(Guid driverId, CancellationToken ct = default)
        => await EnsureOk(await _http.DeleteAsync($"api/account/driver/{driverId}", ct), ct);

    private static async Task EnsureOk(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new ApiException($"خطأ من الخادم ({(int)resp.StatusCode}): {body}");
        }
    }
}

public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}
