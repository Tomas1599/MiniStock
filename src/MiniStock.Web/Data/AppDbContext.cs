using Microsoft.EntityFrameworkCore;

namespace MiniStock.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockMovement> Movements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.UnitPrice).HasPrecision(18, 2);
            e.Ignore(p => p.IsLow);
            e.Ignore(p => p.StockValue);
        });

        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
