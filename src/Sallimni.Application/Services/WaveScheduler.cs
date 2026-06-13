namespace Sallimni.Application.Services;

/// <summary>جدولة حدود الموجات على فواصل منتظمة (كل ساعة افتراضياً، محاذاة لبداية اليوم).</summary>
public static class WaveScheduler
{
    /// <summary>أقرب حدّ موجة عند/بعد <paramref name="now"/> وفق فاصل بالدقائق.</summary>
    public static DateTimeOffset NextBoundaryAtOrAfter(DateTimeOffset now, int intervalMinutes)
    {
        if (intervalMinutes <= 0) intervalMinutes = 60;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var minutesSinceDayStart = (now - dayStart).TotalMinutes;
        var slots = Math.Ceiling(minutesSinceDayStart / intervalMinutes);
        return dayStart.AddMinutes(slots * intervalMinutes);
    }
}
