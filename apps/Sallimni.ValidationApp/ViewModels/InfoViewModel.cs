using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.ValidationApp.Models;
using Sallimni.ValidationApp.Services;

namespace Sallimni.ValidationApp.ViewModels;

/// <summary>
/// تبويب "القاعدة": لقطة عن حالة قاعدة البيانات (عدد الأصناف/المتاجر/الأسعار، آخر تحديث سعر
/// وكم تغيّر فيه آخر 24 ساعة، وملخّص عمليات التحقّق) — ليطمئنّ العامل لحداثة ما يفحص ضدّه.
/// </summary>
public partial class InfoViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public InfoViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private ValidationStatsDto? _stats;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public bool HasStats => Stats is not null;
    partial void OnStatsChanged(ValidationStatsDto? value) => OnPropertyChanged(nameof(HasStats));

    /// <summary>يحمّل لقطة الحالة (يُستدعى عند ظهور الصفحة وعند الضغط على تحديث).</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var s = await _api.StatsAsync();
            if (s is null) { ErrorMessage = "تعذّر جلب حالة القاعدة."; return; }
            Stats = s;
        }
        catch
        {
            ErrorMessage = "تعذّر جلب حالة القاعدة. تحقّق من الاتصال.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
