using System.Globalization;

namespace Sallimni.DriverApp.Services;

public class BoolToFlowDirectionConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => (v is bool b && b) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class PriceConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is decimal d ? d.ToString("0.00") : "—";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is bool b && !b;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => v is bool b && !b;
}

public class NotNullConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is not null && !(v is string s && s.Length == 0);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>حالة المهمة (int) → نص "status.N".</summary>
public class TaskStatusConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is int s ? LocalizationManager.Instance[$"status.{s}"] : "";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>true إذا (int) == الوسيط.</summary>
public class IntEqualsConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is int i && p is string s && int.TryParse(s, out var x) && i == x;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>true إذا (int) != الوسيط.</summary>
public class NotIntEqualsConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => !(v is int i && p is string s && int.TryParse(s, out var x) && i == x);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
