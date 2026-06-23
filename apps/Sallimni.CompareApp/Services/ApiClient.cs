using System.Net.Http.Json;
using System.Text.Json;
using Sallimni.CompareApp.Models;

namespace Sallimni.CompareApp.Services;

/// <summary>عميل بسيط لنقطة مقارنة السعر في الخادم.</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http) => _http = http;

    public async Task<ScanCompareResponse?> ScanCompareAsync(string code, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<ScanCompareResponse>(
            $"api/scan-compare/{Uri.EscapeDataString(code)}", JsonOpts, ct);
}
