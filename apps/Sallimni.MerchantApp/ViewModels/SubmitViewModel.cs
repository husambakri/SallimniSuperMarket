using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.MerchantApp.Models;
using Sallimni.MerchantApp.Services;

namespace Sallimni.MerchantApp.ViewModels;

/// <summary>طلب إضافة صنف جديد لطابور اعتماد الإدارة (قسم 3).</summary>
public partial class SubmitViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppState _state;

    public SubmitViewModel(ApiClient api, AppState state)
    {
        _api = api;
        _state = state;
    }

    [ObservableProperty] private string _nameAr = "";
    [ObservableProperty] private string _nameEn = "";
    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _unitSize = "";

    /// <summary>شرائح الضريبة المتاحة (تطابق enum الخادم).</summary>
    public List<int> TaxClasses { get; } = new() { 0, 2, 4, 5, 10, 16 };
    [ObservableProperty] private int _selectedTaxClass = 16;

    public ObservableCollection<SubmissionDto> MySubmissions { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            await _state.EnsureMerchantAsync(_api);
            if (_state.MerchantId is null) return;
            var subs = await _api.GetSubmissionsAsync(_state.MerchantId.Value);
            MySubmissions.Clear();
            foreach (var s in subs) MySubmissions.Add(s);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        StatusMessage = null; ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(NameAr)) { ErrorMessage = "الاسم بالعربية مطلوب."; return; }
        if (_state.MerchantId is null) { ErrorMessage = "لا يوجد تاجر."; return; }

        IsBusy = true;
        try
        {
            await _api.CreateSubmissionAsync(_state.MerchantId.Value, new CreateSubmissionRequest
            {
                NameAr = NameAr.Trim(),
                NameEn = NameEn.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                UnitSize = string.IsNullOrWhiteSpace(UnitSize) ? null : UnitSize.Trim(),
                SuggestedTaxClass = SelectedTaxClass
            });
            StatusMessage = LocalizationManager.Instance["submit.sent"];
            NameAr = NameEn = Barcode = UnitSize = "";
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
