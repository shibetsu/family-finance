# Family Finance

A personal finance tracker built for Desjardins (AccèsD) account holders. Paste your transaction history directly from the AccèsD web portal, let a local Claude AI classify them into your budget categories, then review monthly spending through an interactive dashboard.

All data is stored in your browser's `localStorage` — no account, no cloud, no database.

---

## Features

### Dashboard
- **Summary cards** — monthly budget, total spent, remaining budget, transaction count, revenue, and net balance
- **Spending by Category** — donut chart; hover a slice to see the category total
- **6-Month Trend** — line chart showing expenses and revenue over the last six months
- **Expense & Revenue breakdowns** — per-category tables with budget progress bars

### Transactions
- **Import from AccèsD** — paste the transaction table from the AccèsD portal; the parser auto-detects the format (standard credit card, BONIDOLLARS credit card, or debit account)
- **AI classification** — a local Claude server classifies each expense into your budget categories automatically; previously seen merchants are cached and applied instantly
- **Inline category editing** — change the category on any confirmed transaction from the table
- **Month filter** — navigate month by month with prev/next arrows
- **Category filter** — multi-select dropdown to show only certain categories
- **Date sort** — click the Date column header; defaults to newest first
- **Close Month** — lock a past month to prevent any further edits; useful when you later rename categories or adjust budgets without affecting historical records

### Budget Categories
- Create and manage expense categories (name, monthly budget amount, color)
- Mark a category as ignored to exclude it from dashboard totals

### Revenue Categories
- Create and manage income categories (name, color)
- Mark a category as ignored to exclude it from dashboard totals

---

## Architecture

```
family-finance/
├── FinTool/                    # Blazor WebAssembly app
│   ├── Components/
│   │   ├── DashboardTab.razor
│   │   ├── TransactionsTab.razor
│   │   ├── ImportDialog.razor  # Import + pending review modal
│   │   ├── BudgetTab.razor
│   │   ├── RevenueTab.razor
│   │   ├── CategoryDialog.razor
│   │   └── RevenueCategoryDialog.razor
│   ├── Layout/
│   │   └── MainLayout.razor    # App shell, drawer, MudBlazor theme
│   ├── Models/
│   │   ├── Transaction.cs
│   │   ├── BudgetCategory.cs
│   │   └── RevenueCategory.cs
│   ├── Services/
│   │   ├── LocalStorageService.cs      # JS interop for localStorage
│   │   ├── TransactionService.cs       # Transaction CRUD
│   │   ├── BudgetService.cs            # Budget category CRUD
│   │   ├── RevenueCategoryService.cs   # Revenue category CRUD
│   │   ├── MerchantCacheService.cs     # Description → category memory
│   │   ├── ClosedMonthService.cs       # Month lock tracking
│   │   ├── ClaudeService.cs            # HTTP client for local AI server
│   │   └── DesjardinsParser.cs         # AccèsD paste parser
│   └── wwwroot/                        # Static assets, favicon, manifest
│
└── FinTool.Server/             # Minimal ASP.NET Core API (AI bridge)
    └── Program.cs              # /api/ping + /api/classify endpoints
```

### Data flow

```
AccèsD paste
    └─► DesjardinsParser          (parse into Transaction objects)
            └─► MerchantCacheService   (check previously learned categories)
            └─► ClaudeService          (classify uncached transactions via AI)
                    └─► FinTool.Server     (shells out to `claude -p`)
            └─► ImportDialog        (user reviews & confirms)
                    └─► TransactionService (persist to localStorage)
```

---

## Getting Started

### Prerequisites

| Requirement | Notes |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download) | Required to build and run both projects |
| [Claude Code CLI](https://claude.ai/code) | Required for AI classification (`claude` must be on your PATH) |

> **AI classification is optional.** If `FinTool.Server` is not running, the import flow still works — transactions just won't be auto-classified and you assign categories manually during the review step.

### Running the app

The easiest way is to use the included PowerShell launcher, which opens both processes in separate terminal windows:

```powershell
.\start.ps1
```

Then open [http://localhost:5254](http://localhost:5254) in your browser.

**Or start each project manually:**

```powershell
# Terminal 1 — AI classification server (optional)
dotnet run --project FinTool.Server

# Terminal 2 — Blazor app (with hot reload)
dotnet watch --project FinTool --launch-profile http
```

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
5. Review the suggested categories in the table. Adjust any that are wrong, toggle income transactions as Revenue if applicable.
6. Click **Confirm All** to save. The modal closes and the transactions appear in the table.

### 3. Edit categories later

Open the **Transactions** tab, navigate to the relevant month, and use the dropdown in each row's Category column to reassign it. Changes are saved immediately.

### 4. Close a month

Once a month is fully reviewed, click **Close Month** in the header. This locks all transactions in the currently viewed month — category dropdowns become read-only and the delete button is hidden. Locked months display a 🔒 icon next to the month name.

This protects historical data when you later rename categories or adjust budget amounts.

---

## Data Storage

Everything is stored in **browser `localStorage`** under these keys:

| Key | Contents |
|---|---|
| `fintool_transactions` | All confirmed transactions |
| `fintool_budget_categories` | Budget category definitions |
| `fintool_revenue_categories` | Revenue category definitions |
| `fintool_merchant_cache` | Learned description → category mappings |
| `fintool_closed_months` | List of locked month keys (`"yyyy-MM"`) |
| `fintool_darkmode` | Dark mode preference (`true`/`false`) |

> Data is browser- and device-specific. To migrate to another browser or device, use the browser's DevTools to export and re-import the `localStorage` values.

---

## AI Classification (FinTool.Server)

`FinTool.Server` is a minimal ASP.NET Core app that acts as a bridge between the Blazor frontend and the locally installed Claude CLI.

**Endpoints:**

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/ping` | Health check — returns `{"status":"ok"}` |
| `POST` | `/api/classify` | Classifies a batch of transaction descriptions |

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
| Data persistence | Browser `localStorage` (JS interop) |
| Backend (AI bridge) | ASP.NET Core minimal API (.NET 8) |
