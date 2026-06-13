using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.MerchantApp.Models;
using Sallimni.MerchantApp.Services;

namespace Sallimni.MerchantApp.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppState _state;
    private readonly AppConfig _config;

    public SettingsViewModel(ApiClient api, AppState state, AppConfig config)
    {
        _api = api;
        _state = state;
        _config = config;
    }

    public ObservableCollection<MerchantInfoDto> Merchants { get; } = new();
    public string ServerUrl => _config.BaseUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTaxRegistered))]
    private MerchantInfoDto? _selectedMerchant;

    public bool IsTaxRegistered => _state.CurrentMerchant?.IsSalesTaxRegistered ?? false;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await _state.EnsureMerchantAsync(_api);
        Merchants.Clear();
        foreach (var m in _state.Merchants) Merchants.Add(m);
        SelectedMerchant = _state.CurrentMerchant;
    }

    partial void OnSelectedMerchantChanged(MerchantInfoDto? value)
    {
        if (value is not null) _state.CurrentMerchant = value;
    }

    public bool IsArabic => LocalizationManager.Instance.Language == "ar";

    [RelayCommand] private void SetArabic() => LocalizationManager.Instance.Language = "ar";
    [RelayCommand] private void SetEnglish() => LocalizationManager.Instance.Language = "en";

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;
        var loc = LocalizationManager.Instance;
        var ok = await page.DisplayAlertAsync(loc["settings.delete"], loc["settings.delete_confirm"], loc["common.yes"], loc["common.no"]);
        if (!ok || _state.MerchantId is null) return;
        try
        {
            await _api.DeleteAccountAsync(_state.MerchantId.Value);
            await page.DisplayAlertAsync(loc["settings.delete"], loc["settings.deleted"], loc["common.ok"]);
            _state.CurrentMerchant = null;
        }
        catch (Exception ex) { await page.DisplayAlertAsync(loc["common.error"], ex.Message, loc["common.ok"]); }
    }
}
