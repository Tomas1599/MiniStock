using Microsoft.EntityFrameworkCore;

namespace MiniStock.Web.Data;

/// <summary>Fictional demo data so the app is useful the first time it runs.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        if (await db.Products.AnyAsync()) return;

        var norte = new Supplier { Name = "Norte Tools Ltd", Email = "orders@example.com" };
        var atlantic = new Supplier { Name = "Atlantic Supplies", Email = "sales@example.com" };
        var iberia = new Supplier { Name = "Iberia Wholesale", Email = "hello@example.com" };
        db.Suppliers.AddRange(norte, atlantic, iberia);

        (string Sku, string Name, string Category, decimal Price, int Qty, int Reorder, Supplier Supplier)[] items =
        [
            ("TL-1001", "Cordless drill 18V",         "Tools",       89.90m,  14,   5, norte),
            ("TL-1002", "Claw hammer 500 g",          "Tools",       12.50m,  40,  10, norte),
            ("TL-1003", "Adjustable wrench 250 mm",   "Tools",       15.75m,   6,   8, norte),
            ("TL-1004", "Spirit level 60 cm",         "Tools",       18.20m,  22,   6, norte),
            ("TL-1005", "Screwdriver set (24 pcs)",   "Tools",       24.99m,   3,   6, norte),
            ("TL-1006", "Tape measure 5 m",           "Tools",        6.40m,  58,  15, norte),
            ("FS-2001", "Wood screws 4x40 (box 200)", "Fasteners",    5.90m, 120,  30, atlantic),
            ("FS-2002", "Hex bolts M8x50 (box 100)",  "Fasteners",    9.80m,  75,  20, atlantic),
            ("FS-2003", "Wall plugs 8 mm (box 100)",  "Fasteners",    3.60m,   9,  25, atlantic),
            ("FS-2004", "Cable ties 300 mm (100)",    "Fasteners",    4.20m,  64,  20, atlantic),
            ("FS-2005", "Washers M8 (box 250)",       "Fasteners",    3.10m,  48,  15, atlantic),
            ("EL-3001", "LED bulb E27 10 W",          "Electrical",   2.99m, 210,  60, iberia),
            ("EL-3002", "Extension lead 5 m",         "Electrical",  11.90m,  17,   8, iberia),
            ("EL-3003", "Wall socket, white",         "Electrical",   3.80m,  34,  12, iberia),
            ("EL-3004", "Cable 3x1.5 mm2 (per m)",    "Electrical",   1.35m, 450, 150, iberia),
            ("EL-3005", "Circuit breaker 16 A",       "Electrical",   7.60m,   4,  10, iberia),
            ("PT-4001", "Exterior paint 5 L, white",  "Paint",       32.00m,  12,   6, atlantic),
            ("PT-4002", "Interior paint 5 L, ivory",  "Paint",       28.50m,  15,   6, atlantic),
            ("PT-4003", "Paint roller 230 mm",        "Paint",        4.90m,  26,  10, atlantic),
            ("PT-4004", "Masking tape 50 m",          "Paint",        2.40m,   7,  20, atlantic),
            ("PT-4005", "Sandpaper assorted (10)",    "Paint",        5.20m,  31,  12, norte),
            ("SF-5001", "Work gloves, size L",        "Safety",       4.50m,  45,  15, iberia),
            ("SF-5002", "Safety glasses",             "Safety",       3.30m,  38,  12, iberia),
            ("SF-5003", "Dust mask FFP2 (box 10)",    "Safety",       9.90m,   5,  10, iberia),
        ];

        var start = DateTime.UtcNow.AddDays(-14);
        var i = 0;
        foreach (var (sku, name, category, price, qty, reorder, supplier) in items)
        {
            var product = new Product
            {
                Sku = sku, Name = name, Category = category,
                UnitPrice = price, Quantity = qty, ReorderLevel = reorder,
                Supplier = supplier,
            };
            db.Products.Add(product);
            db.Movements.Add(new StockMovement
            {
                Product = product,
                Delta = qty,
                Reason = "Initial stock",
                CreatedAt = start.AddMinutes(i++),
            });
        }

        await db.SaveChangesAsync();
    }
}
