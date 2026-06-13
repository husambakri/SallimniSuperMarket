namespace Sallimni.Application.Services;

/// <summary>إحداثيات جغرافية.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>أدوات جغرافية: مسافة Haversine وزمن قيادة تقديري.</summary>
public static class GeoUtils
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>المسافة بالكيلومترات بين نقطتين (Haversine).</summary>
    public static double DistanceKm(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);
        var lat1 = ToRad(a.Latitude);
        var lat2 = ToRad(b.Latitude);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
        return EarthRadiusKm * 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
    }

    /// <summary>زمن القيادة بالدقائق لمسافة بسرعة متوسطة (كم/ساعة).</summary>
    public static double DriveMinutes(double distanceKm, double avgSpeedKmh)
        => avgSpeedKmh <= 0 ? 0 : distanceKm / avgSpeedKmh * 60.0;

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
