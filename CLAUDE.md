# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Running the App

The server now hosts the Blazor WASM frontend — only one process is needed.

```powershell
# Dev with hot reload — serves both API and Blazor WASM at http://localhost:5111
dotnet watch --project FinTool.Server --launch-profile http
```

No migration commands are needed — the database is created automatically on first run:
- Windows: `%LocalAppData%\FamilyFinance\family-finance.db`
- Linux: `~/.local/share/FamilyFinance/family-finance.db`

`dotnet restore` is required on a fresh clone before the first run.

## Building Release Packages

```powershell
# Windows PowerShell — builds win-x64 and linux-x64 zips in ./release/
.\publish.ps1 -Version 1.2.0
```

```bash
# Bash (from repo root)
./publish.sh 1.2.0
```

Each zip contains a self-contained single-file executable plus the `wwwroot/` folder with the Blazor WASM runtime. On Linux: `chmod +x FinTool.Server && ./FinTool.Server`. Listens on `http://0.0.0.0:5111` by default; override with `ASPNETCORE_URLS` env var.

## Architecture

Two-project solution with no shared library — models are intentionally duplicated between client (`FinTool/Models/`) and server (inline entity classes in `FinTool.Server/Program.cs`).

**`FinTool/`** — Blazor WebAssembly app. Served by the server in both dev and production. All data access goes through HTTP services (`*Service.cs`) using `builder.HostEnvironment.BaseAddress` as the API root (same origin as where the app is served). Services maintain an in-memory cache (`_cache` field, nullable, nulled out on mutation). Components are either full pages (`Pages/`) or tab/dialog components (`Components/`) embedded in pages. Large components use a code-behind pattern: markup in `.razor`, logic in `.razor.cs` (same partial class). `BudgetPlanner.razor` keeps a small `@code` block containing only `DrawTable` (a `RenderFragment` with embedded Razor HTML) while the rest lives in `BudgetPlanner.razor.cs`.

**`FinTool.Server/`** — minimal API server, split across several files:
- `Program.cs` — startup/config, schema init, endpoint registration calls only
- `Data/AppDbContext.cs` — EF Core DbContext
- `Models/Entities.cs` — all entity classes and request/response records
- `GlobalUsings.cs` — project-wide global usings
- `Endpoints/AuthEndpoints.cs` — auth routes + helpers (GenerateToken, HashPassword)
- `Endpoints/TransactionEndpoints.cs` — transaction CRUD routes
- `Endpoints/BudgetEndpoints.cs` — budget categories, revenue categories, budget drafts
- `Endpoints/AccountEndpoints.cs` — accounts + goals routes
- `Endpoints/MiscEndpoints.cs` — merchant cache, closed months, recurring
- `Endpoints/AiEndpoints.cs` — classify + chat + RunClaudeAsync

New tables are added via `CREATE TABLE IF NOT EXISTS` in `Program.cs` startup. Columns added later use `ALTER TABLE … ADD COLUMN` wrapped in `try/catch`.

## Key Conventions

**Amount sign convention** — positive = money out (expense), negative = money in (revenue/refund). Enforced throughout: `t.Amount > 0` for expenses, `t.IsRevenue` flag for income lines, revenue totals computed as `Sum(t => -t.Amount)`.

**JSON serialization** — server uses `PropertyNamingPolicy = null` (PascalCase responses) with `PropertyNameCaseInsensitive = true` reads, so client model property names must match server entity property names exactly.

**Month state** — `MonthState` is a scoped service shared across all pages. Components subscribe to `MonthSvc.OnChange` in `OnAfterRenderAsync` (first render) and must unsubscribe in `Dispose()`. The selected month drives filtering on every data page.

**Rendering pattern** — data is loaded in `OnAfterRenderAsync` with `if (!firstRender) return; await Task.Delay(1);` to avoid blocking the initial paint. A `_loading` bool controls a skeleton/overlay until data arrives.

**Color system** — soft palette is defined in `ColorPalette.cs` (`ColorPalette.Soft` array, `ColorPalette.Random()`). Dialogs use `ColorPicker.razor` (swatch row + native fallback). Inline row editors use `InlineColorPicker.razor` (circle that opens a floating swatch panel). New items always call `ColorPalette.Random()` for their default color.

**MudBlazor table stability** — never conditionally switch component types at the same render-tree position within `MudTable.RowTemplate` based on state that changes on every render (e.g. `_isMonthClosed`). Use `Disabled="@_isMonthClosed"` on the same component instead. Switching types per row on every month change causes WASM GC crashes.

**Nullable generic MudBlazor items** — `MudSelectItem<Guid?>` has a known cast issue. Use `T="string"` with the GUID serialised as a string and parse it back in `ValueChanged`.

## Adding a New Data Entity

1. **Server** (`FinTool.Server/Program.cs`): add entity class, `DbSet<>` on `AppDbContext`, `ToTable()` in `OnModelCreating`, `CREATE TABLE IF NOT EXISTS` in startup, and CRUD endpoints.
2. **Client model** (`FinTool/Models/`): matching C# class (property names must match server entity exactly).
3. **Client service** (`FinTool/Services/`): HTTP wrapper with nullable `_cache` field; null it out in every mutating method.
4. **Register** the service in `FinTool/Program.cs` as `AddScoped<>`.
5. **Page/component**: inject the service, load in `OnAfterRenderAsync(firstRender)` pattern.

## Page Navigation

Navigation links live in `MainLayout.razor` (`MudNavMenu`). Icons are letter-in-box SVGs defined as static strings in `NavIcons.cs`. Current page order: Dashboard → Transactions → Recurring → Planner → Goals → Accounts → Settings.

## AI Integration

`/api/classify` and `/api/chat` both shell out to `claude -p` via `RunClaudeAsync`. The call pipes a constructed prompt to stdin and reads stdout. It times out after 120 s. If the CLI is absent the server still starts — AI endpoints return errors but all other functionality works.

## Data the Server Owns

| Table | Notes |
|---|---|
| `Transactions` | `Amount` positive = expense; `GoalId` nullable FK to Goals |
| `BudgetCategories` | `IsIgnored` excludes from dashboard totals |
| `RevenueCategories` | No `MonthlyAmount`; `IsIgnored` same as above |
| `BudgetDrafts` | `ExpensesJson` / `RevenueJson` stored as JSON text columns |
| `Goals` | `CurrentAmount` = manual starting balance; actual progress = starting + tagged transactions |
| `Accounts` | Accent color used as chip border in transaction rows |
| `MerchantCache` | PK is normalised description (uppercase); maps to category name |
| `ClosedMonths` | PK is `"yyyy-MM"` string; locked months are read-only in the UI |
