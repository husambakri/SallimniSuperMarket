using Microsoft.EntityFrameworkCore;
using Sallimni.Domain.Entities;
using Sallimni.Domain.Enums;
using Sallimni.Infrastructure;

namespace Sallimni.Api.Seeding;

/// <summary>بيانات أولية غنيّة بأسلوب أسواق التوصيل: فئات بأيقونات، منتجات بصور تعبيرية ووصف،
/// وثلاثة تجار بأسعار متفاوتة لإبراز "التوفير" (أرخص سعر مقابل أعلى سعر).</summary>
public static class DataSeeder
{
    public static async Task SeedAsync(SallimniDbContext db)
    {
        if (await db.Merchants.AnyAsync()) return;

        db.CommissionConfigs.Add(new CommissionConfig { DefaultRate = 0.10m, MerchantId = null });
        db.WaveConfigs.Add(new WaveConfig());

        // ثلاثة تجار ضمن دائرة قريبة.
        var mA = new Merchant { Name = "سوبر ماركت أ", IsSalesTaxRegistered = true, Latitude = 31.95, Longitude = 35.91 };
        var mB = new Merchant { Name = "سوبر ماركت ب", IsSalesTaxRegistered = true, Latitude = 31.96, Longitude = 35.92 };
        var mC = new Merchant { Name = "سوبر ماركت ج", IsSalesTaxRegistered = false, Latitude = 31.94, Longitude = 35.90 };
        db.Merchants.AddRange(mA, mB, mC);
        var merchants = new[] { mA, mB, mC };

        db.Drivers.Add(new Driver { Name = "سائق 1", Phone = "0790000000" });

        var cust = new Customer { Name = "زبون تجريبي", Phone = "0791111111" };
        db.Customers.Add(cust);
        // عدّة عناوين بإحداثيات متباعدة لإبراز تحسين مسار التوزيع.
        db.Addresses.AddRange(
            new Address { Customer = cust, Label = "البيت", Line = "عمان - شارع المثال", Latitude = 31.980, Longitude = 35.900, IsDefault = true },
            new Address { Customer = cust, Label = "العمل", Line = "عمان - الدوار الخامس", Latitude = 31.965, Longitude = 35.885 },
            new Address { Customer = cust, Label = "بيت الأهل", Line = "عمان - الجاردنز", Latitude = 31.995, Longitude = 35.870 });

        var sort = 0;
        Category Cat(string ar, string en, string icon)
        {
            var c = new Category { NameAr = ar, NameEn = en, Icon = icon, SortOrder = sort++ };
            db.Categories.Add(c);
            return c;
        }

        var bc = 6291000000000L;
        Product Prod(Category cat, string ar, string en, string emoji, string unit, TaxClass tax, string desc, params decimal[] prices)
        {
            var p = new Product
            {
                NameAr = ar, NameEn = en, Emoji = emoji, UnitSize = unit, TaxClass = tax,
                Description = desc, Category = cat, Barcode = (bc++).ToString()
            };
            db.Products.Add(p);
            // أسعار متفاوتة على التجار (شاملة الضريبة) — تتولّد منها نسبة التوفير.
            for (var i = 0; i < prices.Length && i < merchants.Length; i++)
                db.MerchantProducts.Add(new MerchantProduct
                {
                    Merchant = merchants[i], Product = p, Price = prices[i], StockQty = 80
                });
            return p;
        }

        var beverages = Cat("مشروبات", "Beverages", "🥤");
        Prod(beverages, "مشروب غازي", "Soft Drink", "🥤", "330ml", TaxClass.Sixteen, "علبة مشروب غازي منعش بارد.", 0.60m, 0.70m, 0.65m);
        Prod(beverages, "عصير برتقال", "Orange Juice", "🧃", "1L", TaxClass.Five, "عصير برتقال طبيعي 100%.", 1.20m, 1.45m, 1.35m);
        Prod(beverages, "مياه معدنية", "Mineral Water", "💧", "1.5L", TaxClass.Zero, "مياه شرب معدنية نقية.", 0.35m, 0.45m, 0.40m);

        var milk = Cat("حليب", "Milk", "🥛");
        Prod(milk, "حليب طازج", "Fresh Milk", "🥛", "1L", TaxClass.Zero, "حليب بقري طازج كامل الدسم.", 0.90m, 1.10m, 1.00m);
        Prod(milk, "حليب مجفف", "Powdered Milk", "🥛", "900g", TaxClass.Five, "حليب مجفف غني بالكالسيوم.", 6.50m, 7.20m, 6.90m);

        var dairy = Cat("ألبان وأجبان", "Dairy & Eggs", "🧀");
        Prod(dairy, "لبنة بقرية", "Labneh", "🧀", "500g", TaxClass.Five, "لبنة كريمية غنية بالنكهة.", 2.10m, 2.50m, 2.30m);
        Prod(dairy, "جبنة بيضاء", "White Cheese", "🧀", "400g", TaxClass.Five, "جبنة بيضاء طرية.", 2.80m, 3.20m, 3.00m);
        Prod(dairy, "بيض", "Eggs", "🥚", "12pcs", TaxClass.Zero, "بيض طازج عبوة 12 حبة.", 1.50m, 1.80m, 1.65m);

        var bakery = Cat("مخبوزات", "Bakery", "🍞");
        Prod(bakery, "خبز برغر", "Burger Buns", "🍔", "6pcs", TaxClass.Sixteen, "خبز برجر طري، عبوة 6 قطع، مثالي للوجبات العائلية.", 0.55m, 0.65m, 0.60m);
        Prod(bakery, "خبز توست", "Toast Bread", "🍞", "600g", TaxClass.Sixteen, "خبز توست أبيض طازج.", 0.70m, 0.85m, 0.80m);
        Prod(bakery, "كرواسون", "Croissant", "🥐", "4pcs", TaxClass.Sixteen, "كرواسون بالزبدة هشّ.", 1.60m, 1.95m, 1.80m);

        var produce = Cat("خضار وفواكه", "Fruit & Veg", "🥬");
        Prod(produce, "موز", "Banana", "🍌", "1kg", TaxClass.Zero, "موز طازج ناضج.", 0.80m, 1.00m, 0.90m);
        Prod(produce, "طماطم", "Tomato", "🍅", "1kg", TaxClass.Zero, "طماطم حمراء طازجة.", 0.55m, 0.75m, 0.65m);
        Prod(produce, "خيار", "Cucumber", "🥒", "1kg", TaxClass.Zero, "خيار أخضر طازج.", 0.50m, 0.65m, 0.58m);

        var staples = Cat("معلّبات وأساسيات", "Pantry", "🥫");
        Prod(staples, "أرز", "Rice", "🍚", "5kg", TaxClass.Zero, "أرز حبة طويلة فاخر.", 4.50m, 5.20m, 4.90m);
        Prod(staples, "زيت نباتي", "Vegetable Oil", "🛢️", "1.5L", TaxClass.Five, "زيت نباتي للطهي والقلي.", 2.40m, 2.85m, 2.60m);
        Prod(staples, "تونة معلّبة", "Canned Tuna", "🐟", "160g", TaxClass.Sixteen, "تونة في زيت دوّار الشمس.", 1.10m, 1.35m, 1.25m);

        var snacks = Cat("وجبات خفيفة", "Snacks", "🍫");
        Prod(snacks, "شوكولاتة", "Chocolate", "🍫", "100g", TaxClass.Sixteen, "لوح شوكولاتة بالحليب.", 0.75m, 0.95m, 0.85m);
        Prod(snacks, "رقائق بطاطا", "Potato Chips", "🥔", "150g", TaxClass.Sixteen, "رقائق بطاطا مقرمشة.", 0.65m, 0.80m, 0.72m);

        var care = Cat("العناية والتنظيف", "Care & Cleaning", "🧴");
        Prod(care, "صابون", "Soap", "🧼", "500g", TaxClass.Sixteen, "صابون منظّف فعّال.", 1.30m, 1.60m, 1.45m);
        Prod(care, "شامبو", "Shampoo", "🧴", "400ml", TaxClass.Sixteen, "شامبو للعناية بالشعر.", 2.20m, 2.70m, 2.45m);

        await db.SaveChangesAsync();
    }
}
