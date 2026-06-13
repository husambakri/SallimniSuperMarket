using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.AdminApp.Models;
using Sallimni.AdminApp.Services;

namespace Sallimni.AdminApp.ViewModels;

/// <summary>اعتماد/رفض طلبات إضافة الأصناف من التجار (قسم 3). الاعتماد يُنشئ بطاقة Product رئيسية.</summary>
public partial class ApprovalsViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    public ApprovalsViewModel(ApiClient api) => _api = api;

    public ObservableCollection<AdminSubmissionDto> Submissions { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();

    [ObservableProperty] private CategoryDto? _selectedCategory;
    public bool IsEmpty => Submissions.Count == 0 && !IsBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            var cats = await _api.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);
            SelectedCategory ??= Categories.FirstOrDefault();

            var subs = await _api.GetSubmissionsAsync();
            Submissions.Clear();
            foreach (var s in subs) Submissions.Add(s);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand]
    private async Task ApproveAsync(AdminSubmissionDto sub)
    {
        if (sub is null) return;
        var cat = SelectedCategory ?? Categories.FirstOrDefault();
        if (cat is null) { ErrorMessage = "لا يوجد تصنيف."; return; }
        ErrorMessage = null;
        try
        {
            await _api.ApproveSubmissionAsync(sub.Id, cat.Id);
            StatusMessage = LocalizationManager.Instance["common.done"];
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RejectAsync(AdminSubmissionDto sub)
    {
        if (sub is null) return;
        ErrorMessage = null;
        try
        {
            await _api.RejectSubmissionAsync(sub.Id, null);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
