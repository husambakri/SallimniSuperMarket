using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sallimni.MerchantApp.Models;
using Sallimni.MerchantApp.Services;

namespace Sallimni.MerchantApp.ViewModels;

/// <summary>إدارة مخزون التاجر وأسعاره (ربط/تعديل MerchantProduct) — قسم 2/3.</summary>
public partial class InventoryViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AppState _state;

    public InventoryViewModel(ApiClient api, AppState state)
    {
        _api = api;
        _state = state;
    }

    public ObservableCollection<InventoryRow> Rows { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try
        {
            await _state.EnsureMerchantAsync(_api);
            if (_state.MerchantId is null) { ErrorMessage = "لا يوجد تاجر."; return; }
            var rows = await _api.GetCatalogAsync(_state.MerchantId.Value);
            Rows.Clear();
            foreach (var r in rows) Rows.Add(new InventoryRow(r));
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync(InventoryRow row)
    {
        if (_state.MerchantId is null || row is null) return;
        row.IsSaving = true; row.Saved = false; ErrorMessage = null;
        try
        {
            await _api.UpsertProductAsync(_state.MerchantId.Value, row.ProductId, new UpsertMerchantProductRequest
            {
                Price = row.Price,
                StockQty = row.StockQty,
                IsAvailable = row.IsAvailable
            });
            row.IsLinked = true;
            row.Saved = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { row.IsSaving = false; }
    }
}

/// <summary>سطر قابل للتعديل لصنف في كتالوج التاجر.</summary>
public partial class InventoryRow : ObservableObject
{
    public Guid ProductId { get; }
    public string NameAr { get; }
    public string? UnitSize { get; }
    public string? Barcode { get; }

    public InventoryRow(MerchantCatalogRowDto dto)
    {
        ProductId = dto.ProductId;
        NameAr = dto.NameAr;
        UnitSize = dto.UnitSize;
        Barcode = dto.Barcode;
        _price = dto.Price ?? 0m;
        _stockQty = dto.StockQty;
        _isAvailable = dto.IsAvailable;
        _isLinked = dto.IsLinked;
    }

    [ObservableProperty] private decimal _price;
    [ObservableProperty] private int _stockQty;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private bool _isLinked;
    [ObservableProperty] private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SavedVisible))]
    private bool _saved;

    public bool SavedVisible => Saved;
}
