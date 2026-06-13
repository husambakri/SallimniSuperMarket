using System.Net.Http.Json;
using System.Text.Json;
using Sallimni.CustomerApp.Models;

namespace Sallimni.CustomerApp.Services;

/// <summary>عميل HTTP للتخاطب مع خادم سلّمني (Sallimni.Api).</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http) => _http = http;

    public async Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<CategoryDto>>("api/catalog/categories", JsonOpts, ct) ?? new();

    public async Task<List<ProductDto>> GetProductsAsync(Guid? categoryId = null, string? q = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (categoryId.HasValue) qs.Add($"categoryId={categoryId}");
        if (!string.IsNullOrWhiteSpace(q)) qs.Add($"q={Uri.EscapeDataString(q)}");
        var url = "api/catalog/products" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<ProductDto>>(url, JsonOpts, ct) ?? new();
    }

    public async Task<List<ProductDto>> GetOffersAsync(int take = 10, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ProductDto>>($"api/catalog/offers?take={take}", JsonOpts, ct) ?? new();

    public async Task<ProductDetailDto?> GetProductAsync(Guid id, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<ProductDetailDto>($"api/catalog/products/{id}", JsonOpts, ct);

    public async Task<List<CustomerDto>> GetCustomersAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<CustomerDto>>("api/catalog/customers", JsonOpts, ct) ?? new();

    public async Task<BarcodeLookupDto> LookupBarcodeAsync(string code, Guid? customerId, CancellationToken ct = default)
    {
        var url = $"api/catalog/barcode/{Uri.EscapeDataString(code)}";
        if (customerId.HasValue) url += $"?customerId={customerId}";
        return await _http.GetFromJsonAsync<BarcodeLookupDto>(url, JsonOpts, ct)
               ?? new BarcodeLookupDto { Found = false };
    }

    public async Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/orders", req, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new ApiException($"فشل تأكيد الطلب ({(int)resp.StatusCode}): {body}");
        }
        return (await resp.Content.ReadFromJsonAsync<OrderDto>(JsonOpts, ct))!;
    }

    public async Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<OrderDto>($"api/orders/{id}", JsonOpts, ct);

    public async Task DeleteAccountAsync(Guid customerId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/account/customer/{customerId}", ct);
        if (!resp.IsSuccessStatusCode)
            throw new ApiException($"فشل حذف الحساب ({(int)resp.StatusCode}).");
    }
}

public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}
