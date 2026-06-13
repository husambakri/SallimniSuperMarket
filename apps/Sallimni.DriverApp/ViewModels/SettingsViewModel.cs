using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.DriverApp.Models;
using Sallimni.DriverApp.Services;

namespace Sallimni.DriverApp.ViewModels;

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

    public ObservableCollection<DriverInfoDto> Drivers { get; } = new();
    public string ServerUrl => _config.BaseUrl;

    [ObservableProperty] private DriverInfoDto? _selectedDriver;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await _state.EnsureDriverAsync(_api);
        Drivers.Clear();
        foreach (var d in _state.Drivers) Drivers.Add(d);
        SelectedDriver = _state.CurrentDriver;
    }

    partial void OnSelectedDriverChanged(DriverInfoDto? value)
    {
        if (value is not null) _state.CurrentDriver = value;
    }

    [RelayCommand] private void SetArabic() => LocalizationManager.Instance.Language = "ar";
    [RelayCommand] private void SetEnglish() => LocalizationManager.Instance.Language = "en";

    public string PrivacyUrl => _config.BaseUrl.TrimEnd('/') + "/privacy.html";
    public string TermsUrl => _config.BaseUrl.TrimEnd('/') + "/terms.html";

    [RelayCommand] private async Task OpenPrivacyAsync() { try { await Launcher.OpenAsync(PrivacyUrl); } catch { } }
    [RelayCommand] private async Task OpenTermsAsync() { try { await Launcher.OpenAsync(TermsUrl); } catch { } }

    /// <summary>حذف الحساب داخل التطبيق (قسم 15).</summary>
    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;
        var loc = LocalizationManager.Instance;
        var ok = await page.DisplayAlertAsync(loc["settings.delete"], loc["settings.delete_confirm"], "نعم", "لا");
        if (!ok || _state.DriverId is null) return;
        try
        {
            await _api.DeleteAccountAsync(_state.DriverId.Value);
            await page.DisplayAlertAsync(loc["settings.delete"], loc["settings.deleted"], loc["common.ok"]);
            _state.CurrentDriver = null;
        }
        catch (Exception ex) { await page.DisplayAlertAsync(loc["common.error"], ex.Message, loc["common.ok"]); }
    }
}
