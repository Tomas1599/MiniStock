# MiniStock

[![CI](https://github.com/Tomas1599/MiniStock/actions/workflows/ci.yml/badge.svg)](https://github.com/Tomas1599/MiniStock/actions/workflows/ci.yml)

A small **inventory management app** built with **Blazor Server (.NET 10)**, **Entity Framework Core** and **SQLite**. Track products, adjust stock with a reason, see what needs reordering, and keep a full history of every movement.

It is a portfolio project that shows how I structure a business application: rules in a tested service layer, a thin UI on top, and a database that protects its own integrity. All data is fictional demo data.

## Features

- **Dashboard** with product count, units in stock, total stock value and low-stock alerts
- **Products** list with search (name, SKU, category) and a "low stock only" filter
- **Stock adjustments** (+1 / -1 buttons or any amount) that always record a reason
- **Movement history**: an audit trail of every change, newest first
- **Add products** with validation (unique SKU, no negative prices or quantities)
- Seeded with realistic demo data on first run

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/Tomas1599/MiniStock.git
cd MiniStock
dotnet run --project src/MiniStock.Web
```

Then open the URL printed in the console. A `ministock.db` SQLite file is created and seeded automatically; delete it to reset the demo data.

```bash
dotnet test
```

## Design notes

- **Business rules live in `InventoryService`**, not in the Razor pages. The UI only calls the service and shows its errors, so the rules can be unit-tested without a browser.
- **Stock can never go negative.** An adjustment that would do so is rejected and nothing is written; going to exactly zero is allowed.
- **Every change is an audited movement.** Stock quantity and its movement row are saved in a single `SaveChanges`, so they cannot drift apart.
- **Optimistic concurrency.** `Product.Quantity` is a concurrency token: if two users adjust the same product at the same moment, the second one gets a clear "changed in the meantime, try again" message instead of silently overwriting the first.
- **`IDbContextFactory`** instead of a shared `DbContext`, which is the recommended pattern for Blazor Server where a circuit lives for a long time.
- **Tests run against real SQLite** (in memory), not a mock, so queries, unique constraints and the audit trail are exercised for real.

## Project structure

```
src/MiniStock.Web/
  Data/         Entities, DbContext, InventoryService (the rules), seed data
  Components/   Razor pages (Dashboard, Products, Movements) and layout
tests/MiniStock.Tests/
                xUnit tests for InventoryService and seeding
```

## Possible next steps

- Suppliers page and purchase orders that raise stock on receipt
- Authentication and per-user movement history
- Export to CSV
- Swap SQLite for SQL Server or PostgreSQL (only the connection setup would change)

## License

[MIT](LICENSE)

---

## Português

Aplicação de **gestão de stock** em **Blazor Server (.NET 10)**, **Entity Framework Core** e **SQLite**. Permite ver produtos, ajustar stock com motivo, perceber o que precisa de reposição e consultar o histórico de todos os movimentos.

É um projeto de portefólio com dados totalmente fictícios. As regras de negócio estão num serviço testado (`InventoryService`) e a interface limita-se a usá-lo.

**Executar:** instalar o SDK do .NET 10 e correr `dotnet run --project src/MiniStock.Web`. **Testes:** `dotnet test`.
