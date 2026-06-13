using System.Net.Http.Json;
using System.Text.Json;
using Sallimni.AdminApp.Models;

namespace Sallimni.AdminApp.Services;

/// <summary>عميل HTTP لنقاط نهاية الإدارة في خادم سلّمني.</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http) => _http = http;

    // الاعتمادات والكتالوج
    public async Task<List<AdminSubmissionDto>> GetSubmissionsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<AdminSubmissionDto>>("api/admin/submissions", JsonOpts, ct) ?? new();

    public async Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<CategoryDto>>("api/admin/categories", JsonOpts, ct) ?? new();

    // تأسيس الأصناف
    public async Task<List<AdminProductDto>> GetProductsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<AdminProductDto>>("api/admin/products", JsonOpts, ct) ?? new();

    public async Task CreateProductAsync(CreateProductRequest req, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsJsonAsync("api/admin/products", req, JsonOpts, ct), ct);

    public async Task CreateCategoryAsync(CreateCategoryRequest req, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsJsonAsync("api/admin/categories", req, JsonOpts, ct), ct);

    public async Task UpdateProductAsync(Guid id, CreateProductRequest req, CancellationToken ct = default)
        => await EnsureOk(await _http.PutAsJsonAsync($"api/admin/products/{id}", req, JsonOpts, ct), ct);

    public async Task DeleteProductAsync(Guid id, CancellationToken ct = default)
        => await EnsureOk(await _http.DeleteAsync($"api/admin/products/{id}", ct), ct);

    public async Task UploadProductImageAsync(Guid id, Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        using var content = new System.Net.Http.MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        await EnsureOk(await _http.PostAsync($"api/admin/products/{id}/image", content, ct), ct);
    }

    public async Task UpdateCategoryAsync(Guid id, CreateCategoryRequest req, CancellationToken ct = default)
        => await EnsureOk(await _http.PutAsJsonAsync($"api/admin/categories/{id}", req, JsonOpts, ct), ct);

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
        => await EnsureOk(await _http.DeleteAsync($"api/admin/categories/{id}", ct), ct);

    public async Task ApproveSubmissionAsync(Guid id, Guid categoryId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsJsonAsync($"api/admin/submissions/{id}/approve", new { CategoryId = categoryId }, JsonOpts, ct), ct);

    public async Task RejectSubmissionAsync(Guid id, string? note, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsJsonAsync($"api/admin/submissions/{id}/reject", new { Note = note }, JsonOpts, ct), ct);

    // الموجات والمهام
    public async Task<List<WaveSummaryDto>> GetWavesAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<WaveSummaryDto>>("api/admin/waves", JsonOpts, ct) ?? new();

    public async Task CreateCollectionTaskAsync(Guid waveId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsync($"api/admin/waves/{waveId}/collection-task", null, ct), ct);

    public async Task CreateDistributionTaskAsync(Guid waveId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsync($"api/admin/waves/{waveId}/distribution-task", null, ct), ct);

    public async Task<List<TaskDto>> GetTasksAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<TaskDto>>("api/admin/tasks", JsonOpts, ct) ?? new();

    public async Task<List<DriverDto>> GetDriversAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<DriverDto>>("api/admin/drivers", JsonOpts, ct) ?? new();

    public async Task AssignDriverAsync(Guid taskId, Guid driverId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsJsonAsync($"api/admin/tasks/{taskId}/assign", new { DriverId = driverId }, JsonOpts, ct), ct);

    // الإعدادات
    public async Task<CommissionConfigDto> GetCommissionAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<CommissionConfigDto>("api/admin/commission", JsonOpts, ct) ?? new();

    public async Task UpdateCommissionAsync(decimal rate, CancellationToken ct = default)
        => await EnsureOk(await _http.PutAsJsonAsync("api/admin/commission", new { DefaultRate = rate }, JsonOpts, ct), ct);

    public async Task<WaveConfigDto> GetWaveConfigAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<WaveConfigDto>("api/admin/wave-config", JsonOpts, ct) ?? new();

    public async Task UpdateWaveConfigAsync(WaveConfigDto cfg, CancellationToken ct = default)
        => await EnsureOk(await _http.PutAsJsonAsync("api/admin/wave-config", cfg, JsonOpts, ct), ct);

    // التسويات
    public async Task<List<SettlementRowDto>> GetSettlementsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<SettlementRowDto>>("api/admin/settlements", JsonOpts, ct) ?? new();

    public async Task SettleAsync(Guid subOrderId, CancellationToken ct = default)
        => await EnsureOk(await _http.PostAsync($"api/admin/settlements/{subOrderId}/settle", null, ct), ct);

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
