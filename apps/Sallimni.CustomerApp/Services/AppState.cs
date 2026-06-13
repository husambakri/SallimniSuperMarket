using CommunityToolkit.Mvvm.ComponentModel;
using Sallimni.CustomerApp.Models;

namespace Sallimni.CustomerApp.Services;

/// <summary>
/// حالة التطبيق المشتركة: الزبون الحالي وعنوانه المختار وطريقة الدفع.
/// (في الإصدار الأول يُختار زبون تجريبي من الخادم بدل تسجيل دخول كامل.)
/// </summary>
public partial class AppState : ObservableObject
{
    [ObservableProperty] private CustomerDto? _currentCustomer;
    [ObservableProperty] private AddressDto? _selectedAddress;

    /// <summary>0 = كاش، 1 = CliQ (الدفع عند الاستلام).</summary>
    [ObservableProperty] private int _paymentMethod;

    /// <summary>آخر طلب مؤكَّد — يُعرض في صفحة الطلب/التتبّع.</summary>
    [ObservableProperty] private OrderDto? _lastOrder;

    public bool IsReady => CurrentCustomer is not null && SelectedAddress is not null;
}
