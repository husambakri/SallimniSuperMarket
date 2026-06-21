// ===================================================
// Helper extensions لقراءة JSON
// ===================================================
using System.Text.Json;
namespace JordanGrocery;

internal static class JsonExtensions
{
    public static string? GetString(this JsonElement el, string key)
        => el.TryGetProperty(key, out var v) ? v.GetString() : null;

    public static decimal GetDecimal(this JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.TryGetDecimal(out var d) ? d : 0;

    public static bool GetBool(this JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    public static JsonElement? GetPropertyOrNull(this JsonElement el, string key)
        => el.TryGetProperty(key, out var v) ? v : null;

    public static decimal? GetDecimal(this JsonElement? el, string key)
        => el?.TryGetProperty(key, out var v) == true && v.TryGetDecimal(out var d) ? d : null;

    public static int GetArrayLength(this JsonElement? el)
        => el?.ValueKind == JsonValueKind.Array ? el.Value.GetArrayLength() : 0;
}
