using Microsoft.EntityFrameworkCore;
using MiniStock.Web.Components;
using MiniStock.Web.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// A factory (not a shared DbContext) is the recommended pattern for Blazor Server.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=ministock.db"));
builder.Services.AddScoped<InventoryService>();

var app = builder.Build();

await SeedData.EnsureSeededAsync(app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
