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

    public async Task<ScanCompareResponse?> ScanCompareAsync(
        string code, double? lat = null, double? lng = null, CancellationToken ct = default)
    {
        var url = $"api/scan-compare/{Uri.EscapeDataString(code)}";
        if (lat.HasValue && lng.HasValue)
            url += $"?lat={lat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                   $"&lng={lng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return await _http.GetFromJsonAsync<ScanCompareResponse>(url, JsonOpts, ct);
    }
}
