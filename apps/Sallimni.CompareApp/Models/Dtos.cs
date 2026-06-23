namespace Sallimni.CompareApp.Models;

// نماذج مطابقة لاستجابة /api/scan-compare في الخادم.

public class ScanCompareResponse
{
    public string Barcode { get; set; } = "";
    public bool TimedOut { get; set; }
    public int StoresQueried { get; set; }
    public int Count { get; set; }
    public List<LiveScanDto> Results { get; set; } = new();
}

public class LiveScanDto
{
    public string Store { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Special { get; set; }
    public decimal EffectivePrice { get; set; }
    public bool InStock { get; set; }
    public string StockStatus { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string ProductUrl { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // مشتقّات العرض (تُضبط في ViewModel) — بلا حاجة لمحوّلات في XAML.
    public bool IsCheapest { get; set; }
    public string? Note { get; set; }
    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
    public bool HasNoImage => !HasImage;
    public bool HasNote => !string.IsNullOrEmpty(Note);
    public string AvailabilityText => InStock ? "متوفّر" : "غير متوفّر";
    public double RowOpacity => InStock ? 1.0 : 0.55;
    public string PriceText => EffectivePrice.ToString("0.00");
    public string Glyph => "🏪";
}
