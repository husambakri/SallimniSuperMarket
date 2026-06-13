using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sallimni.CustomerApp.Models;

namespace Sallimni.CustomerApp.Services;

/// <summary>سطر في السلّة: صنف + كمّية + السعر المعروض (أرخص تاجر شامل الضريبة).</summary>
public partial class CartItem : ObservableObject
{
    public ProductDto Product { get; }
    public CartItem(ProductDto product, int qty = 1) { Product = product; _quantity = qty; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    private int _quantity;

    public decimal UnitPrice => Product.CheapestPriceInclTax ?? 0m;
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>السلّة الموحّدة للزبون (محلّية حتى التأكيد). تُقسَّم على الخادم عند الطلب.</summary>
public partial class CartService : ObservableObject
{
    public ObservableCollection<CartItem> Items { get; } = new();

    public CartService() => Items.CollectionChanged += (_, _) => RaiseTotals();

    public void Add(ProductDto product, int qty = 1)
    {
        var existing = Items.FirstOrDefault(i => i.Product.Id == product.Id);
        if (existing is not null)
            existing.Quantity += qty;
        else
        {
            var item = new CartItem(product, qty);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CartItem.Quantity)) RaiseTotals(); };
            Items.Add(item);
        }
        RaiseTotals();
    }

    public void Remove(CartItem item)
    {
        Items.Remove(item);
        RaiseTotals();
    }

    public void Increment(CartItem item) => item.Quantity++;

    public void Decrement(CartItem item)
    {
        if (item.Quantity > 1) item.Quantity--;
        else Remove(item);
    }

    public void Clear()
    {
        Items.Clear();
        RaiseTotals();
    }

    public int Count => Items.Sum(i => i.Quantity);
    public decimal Total => Items.Sum(i => i.LineTotal);
    public bool IsEmpty => Items.Count == 0;

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
