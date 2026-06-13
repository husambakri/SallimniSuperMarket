using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.MerchantApp.Models;
using Sallimni.MerchantApp.Services;

namespace Sallimni.MerchantApp.ViewModels;

/// <summary>استقبال الطلبات الفرعية الواردة وتجهيزها (قسم 2، 5.1). تحديث الحالة: بدء التحضير/جاهز.</summary>
public partial class OrdersViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppState _state;

    public OrdersViewModel(ApiClient api, AppState state)
    {
        _api = api;
        _state = state;
    }

    public ObservableCollection<MerchantSubOrderDto> Orders { get; } = new();
    public bool IsEmpty => Orders.Count == 0 && !IsBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            await _state.EnsureMerchantAsync(_api);
            if (_state.MerchantId is null) { ErrorMessage = "لا يوجد تاجر."; return; }
            var orders = await _api.GetSubOrdersAsync(_state.MerchantId.Value);
            Orders.Clear();
            foreach (var o in orders) Orders.Add(o);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    // 1 = Preparing، 2 = PickedUp (جاهز/تم الاستلام). مطابق لـ SubOrderStatus في الخادم.
    [RelayCommand]
    private Task StartPrep(MerchantSubOrderDto o) => SetStatus(o, 1);

    [RelayCommand]
    private Task MarkReady(MerchantSubOrderDto o) => SetStatus(o, 2);

    private async Task SetStatus(MerchantSubOrderDto o, int status)
    {
        if (o is null || IsBusy) return;
        ErrorMessage = null;
        try
        {
            await _api.UpdateSubOrderStatusAsync(o.SubOrderId, status);
            await LoadAsync(); // يعيد التحميل ويضبط IsBusy بنفسه
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
