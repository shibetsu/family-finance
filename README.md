# Family Finance

A personal finance tracker built for Desjardins (AccèsD) account holders. Paste your transaction history directly from the AccèsD web portal, let a local Claude AI classify them into your budget categories, then review monthly spending through an interactive dashboard.

Data is stored in a local **SQLite database** — no account, no cloud, no third-party service.

---

## Features

### Global Month Picker
- The month selector lives in the **top app bar** and is shared across all pages — changing it on one page changes it everywhere
- Prev/next chevrons navigate month by month; the forward arrow is disabled on the current month
- The selected month is reflected in the URL (`?month=yyyy-MM`) for deep linking and browser back/forward support

### Dashboard
- **Summary cards** — monthly budget, total spent, remaining budget, transaction count, revenue, and net balance
- **Spending by Category** — donut chart; click a slice (or the center label) to jump to that category's transactions for the current month
- **6-Month Trend** — line chart showing expenses and revenue over the last six months
- **Expense & Revenue breakdowns** — per-category tables with budget progress bars, transaction counts, and per-transaction averages; category names are clickable and navigate to the Transactions page pre-filtered to that category and month
- **Ignored transactions** — a collapsible panel at the bottom lists all transactions belonging to ignored categories for the selected month, showing total ignored expenses and revenue; helps audit what is excluded from your totals

### Transactions
- **Import from AccèsD** — paste the transaction table from the AccèsD portal; the parser auto-detects the format (standard credit card, BONIDOLLARS credit card, or debit account); the Import & Classify button activates as soon as text is pasted
- **AI classification** — a local Claude server classifies each expense into your budget categories automatically; previously seen merchants are cached and applied instantly (no extra HTTP call per transaction)
- **Revenue auto-detection** — transactions where money comes in (negative amounts) are automatically treated as revenue; choose the revenue category directly in the import review table
- **Inline category editing** — change the category on any confirmed transaction from the table; updates the merchant cache so future imports are pre-classified correctly
- **Mass delete** — select multiple transactions with checkboxes (with indeterminate header state) and delete them in one action
- **Category filter** — click the filter icon (funnel) in the Category column header to open a checkbox popover; active filter is indicated by a filled blue icon; filters are preserved when navigating away and back
- **Sort** — click the Date, Category, or Amount column headers to sort; Date defaults to newest first
- **Close Month** — lock a past month to prevent any further edits; useful when you later rename categories or adjust budgets without affecting historical records

### Budget Categories
- Create and manage expense categories (name, monthly budget amount, color)
- Mark a category as ignored to exclude it from dashboard totals and charts

### Revenue Categories
- Create and manage income categories (name, color)
- Mark a category as ignored to exclude it from dashboard totals

### Budget Planner *(new)*
- **Multiple drafts** — create as many budget scenarios as you want; drafts are named, renameable, and deletable from a tab bar
- **Import from history** — the "Import history" menu detects up to 3 contiguous past months with transaction data and offers to create a draft pre-populated with averaged actual spending per category (preserving real category colors and ordering by amount)
- **Inline editing** — edit category names, amounts (numeric field), and colors (native color picker overlaid on the color dot) directly in the table without a modal
- **Historical reference column** — each row shows the actual 3-month average alongside the draft amount; the reference is color-coded (green = under, warning = over) with a tooltip showing the exact difference
- **Live summary** — Monthly Budget, Monthly Revenue, and Net cards update as you type
- **Isolated data** — drafts are stored in the SQLite database (`BudgetDrafts` table) but never affect your real budget categories, revenue categories, or transactions

---

## Architecture

```
family-finance/
├── FinTool/                    # Blazor WebAssembly app
│   ├── Components/
│   │   ├── DashboardTab.razor
│   │   ├── TransactionsTab.razor
│   │   ├── ImportDialog.razor
│   │   ├── BudgetTab.razor
│   │   ├── RevenueTab.razor
│   │   ├── CategoryDialog.razor
│   │   └── RevenueCategoryDialog.razor
│   ├── Layout/
│   │   └── MainLayout.razor    # App shell, global month picker, drawer, theme
│   ├── Models/
│   │   ├── Transaction.cs
│   │   ├── BudgetCategory.cs
│   │   ├── RevenueCategory.cs
│   │   └── BudgetDraft.cs      # Draft + DraftRow models for Budget Planner
│   ├── Pages/
│   │   └── BudgetPlanner.razor # Budget scenario builder
│   ├── Services/
│   │   ├── LocalStorageService.cs      # JS interop — dark mode pref only
│   │   ├── TransactionService.cs       # Transaction CRUD (via HTTP, in-memory cache)
│   │   ├── BudgetService.cs            # Budget category CRUD (via HTTP, in-memory cache)
│   │   ├── RevenueCategoryService.cs   # Revenue category CRUD (via HTTP, in-memory cache)
│   │   ├── MerchantCacheService.cs     # Description → category memory (bulk-loaded, in-memory)
│   │   ├── ClosedMonthService.cs       # Month lock tracking (via HTTP, in-memory cache)
│   │   ├── ClaudeService.cs            # AI classification (via HTTP)
│   │   ├── MonthState.cs               # Global selected month — shared across all pages
│   │   ├── TransactionFilterState.cs   # Persists transaction page category filter across navigation
│   │   ├── BudgetPlannerService.cs     # Draft CRUD (via HTTP)
│   │   └── DesjardinsParser.cs         # AccèsD paste parser
│   ├── NavIcons.cs             # SVG letter-in-box nav icons matching the app logo
│   └── wwwroot/                # Static assets, PWA icons, manifest
│
└── FinTool.Server/             # ASP.NET Core API — SQLite database + AI bridge
    └── Program.cs              # EF Core DbContext, REST endpoints, Claude runner
```

### Data flow

```
AccèsD paste
    └─► DesjardinsParser              (parse into Transaction objects)
            └─► MerchantCacheService  (lookup previously learned categories)
            └─► ClaudeService         (classify uncached transactions via AI)
                    └─► FinTool.Server    (shells out to `claude -p`)
            └─► ImportDialog          (user reviews & confirms)
                    └─► TransactionService
                            └─► FinTool.Server  (persists to SQLite)
```

---

## Getting Started

### Prerequisites

| Requirement | Notes |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download) | Required to build and run both projects |
| [Claude Code CLI](https://claude.ai/code) | Optional — only needed for AI auto-classification (`claude` must be on your PATH) |

> **`FinTool.Server` is required** — it hosts the SQLite database and exposes the REST API the Blazor app depends on. The app will not load any data without it running.
>
> **AI classification is optional.** If the Claude CLI is not installed, the server still starts and all data features work — transactions just won't be auto-classified during import.

### Running the app

The easiest way is to use the included launcher, which opens both processes in separate terminal windows:

```cmd
start.cmd
```

Then open [http://localhost:5254](http://localhost:5254) in your browser.

**Or start each project manually:**

```powershell
# Terminal 1 — API server (required: database + optional AI)
dotnet run --project FinTool.Server

# Terminal 2 — Blazor app (with hot reload)
dotnet watch --project FinTool --launch-profile http
```

**First run:** `FinTool.Server` automatically creates the SQLite database at
`%LocalAppData%\FamilyFinance\family-finance.db` — no migration commands needed.

**Fresh clone:** run `dotnet restore` once before the above if packages haven't been downloaded yet.

---

## How to Use

### 1. Set up categories

Before importing, go to **Budget** and create your expense categories (e.g. Groceries, Restaurants, Transport). Optionally, go to **Revenue** and create income categories (e.g. Salary, Freelance).

Each category has a name, a monthly budget amount, and a color used in charts.

### 2. Import transactions

1. Log into [AccèsD](https://accesd.desjardins.com), navigate to your account's transaction history, and **select all and copy** the table.
2. In Family Finance, click **Import** (top right of the Transactions page).
3. Paste into the text area. The format is auto-detected (credit card, BONIDOLLARS card, or debit account).
4. Click **Import & Classify** — the app checks the merchant cache, then sends uncached expenses to the local Claude server for classification.
5. Review the suggested categories in the table. Adjust any that are wrong. Income transactions (money coming in) are automatically flagged as revenue — pick the revenue category from the dropdown.
6. Click **Confirm All** to save. The modal closes and the transactions appear in the table.

### 3. Edit categories later

Open the **Transactions** tab, navigate to the relevant month using the **month picker in the top bar**, and use the dropdown in each row's Category column to reassign it. Changes are saved immediately.

### 4. Close a month

Once a month is fully reviewed, click **Close Month** in the header. This locks all transactions in the currently viewed month — category dropdowns become read-only and the delete button is hidden. Locked months display a 🔒 icon next to the month name.

This protects historical data when you later rename categories or adjust budget amounts.

### 5. Plan future budgets

Open the **Budget Planner** from the navigation drawer. Click **Import history** to create a draft seeded from your actual recent spending, or **New Draft** to start from scratch. You can maintain multiple named drafts and experiment freely — nothing here touches your real data.

---

## Data Storage

All application data is persisted in a **SQLite database** managed by `FinTool.Server`.

**Database location:** `%LocalAppData%\FamilyFinance\family-finance.db`

| Table | Contents |
|---|---|
| `Transactions` | All confirmed transactions |
| `BudgetCategories` | Expense category definitions |
| `RevenueCategories` | Income category definitions |
| `MerchantCache` | Learned description → category mappings |
| `ClosedMonths` | Locked month keys (`"yyyy-MM"`) |
| `BudgetDrafts` | Budget Planner drafts (expenses + revenue stored as JSON columns) |

The schema is created automatically on first startup via EF Core's `EnsureCreated()` plus explicit `CREATE TABLE IF NOT EXISTS` statements for tables added after the initial schema — no migrations to run.

**Dark mode preference** is the only thing stored in browser `localStorage` (key `fintool_darkmode`), since it is UI state that belongs to the browser, not the database.

> Because data lives in the SQLite file, it is device-specific but browser-independent. Back up or copy `family-finance.db` to migrate to another machine.

---

## API Reference (FinTool.Server)

`FinTool.Server` is a minimal ASP.NET Core app running on `http://localhost:5111`. All responses use PascalCase JSON.

### Transactions

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/transactions` | All transactions, ordered by date desc |
| `POST` | `/api/transactions/batch` | Import array; skips duplicates (same date + description + amount) |
| `PUT` | `/api/transactions/{id}` | Update category / flags on one transaction |
| `DELETE` | `/api/transactions/{id}` | Delete a transaction |
| `POST` | `/api/transactions/batch-delete` | Delete multiple transactions by id array |

### Categories

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/budget-categories` | All budget categories |
| `POST` | `/api/budget-categories` | Create or update (upsert by Id) |
| `DELETE` | `/api/budget-categories/{id}` | Delete a budget category |
| `GET` | `/api/revenue-categories` | All revenue categories |
| `POST` | `/api/revenue-categories` | Create or update (upsert by Id) |
| `DELETE` | `/api/revenue-categories/{id}` | Delete a revenue category |

### Budget Planner

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/budget-drafts` | All saved drafts (expenses and revenue deserialized) |
| `PUT` | `/api/budget-drafts` | Replace all drafts atomically |

### Merchant Cache & Closed Months

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/merchant-cache` | Return all cached mappings as a `{ description: category }` dictionary |
| `POST` | `/api/merchant-cache/lookup` | Look up cached category for a single description |
| `POST` | `/api/merchant-cache/set` | Store a description → category mapping |
| `GET` | `/api/closed-months` | List all locked month keys |
| `POST` | `/api/closed-months` | Lock a month |

### AI Classification

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/ping` | Health check — returns `{"status":"ok"}` |
| `POST` | `/api/classify` | Classify a batch of transaction descriptions |

**Classify request:**
```json
{
  "transactions": ["METRO PLUS ST-EUSTACHE", "DOLLARAMA 1234"],
  "categories": ["Groceries", "Restaurants", "Transport", "Entertainment"]
}
```

**Classify response:**
```json
[
  {"index": 1, "category": "Groceries"},
  {"index": 2, "category": "Entertainment"}
]
```

Internally, the server builds a prompt and pipes it to `claude -p` via `Process.Start`. The response is parsed for a JSON array; any surrounding text Claude adds is stripped. The call times out after 120 seconds.

---

## AccèsD Parser

`DesjardinsParser` supports three paste formats and auto-detects which one is being used:

| Format | Detection | Notes |
|---|---|---|
| Standard credit card | Default | Tab-separated, one line per transaction |
| BONIDOLLARS credit card | Paste contains `"BONIDOLLARS"` | Multi-line blocks with Desjardins category + merchant name |
| Debit account | Paste contains `"Solde"` | Multi-line blocks with category, optional sub-description, and balance |

**Amount sign convention used throughout the app:**
- `+` (positive) = expense / money out
- `−` (negative) = income, refund, or payment

French month abbreviations are supported (jan, janv, fév, mar, avr, mai, juin, juil, août, sep, oct, nov, déc).

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend framework | [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) (.NET 8) |
| UI component library | [MudBlazor](https://mudblazor.com/) 7.15.0 |
| Heading font | [Playfair Display](https://fonts.google.com/specimen/Playfair+Display) |
| Body font | [Inter](https://fonts.google.com/specimen/Inter) |
| AI | [Claude Code CLI](https://claude.ai/code) (local, via `FinTool.Server`) |
| Database | SQLite via [EF Core 8](https://learn.microsoft.com/ef/core/) |
| Backend | ASP.NET Core minimal API (.NET 8) |
