using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.CustomerApp.Models;
using Sallimni.CustomerApp.Services;

namespace Sallimni.CustomerApp.ViewModels;

public partial class OrderViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppState _state;

    public OrderViewModel(ApiClient api, AppState state)
    {
        _api = api;
        _state = state;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(EtaText))]
    [NotifyPropertyChangedFor(nameof(HasUnfulfilled))]
    private OrderDto? _order;

    public void Load() => Order = _state.LastOrder;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (Order is null || IsBusy) return;
        IsBusy = true;
        try
        {
            var fresh = await _api.GetOrderAsync(Order.Id);
            if (fresh is not null) Order = fresh;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    public bool HasUnfulfilled => Order?.Unfulfilled is { Count: > 0 };

    public string EtaText => Order?.EstimatedDeliveryAt is { } eta
        ? eta.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "—";

    public string StatusText => Order is null ? "" : StatusLabel(Order.Status);

    private static string StatusLabel(int status) => status switch
    {
        0 => "في السلّة",
        1 => "مؤكَّد",
        2 => "مُقسَّم على التجار",
        3 => "مهمة تجميع",
        4 => "تم الاستلام من المتاجر",
        5 => "في المستودع / مفروز",
        6 => "مهمة توزيع",
        7 => "خرج للتوصيل",
        8 => "مُسلَّم ومحصَّل",
        9 => "مُسوّى",
        100 => "ملغى",
        _ => $"حالة {status}"
    };
}
