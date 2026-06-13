using System.Globalization;

namespace Sallimni.CustomerApp.Services;

/// <summary>
/// امتداد XAML: {svc:Translate Key=catalog.title} يربط النص بمفتاح ترجمة.
/// نتجنّب مسار الـ indexer (يكسره النقطة في المفتاح) ونربط بخاصية Language
/// عبر محوِّل يأخذ المفتاح كـ ConverterParameter — فيتحدّث النص حيّاً عند تبديل اللغة.
/// </summary>
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

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}

/// <summary>يحوّل (اللغة الحالية + مفتاح) إلى النص المترجَم.</summary>
public class LocalizeConverter : IValueConverter
{
    public static readonly LocalizeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => parameter is string key ? LocalizationManager.Instance[key] : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
