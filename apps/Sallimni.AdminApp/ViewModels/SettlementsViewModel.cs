using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.AdminApp.Models;
using Sallimni.AdminApp.Services;

namespace Sallimni.AdminApp.ViewModels;

/// <summary>تسويات التجار: عرض المستحق بعد العمولة ووسم الطلب الفرعي مُسوّى (قسم 7).</summary>
public partial class SettlementsViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    public SettlementsViewModel(ApiClient api) => _api = api;

    public ObservableCollection<SettlementRowDto> Rows { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            var rows = await _api.GetSettlementsAsync();
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SettleAsync(SettlementRowDto row)
    {
        if (row is null) return;
        ErrorMessage = null;
        try { await _api.SettleAsync(row.SubOrderId); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
