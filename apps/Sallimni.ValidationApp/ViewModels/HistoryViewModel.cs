using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.ValidationApp.Models;
using Sallimni.ValidationApp.Services;

namespace Sallimni.ValidationApp.ViewModels;

/// <summary>
/// صفحة السجلّ: نختار الفرع الذي اشتغلنا فيه فيُعرض سجلّ التحقّقات (الأحدث أولاً) —
/// السعر المخزّن مقابل الواقع وحالة التطابق لكل عملية.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public HistoryViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private ValidationBranchDto? _selectedBranch;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public bool IsEmpty => !IsBusy && SelectedBranch is not null && Records.Count == 0;

    public ObservableCollection<ValidationBranchDto> Branches { get; } = new();
    public ObservableCollection<ValidationHistoryDto> Records { get; } = new();

    partial void OnSelectedBranchChanged(ValidationBranchDto? value)
    {
        if (value is not null)
            _ = LoadHistoryAsync(value.MerchantId);
        else
        {
            Records.Clear();
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>يحمّل قائمة الفروع (يُستدعى عند ظهور الصفحة).</summary>
    [RelayCommand]
    private async Task LoadBranchesAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var branches = await _api.BranchesAsync();
            var previous = SelectedBranch?.MerchantId;
            Branches.Clear();
            foreach (var b in branches) Branches.Add(b);
            // أعِد اختيار الفرع نفسه إن بقي موجوداً (لتحديث السجلّ بعد تسجيل جديد).
            if (previous is not null)
                SelectedBranch = Branches.FirstOrDefault(b => b.MerchantId == previous);
            if (Branches.Count == 0) ErrorMessage = "لا سجلّ تحقّق بعد — ابدأ بالمسح.";
        }
        catch
        {
            ErrorMessage = "تعذّر جلب الفروع. تحقّق من الاتصال.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task LoadHistoryAsync(Guid merchantId)
    {
        IsBusy = true;
        ErrorMessage = null;
        Records.Clear();
        OnPropertyChanged(nameof(IsEmpty));
        try
        {
            var rows = await _api.HistoryAsync(merchantId);
            foreach (var r in rows) Records.Add(r);
        }
        catch
        {
            ErrorMessage = "تعذّر جلب السجلّ. تحقّق من الاتصال.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
