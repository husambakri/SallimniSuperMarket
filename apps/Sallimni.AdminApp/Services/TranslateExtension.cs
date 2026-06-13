using System.Globalization;

namespace Sallimni.AdminApp.Services;

/// <summary>{svc:Translate Key=...} — يربط النص بمفتاح ترجمة عبر محوِّل (يتجنّب كسر مسار indexer بالنقاط).</summary>
[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = "";

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
        => new Binding
        {
            Mode = BindingMode.OneWay,
            Path = nameof(LocalizationManager.Language),
            Source = LocalizationManager.Instance,
            Converter = LocalizeConverter.Instance,
            ConverterParameter = Key
        };

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

public class LocalizeConverter : IValueConverter
{
    public static readonly LocalizeConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => parameter is string key ? LocalizationManager.Instance[key] : "";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
