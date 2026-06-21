using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly AppState _state;
    private readonly AppConfig _config;
    private readonly ApiClient _api;

    public SettingsViewModel(AppState state, AppConfig config, ApiClient api)
    {
        _state = state;
        _config = config;
        _api = api;
    }

    public string ServerUrl => _config.BaseUrl;
    public string CustomerName => _state.CurrentCustomer?.Name ?? "—";
    public bool IsArabic => LocalizationManager.Instance.Language == "ar";

    /// <summary>روابط الصفحات القانونية على الويب (إلزامية للمتجرين).</summary>
    public string PrivacyUrl => _config.BaseUrl.TrimEnd('/') + "/privacy.html";
    public string TermsUrl => _config.BaseUrl.TrimEnd('/') + "/terms.html";

    [RelayCommand]
    private void SetArabic() => LocalizationManager.Instance.Language = "ar";

    [RelayCommand]
    private void SetEnglish() => LocalizationManager.Instance.Language = "en";

    [RelayCommand]
    private async Task OpenPrivacyAsync()
    {
        try { await Launcher.OpenAsync(PrivacyUrl); } catch { /* لا يفشل */ }
    }

    [RelayCommand]
    private async Task OpenTermsAsync()
    {
        try { await Launcher.OpenAsync(TermsUrl); } catch { /* لا يفشل */ }
    }

    /// <summary>حذف الحساب داخل التطبيق — متطلب متجرَي التطبيقات (قسم 15: Apple 5.1.1 / Google).</summary>
    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;
        var loc = LocalizationManager.Instance;
        var ok = await page.DisplayAlert(loc["settings.delete"], loc["settings.delete_confirm"],
            loc["common.yes"], loc["common.no"]);
        if (!ok) return;

        if (_state.CurrentCustomer is null) return;
        try
        {
            await _api.DeleteAccountAsync(_state.CurrentCustomer.Id);
            await page.DisplayAlert(loc["settings.delete"], loc["settings.deleted"], loc["common.ok"]);
            _state.CurrentCustomer = null;
            _state.SelectedAddress = null;
        }
        catch (Exception ex)
        {
            await page.DisplayAlert(loc["common.error"], ex.Message, loc["common.ok"]);
        }
    }
}
