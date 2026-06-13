using CommunityToolkit.Mvvm.ComponentModel;
using Sallimni.MerchantApp.Models;

namespace Sallimni.MerchantApp.Services;

/// <summary>حالة مشتركة: المتجر الحالي (يُختار من قائمة التجار في الإصدار الأول).</summary>
public partial class AppState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MerchantId))]
    private MerchantInfoDto? _currentMerchant;

    public List<MerchantInfoDto> Merchants { get; private set; } = new();

    public Guid? MerchantId => CurrentMerchant?.Id;

    /// <summary>تحميل التجار واختيار الأول مرّة واحدة (بدل تسجيل دخول كامل).</summary>
    public async Task EnsureMerchantAsync(ApiClient api, CancellationToken ct = default)
    {
        if (CurrentMerchant is not null) return;
        Merchants = await api.GetMerchantsAsync(ct);
        CurrentMerchant = Merchants.FirstOrDefault();
    }
}
