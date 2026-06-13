using System.Net.Http.Json;
using System.Text.Json;
using Sallimni.MerchantApp.Models;

namespace Sallimni.MerchantApp.Services;

/// <summary>عميل HTTP لنقاط نهاية التاجر في خادم سلّمني.</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http) => _http = http;

    public async Task<List<MerchantInfoDto>> GetMerchantsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<MerchantInfoDto>>("api/merchants", JsonOpts, ct) ?? new();

    public async Task<List<MerchantCatalogRowDto>> GetCatalogAsync(Guid merchantId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<MerchantCatalogRowDto>>($"api/merchants/{merchantId}/products", JsonOpts, ct) ?? new();

    public async Task UpsertProductAsync(Guid merchantId, Guid productId, UpsertMerchantProductRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/merchants/{merchantId}/products/{productId}", req, JsonOpts, ct);
        await EnsureOk(resp, ct);
    }

    public async Task<List<MerchantSubOrderDto>> GetSubOrdersAsync(Guid merchantId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<MerchantSubOrderDto>>($"api/merchants/{merchantId}/suborders", JsonOpts, ct) ?? new();

    public async Task UpdateSubOrderStatusAsync(Guid subOrderId, int status, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/merchants/suborders/{subOrderId}/status", new { Status = status }, JsonOpts, ct);
        await EnsureOk(resp, ct);
    }

    public async Task<SubmissionDto> CreateSubmissionAsync(Guid merchantId, CreateSubmissionRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/merchants/{merchantId}/submissions", req, JsonOpts, ct);
        await EnsureOk(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<SubmissionDto>(JsonOpts, ct))!;
    }

    public async Task<List<SubmissionDto>> GetSubmissionsAsync(Guid merchantId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<SubmissionDto>>($"api/merchants/{merchantId}/submissions", JsonOpts, ct) ?? new();

    public async Task DeleteAccountAsync(Guid merchantId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/account/merchant/{merchantId}", ct);
        if (!resp.IsSuccessStatusCode)
            throw new ApiException($"فشل حذف الحساب ({(int)resp.StatusCode}).");
    }

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
