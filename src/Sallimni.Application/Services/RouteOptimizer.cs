namespace Sallimni.Application.Services;

/// <summary>محطة إدخال للتحسين: معرّف + إحداثيات.</summary>
public record RouteStopInput(Guid Id, GeoPoint Point);

/// <summary>محطة بعد التحسين: ترتيبها، مسافة المقطع، الزمن التراكمي، ووقت الوصول التقديري.</summary>
public record RoutedStop(Guid Id, int Sequence, double LegDistanceKm,
    double CumulativeDriveMinutes, DateTimeOffset? ArrivalAt);

public record RouteResult(List<RoutedStop> Stops, double TotalDistanceKm, double TotalMinutes);

/// <summary>
/// تحسين مسار محلّي بخوارزمية الجار الأقرب (Nearest-Neighbor TSP) — قسم 6.2 (بديل بلا API خارجي).
/// يبدأ من نقطة الانطلاق (المستودع) ويزور الأقرب فالأقرب، ويحسب وقت وصول تراكمي لكل محطة.
/// </summary>
public static class RouteOptimizer
{
    public static RouteResult Optimize(
        GeoPoint start,
        IReadOnlyList<RouteStopInput> stops,
        double avgSpeedKmh = 30.0,
        double serviceMinutesPerStop = 5.0,
        DateTimeOffset? departAt = null)
    {
        var remaining = stops.ToList();
        var ordered = new List<RoutedStop>();
        var current = start;
        var cumulativeDrive = 0.0;
        var totalDistance = 0.0;
        var clock = departAt;
        var seq = 1;

        while (remaining.Count > 0)
        {
            // اختيار الأقرب للنقطة الحالية.
            var bestIdx = 0;
            var bestDist = double.MaxValue;
            for (var i = 0; i < remaining.Count; i++)
            {
                var d = GeoUtils.DistanceKm(current, remaining[i].Point);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }

            var next = remaining[bestIdx];
            remaining.RemoveAt(bestIdx);

            var legMinutes = GeoUtils.DriveMinutes(bestDist, avgSpeedKmh);
            cumulativeDrive += legMinutes;
            totalDistance += bestDist;

            DateTimeOffset? arrival = null;
            if (clock is not null)
            {
                clock = clock.Value.AddMinutes(legMinutes);
                arrival = clock;
                clock = clock.Value.AddMinutes(serviceMinutesPerStop); // زمن التسليم قبل المقطع التالي
            }

            ordered.Add(new RoutedStop(next.Id, seq++, bestDist, cumulativeDrive, arrival));
            current = next.Point;
        }

        var totalMinutes = cumulativeDrive + serviceMinutesPerStop * stops.Count;
        return new RouteResult(ordered, totalDistance, totalMinutes);
    }
}
