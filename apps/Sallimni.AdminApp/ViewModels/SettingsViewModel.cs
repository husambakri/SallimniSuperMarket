using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.AdminApp.Models;
using Sallimni.AdminApp.Services;

namespace Sallimni.AdminApp.ViewModels;

/// <summary>ضبط العمولة وإعدادات الموجات + اللغة (قسم 13).</summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppConfig _config;

    public SettingsViewModel(ApiClient api, AppConfig config)
    {
        _api = api;
        _config = config;
    }

    public string ServerUrl => _config.BaseUrl;

    [ObservableProperty] private decimal _commissionRate;
    [ObservableProperty] private int _waveInterval;
    [ObservableProperty] private int _distributionGap;
    [ObservableProperty] private int _prepMinutes;
    [ObservableProperty] private int _transitMinutes;
    [ObservableProperty] private int _maxCustomers;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            CommissionRate = (await _api.GetCommissionAsync()).DefaultRate;
            var w = await _api.GetWaveConfigAsync();
            WaveInterval = w.WaveIntervalMinutes;
            DistributionGap = w.DistributionGapMinutes;
            PrepMinutes = w.DefaultPrepMinutes;
            TransitMinutes = w.DefaultTransitMinutes;
            MaxCustomers = w.MaxCustomersPerDriver;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null; StatusMessage = null;
        try
        {
            await _api.UpdateCommissionAsync(CommissionRate);
            await _api.UpdateWaveConfigAsync(new WaveConfigDto
            {
                WaveIntervalMinutes = WaveInterval,
                DistributionGapMinutes = DistributionGap,
                DefaultPrepMinutes = PrepMinutes,
                DefaultTransitMinutes = TransitMinutes,
                MaxCustomersPerDriver = MaxCustomers
            });
            StatusMessage = LocalizationManager.Instance["cfg.saved"];
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    public bool IsArabic => LocalizationManager.Instance.Language == "ar";
    [RelayCommand] private void SetArabic() => LocalizationManager.Instance.Language = "ar";
    [RelayCommand] private void SetEnglish() => LocalizationManager.Instance.Language = "en";
}
