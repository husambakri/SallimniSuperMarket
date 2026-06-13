using Sallimni.AdminApp.Models;

namespace Sallimni.AdminApp.Services;

/// <summary>يمرّر الصنف المختار من شاشة الأصناف إلى صفحة التعديل.</summary>
public class CatalogState
{
    public AdminProductDto? SelectedProduct { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
}
