using System.Globalization;

namespace Sallimni.DriverApp.Services;

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = "";
    public BindingBase ProvideValue(IServiceProvider sp)
        => new Binding { Mode = BindingMode.OneWay, Path = nameof(LocalizationManager.Language),
            Source = LocalizationManager.Instance, Converter = LocalizeConverter.Instance, ConverterParameter = Key };
    object IMarkupExtension.ProvideValue(IServiceProvider sp) => ProvideValue(sp);
}

public class LocalizeConverter : IValueConverter
{
    public static readonly LocalizeConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) => p is string k ? LocalizationManager.Instance[k] : "";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
