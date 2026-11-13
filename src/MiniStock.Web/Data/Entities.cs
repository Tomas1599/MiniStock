using System.ComponentModel.DataAnnotations;

namespace MiniStock.Web.Data;

public class Supplier
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public List<Product> Products { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal UnitPrice { get; set; }

    // Concurrency token: two users adjusting the same product at once cannot silently overwrite each other.
    [ConcurrencyCheck]
    public int Quantity { get; set; }

    public int ReorderLevel { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public bool IsLow => Quantity <= ReorderLevel;
    public decimal StockValue => Quantity * UnitPrice;
}

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Delta { get; set; }
    public required string Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
