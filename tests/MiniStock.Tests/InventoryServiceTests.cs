using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniStock.Web.Data;

namespace MiniStock.Tests;

/// <summary>An in-memory SQLite database that lives as long as the test.</summary>
public sealed class TestDb : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDb()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public InventoryService Service => new(this);

    public void Dispose() => _connection.Dispose();
}

public class InventoryServiceTests
{
    [Fact]
    public async Task AdjustStock_AddsStock_AndRecordsMovement()
    {
        using var testDb = new TestDb();
        var service = testDb.Service;
        var product = await service.AddProductAsync("a-1", "Widget", "Parts", 2.50m, quantity: 10, reorderLevel: 3);

        await service.AdjustStockAsync(product.Id, +5, "Delivery");

        var products = await service.GetProductsAsync();
        Assert.Equal(15, Assert.Single(products).Quantity);

        var movements = await service.GetRecentMovementsAsync();
        Assert.Equal(2, movements.Count); // initial stock + delivery
        Assert.Equal(5, movements[0].Delta);
        Assert.Equal("Delivery", movements[0].Reason);
    }

    [Fact]
    public async Task AdjustStock_RejectsRemovingMoreThanAvailable_AndLeavesStockUntouched()
    {
        using var testDb = new TestDb();
        var service = testDb.Service;
        var product = await service.AddProductAsync("A-1", "Widget", "Parts", 1m, quantity: 4, reorderLevel: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdjustStockAsync(product.Id, -5, "Sold"));

        Assert.Equal(4, Assert.Single(await service.GetProductsAsync()).Quantity);
        Assert.Single(await service.GetRecentMovementsAsync()); // only the initial stock
    }

    [Fact]
    public async Task AdjustStock_AllowsGoingToExactlyZero()
    {
        using var testDb = new TestDb();
        var service = testDb.Service;
        var product = await service.AddProductAsync("A-1", "Widget", "Parts", 1m, quantity: 4, reorderLevel: 1);

        await service.AdjustStockAsync(product.Id, -4, "Sold");

        Assert.Equal(0, Assert.Single(await service.GetProductsAsync()).Quantity);
    }

    [Theory]
    [InlineData(0, "Reason")]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    public async Task AdjustStock_RejectsInvalidInput(int delta, string reason)
    {
        using var testDb = new TestDb();
        var service = testDb.Service;
        var product = await service.AddProductAsync("A-1", "Widget", "Parts", 1m, quantity: 4, reorderLevel: 1);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AdjustStockAsync(product.Id, delta, reason));
    }

    [Fact]
    public async Task AdjustStock_UnknownProduct_Throws()
    {
        using var testDb = new TestDb();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => testDb.Service.AdjustStockAsync(999, 1, "Reason"));
    }

    [Fact]
    public async Task AddProduct_RejectsDuplicateSku_IgnoringCaseAndWhitespace()
    {
        using var testDb = new TestDb();
        var service = testDb.Service;
        await service.AddProductAsync("A-1", "Widget", "Parts", 1m, 0, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddProductAsync("  a-1 ", "Other", "Parts", 1m, 0, 0));
    }

    [Fact]
    public async Task AddProduct_RejectsNegativeValues()
    {
        using var testDb = new TestDb();
        var service = testDb.Service;

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddProductAsync("A-1", "W", "P", -1m, 0, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddProductAsync("A-2", "W", "P", 1m, -1, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddProductAsync("A-3", "W", "P", 1m, 0, -1));
    }

    [Fact]
    public async Task Summary_CountsLowStock_AndTotalsValue()
    {
        using var testDb = new TestDb();
        var service = testDb.Service;
        await service.AddProductAsync("A-1", "Plenty", "Parts", 2.00m, quantity: 10, reorderLevel: 3);  // 20.00
        await service.AddProductAsync("A-2", "AtLimit", "Parts", 5.00m, quantity: 3, reorderLevel: 3);  // 15.00, low (<=)
        await service.AddProductAsync("A-3", "Empty", "Parts", 7.50m, quantity: 0, reorderLevel: 2);    // 0.00, low

        var summary = await service.GetSummaryAsync();

        Assert.Equal(3, summary.ProductCount);
        Assert.Equal(13, summary.TotalUnits);
        Assert.Equal(35.00m, summary.StockValue);
        Assert.Equal(2, summary.LowStockCount);
        Assert.Equal(new[] { "A-3", "A-2" }, summary.LowStock.Select(p => p.Sku)); // most urgent first
    }

    [Fact]
    public async Task GetProducts_FiltersBySearchAndLowOnly()
    {
        using var testDb = new TestDb();
        var service = testDb.Service;
        await service.AddProductAsync("TL-1", "Hammer", "Tools", 10m, quantity: 1, reorderLevel: 5);
        await service.AddProductAsync("TL-2", "Wrench", "Tools", 12m, quantity: 20, reorderLevel: 5);
        await service.AddProductAsync("PT-1", "Brush", "Paint", 3m, quantity: 20, reorderLevel: 5);

        Assert.Equal(2, (await service.GetProductsAsync(search: "tools")).Count);      // category, case-insensitive
        Assert.Single(await service.GetProductsAsync(search: "PT-1"));                  // SKU
        Assert.Single(await service.GetProductsAsync(search: "wren"));                  // partial name
        Assert.Equal("TL-1", Assert.Single(await service.GetProductsAsync(lowOnly: true)).Sku);
    }

    [Fact]
    public async Task Seed_CreatesDemoData_AndIsIdempotent()
    {
        using var testDb = new TestDb();

        await SeedData.EnsureSeededAsync(testDb);
        var first = (await testDb.Service.GetProductsAsync()).Count;
        await SeedData.EnsureSeededAsync(testDb);
        var second = (await testDb.Service.GetProductsAsync()).Count;

        Assert.True(first > 10);
        Assert.Equal(first, second);
        Assert.True((await testDb.Service.GetSummaryAsync()).LowStockCount > 0);
    }
}
