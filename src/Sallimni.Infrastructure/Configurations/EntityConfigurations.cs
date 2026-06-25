using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sallimni.Domain.Entities;

namespace Sallimni.Infrastructure.Configurations;

public class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasOne(c => c.Parent)
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        // الباركود مفتاح فحص السعر (قسم 4.1) — مفهرس للبحث السريع.
        b.HasIndex(p => p.Barcode);
        b.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TalabatPriceEntryConfig : IEntityTypeConfiguration<TalabatPriceEntry>
{
    public void Configure(EntityTypeBuilder<TalabatPriceEntry> b)
    {
        // البحث بالباركود مفهرس؛ والربط (فرع + باركود) فريد للتحديث الدوري (upsert).
        b.HasIndex(e => e.Barcode);
        b.HasIndex(e => new { e.BranchId, e.Barcode }).IsUnique();
    }
}

public class PriceValidationConfig : IEntityTypeConfiguration<PriceValidation>
{
    public void Configure(EntityTypeBuilder<PriceValidation> b)
    {
        // سجلّ تاريخي: استعلام السجلّ لكل فرع مرتّباً بالأحدث؛ والبحث بالباركود.
        b.HasIndex(v => new { v.MerchantId, v.CreatedAt });
        b.HasIndex(v => v.Barcode);
        // بلا مفتاح أجنبي على MerchantId عمداً — التاريخ يصمد لو حُذف التاجر (لقطات محفوظة).
    }
}

public class MerchantProductConfig : IEntityTypeConfiguration<MerchantProduct>
{
    public void Configure(EntityTypeBuilder<MerchantProduct> b)
    {
        // تاجر واحد ⟷ بطاقة واحدة: ربط فريد.
        b.HasIndex(mp => new { mp.MerchantId, mp.ProductId }).IsUnique();
        b.HasOne(mp => mp.Merchant)
            .WithMany(m => m.MerchantProducts)
            .HasForeignKey(mp => mp.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(mp => mp.Product)
            .WithMany(p => p.MerchantProducts)
            .HasForeignKey(mp => mp.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        // فهرس مركّب يخدم استعلام الأرخص المتوفر لكل صنف.
        b.HasIndex(mp => new { mp.ProductId, mp.IsAvailable, mp.Price });
    }
}

public class OrderConfig : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.Address)
            .WithMany()
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasOne(o => o.Wave)
            .WithMany(w => w.Orders)
            .HasForeignKey(o => o.WaveId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(o => o.Status);
    }
}

public class SubOrderConfig : IEntityTypeConfiguration<SubOrder>
{
    public void Configure(EntityTypeBuilder<SubOrder> b)
    {
        b.HasOne(s => s.Order)
            .WithMany(o => o.SubOrders)
            .HasForeignKey(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Merchant)
            .WithMany(m => m.SubOrders)
            .HasForeignKey(s => s.MerchantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfig : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Ignore(i => i.LineTotalInclTax); // خاصية محسوبة
        b.HasOne(i => i.SubOrder)
            .WithMany(s => s.Items)
            .HasForeignKey(i => i.SubOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RouteStopConfig : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> b)
    {
        b.HasOne(r => r.Task)
            .WithMany(t => t.Stops)
            .HasForeignKey(r => r.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.Merchant).WithMany().HasForeignKey(r => r.MerchantId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(r => r.SubOrder).WithMany().HasForeignKey(r => r.SubOrderId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ScanEventConfig : IEntityTypeConfiguration<ScanEvent>
{
    public void Configure(EntityTypeBuilder<ScanEvent> b)
    {
        b.HasOne(s => s.SubOrder).WithMany().HasForeignKey(s => s.SubOrderId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(s => s.Order).WithMany().HasForeignKey(s => s.OrderId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(s => s.Driver).WithMany().HasForeignKey(s => s.DriverId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(s => s.QrCode);
    }
}

public class SettlementConfig : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> b)
    {
        b.HasOne(s => s.SubOrder).WithMany().HasForeignKey(s => s.SubOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Merchant).WithMany().HasForeignKey(s => s.MerchantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Driver).WithMany().HasForeignKey(s => s.DriverId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DeliveryTaskConfig : IEntityTypeConfiguration<DeliveryTask>
{
    public void Configure(EntityTypeBuilder<DeliveryTask> b)
    {
        b.HasOne(t => t.Wave)
            .WithMany(w => w.Tasks)
            .HasForeignKey(t => t.WaveId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(t => t.Driver)
            .WithMany()
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
