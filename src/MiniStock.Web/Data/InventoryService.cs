using Microsoft.EntityFrameworkCore;

namespace MiniStock.Web.Data;

public sealed record DashboardSummary(
    int ProductCount,
    int TotalUnits,
    decimal StockValue,
    int LowStockCount,
    IReadOnlyList<Product> LowStock);

/// <summary>
/// All inventory rules live here, so the UI stays thin and the rules are unit-tested.
/// </summary>
public class InventoryService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<Product>> GetProductsAsync(string? search = null, bool lowOnly = false)
    {
        await using var db = await factory.CreateDbContextAsync();

        IQueryable<Product> query = db.Products.AsNoTracking().Include(p => p.Supplier);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.Like(p.Name, term) ||
                EF.Functions.Like(p.Sku, term) ||
                EF.Functions.Like(p.Category, term));
        }

        // SQLite cannot order by decimal columns in SQL, so sort by text columns only.
        var products = await query.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
        return lowOnly ? products.Where(p => p.IsLow).ToList() : products;
    }

    public async Task<Product> AddProductAsync(
        string sku, string name, string category,
        decimal unitPrice, int quantity, int reorderLevel, int? supplierId = null)
    {
        sku = (sku ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        category = (category ?? "").Trim();

        if (sku.Length == 0) throw new ArgumentException("SKU is required.", nameof(sku));
        if (name.Length == 0) throw new ArgumentException("Name is required.", nameof(name));
        if (category.Length == 0) throw new ArgumentException("Category is required.", nameof(category));
        if (unitPrice < 0) throw new ArgumentException("Price cannot be negative.", nameof(unitPrice));
        if (quantity < 0) throw new ArgumentException("Quantity cannot be negative.", nameof(quantity));
        if (reorderLevel < 0) throw new ArgumentException("Reorder level cannot be negative.", nameof(reorderLevel));

        await using var db = await factory.CreateDbContextAsync();

        if (await db.Products.AnyAsync(p => p.Sku == sku))
            throw new InvalidOperationException($"SKU {sku} already exists.");

        var product = new Product
        {
            Sku = sku,
            Name = name,
            Category = category,
            UnitPrice = unitPrice,
            Quantity = quantity,
            ReorderLevel = reorderLevel,
            SupplierId = supplierId,
        };
        db.Products.Add(product);

        if (quantity > 0)
        {
            db.Movements.Add(new StockMovement
            {
                Product = product,
                Delta = quantity,
                Reason = "Initial stock",
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return product;
    }

    /// <summary>Adds (positive delta) or removes (negative delta) stock and records why.</summary>
    public async Task<StockMovement> AdjustStockAsync(int productId, int delta, string reason)
    {
        if (delta == 0) throw new ArgumentException("The adjustment cannot be zero.", nameof(delta));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));

        await using var db = await factory.CreateDbContextAsync();

        var product = await db.Products.FindAsync(productId)
            ?? throw new KeyNotFoundException($"Product {productId} not found.");

        if (product.Quantity + delta < 0)
            throw new InvalidOperationException(
                $"Not enough stock of {product.Sku}: {product.Quantity} available, {-delta} requested.");

        product.Quantity += delta;

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Delta = delta,
            Reason = reason.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Movements.Add(movement);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException($"Stock of {product.Sku} changed in the meantime. Please try again.");
        }

        return movement;
    }

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        var products = await GetProductsAsync();
        var low = products.Where(p => p.IsLow).OrderBy(p => p.Quantity - p.ReorderLevel).ToList();

        return new DashboardSummary(
            ProductCount: products.Count,
            TotalUnits: products.Sum(p => p.Quantity),
            StockValue: products.Sum(p => p.StockValue),
            LowStockCount: low.Count,
            LowStock: low);
    }

    public async Task<List<StockMovement>> GetRecentMovementsAsync(int take = 100)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Movements.AsNoTracking()
            .Include(m => m.Product)
            .OrderByDescending(m => m.Id)
            .Take(take)
            .ToListAsync();
    }
}
