using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Sallimni.ValidationApp.Models;

namespace Sallimni.ValidationApp.Services;

/// <summary>عميل نقاط تحقّق الأسعار في الخادم (/api/validation).</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static string Inv(double v) => v.ToString(CultureInfo.InvariantCulture);

    public ApiClient(HttpClient http) => _http = http;

    /// <summary>أقرب فرع للموقع + سعرنا المخزّن للباركود فيه.</summary>
    public async Task<ValidationLookupDto?> LookupAsync(string code, double lat, double lng, CancellationToken ct = default)
    {
        var url = $"api/validation/lookup?barcode={Uri.EscapeDataString(code)}&lat={Inv(lat)}&lng={Inv(lng)}";
        return await _http.GetFromJsonAsync<ValidationLookupDto>(url, JsonOpts, ct);
    }

    /// <summary>يسجّل عملية تحقّق (صفّ تاريخي). يرمي عند فشل الخادم.</summary>
    public async Task RecordAsync(ValidationRecordRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/validation/record", req, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"فشل التسجيل ({(int)resp.StatusCode}): {body}");
        }
    }

    /// <summary>الفروع التي ظهرت في سجلّ التحقّق.</summary>
    public async Task<List<ValidationBranchDto>> BranchesAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ValidationBranchDto>>("api/validation/branches", JsonOpts, ct)
           ?? new List<ValidationBranchDto>();

    /// <summary>سجلّ تحقّقات فرع واحد.</summary>
    public async Task<List<ValidationHistoryDto>> HistoryAsync(Guid merchantId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ValidationHistoryDto>>(
               $"api/validation/history?merchantId={merchantId}", JsonOpts, ct)
           ?? new List<ValidationHistoryDto>();
}
