# Family Finance

A personal finance tracker built for Desjardins (AccèsD) account holders. Paste your transaction history directly from the AccèsD web portal, let a local Claude AI classify them into your budget categories, then review monthly spending through an interactive dashboard.

Data is stored in a local **SQLite database** — no account, no cloud, no third-party service.

---

## Features

### Authentication
- JWT-based login protects all pages and API endpoints
- Default seed account: **Admin / Admin123!** (change on first run)
- Token is stored in browser `sessionStorage` and sent as a Bearer header on every API request
- All app pages carry `[Authorize]` — the Blazor router gate prevents any component from rendering (and making API calls) until the auth state is confirmed; unauthenticated visitors are redirected to `/login` with no flash and no 401 errors
- App version is shown bottom-left on the login screen and above the username row in the nav drawer; sourced from `version.txt` at startup

### Global Month Picker
- The month selector lives in the **top app bar** and is shared across all pages — changing it on one page changes it everywhere
- Prev/next chevrons navigate month by month; the forward arrow is disabled on the current month
- The selected month is reflected in the URL (`?month=yyyy-MM`) for deep linking and browser back/forward support

### Dashboard
- **Summary cards** — monthly budget, total spent, remaining budget, transaction count, revenue, and net balance
- **Spending by Category** — donut chart; click a slice (or the center label) to jump to that category's transactions for the current month
- **6-Month Trend** — line chart showing expenses and revenue over the last six months
- **Expense & Revenue breakdowns** — per-category tables with budget progress bars, transaction counts, and per-transaction averages; category names are clickable and navigate to the Transactions page pre-filtered to that category and month
- **Ignored transactions** — a collapsible panel lists transactions belonging to ignored categories for the selected month

### Transactions
- **Import from AccèsD** — paste the transaction table from the AccèsD portal; the parser auto-detects the format (standard credit card, BONIDOLLARS credit card, or debit account)
- **AI classification** — a local Claude server classifies each expense into your budget categories automatically; previously seen merchants are cached and applied instantly
- **Revenue auto-detection** — transactions where money comes in are automatically treated as revenue; choose the revenue category in the import review table
- **Inline editing** — assign category, account, and savings goal directly from dropdown selectors in each row; category assignments update the merchant cache for future imports
- **Goal tagging** — link any transaction to a savings goal; the goal's progress is computed from all tagged transactions automatically
- **Mass delete** — select multiple transactions with checkboxes and delete them in one action
- **Category & account filters** — funnel icons in column headers open checkbox popovers; active filters persist when navigating away and back
- **Sort** — click Date, Category, or Amount column headers to sort
- **Close Month** — lock a past month to prevent any further edits; locked months show a 🔒 icon and all dropdowns become read-only

### Recurring
- Detects transactions that appear in two or more months (same description) and surfaces them as recurring items
- Shows average amount, last amount, last month seen, number of occurrences, and status (active / paused / cancelled)
- Useful for spotting subscriptions and regular bills without manual tagging

### Planner
- **Multiple drafts** — create and name as many budget scenarios as you want; drafts persist in the database and are never merged with live categories
- **Import from history** — detects up to 3 contiguous past months with data and offers to seed a draft from averaged actual spending (preserves real category colors)
- **Inline editing** — edit category names, amounts, and colors directly in the table; historical average is shown alongside the draft amount with color-coded difference tooltips
- **Apply to Settings** — promote a draft to your live budget in one click; replaces all current budget and revenue categories with the draft's rows after confirmation
- **Live summary** — Monthly Budget, Monthly Revenue, and Net cards update as you type

### Goals
- Define savings targets with a name, target amount, optional starting balance, and color
- **Progress bar** — color-coded to each goal's chosen color; shows current saved amount vs. target and percentage complete
- **Transaction-based progress** — tag any transaction to a goal from the Transactions page; the goal's saved amount is computed as starting balance + sum of all tagged transaction amounts
- **Projection** — shows estimated months to reach the goal based on average net monthly savings from the last 1–3 months of confirmed transactions
- "Achieved!" chip appears once the goal is reached

### Accounts
- Define bank accounts and credit cards with a name, type (credit card / debit / savings), and accent color
- Accounts appear as colored chips in the Transactions table and can be assigned to transactions inline via dropdown

### Settings
- Manage **budget categories** (name, monthly amount, color, optional ignore flag) and **revenue categories** (name, color, optional ignore flag) side by side in a two-column layout
- Categories marked as ignored are excluded from dashboard totals and charts
- Color changes take effect immediately across charts, breakdowns, and the Planner

### Users *(Owner only)*
- The **Owner** role can add, edit, and delete user accounts from the Users page (`/users`)
- Each user has a username, optional display name, optional email, and a role (Owner or Member)
- Owners cannot delete their own account

---

## Architecture

```
family-finance/
├── FinTool/                          # Blazor WebAssembly app (served by FinTool.Server)
│   ├── Components/
│   │   ├── ColorPicker.razor         # Swatch row + native picker fallback (used in dialogs)
│   │   ├── InlineColorPicker.razor   # Compact circle that opens floating swatch panel (used in tables)
│   │   ├── CategoryDialog.razor / RevenueCategoryDialog.razor / GoalDialog.razor
│   │   ├── AddUserDialog.razor / EditUserDialog.razor
│   │   ├── BudgetTab.razor / RevenueTab.razor
│   │   ├── TransactionsTab.razor + TransactionsTab.razor.cs   # code-behind pattern
│   │   ├── DashboardTab.razor + DashboardTab.razor.cs         # code-behind pattern
│   │   ├── ImportDialog.razor
│   │   └── ChatBot.razor
│   ├── Layout/
│   │   ├── MainLayout.razor          # App shell, global month picker, drawer, theme, logout button
│   │   └── LoginLayout.razor         # Minimal centered layout for the login page
│   ├── Models/
│   │   ├── Transaction.cs            # Amount: positive=expense, negative=income; GoalId nullable
│   │   ├── BudgetCategory.cs / RevenueCategory.cs
│   │   ├── BudgetDraft.cs            # Draft + DraftRow for Planner
│   │   ├── Goal.cs
│   │   └── Account.cs
│   ├── Pages/
│   │   ├── Login.razor               # /login — public, uses LoginLayout
│   │   ├── Dashboard.razor / Transactions.razor / Recurring.razor
│   │   ├── BudgetPlanner.razor + BudgetPlanner.razor.cs       # code-behind pattern
│   │   ├── Goals.razor               # /goals
│   │   ├── Accounts.razor            # /accounts
│   │   ├── Settings.razor            # /settings — embeds BudgetTab + RevenueTab side by side
│   │   └── Users.razor               # /users — Owner only
│   ├── Services/
│   │   ├── AuthService.cs            # Login, token storage, user CRUD
│   │   ├── AuthHeaderHandler.cs      # DelegatingHandler — injects Bearer token on every request
│   │   ├── MonthState.cs             # Global selected month — shared across all pages via OnChange event
│   │   ├── TransactionFilterState.cs # Persists category filter across navigation
│   │   ├── TransactionService.cs / BudgetService.cs / RevenueCategoryService.cs
│   │   ├── GoalService.cs / AccountService.cs / RecurringService.cs
│   │   ├── BudgetPlannerService.cs / ClosedMonthService.cs / MerchantCacheService.cs
│   │   ├── ClaudeService.cs / ChatService.cs
│   │   ├── DesjardinsParser.cs       # AccèsD paste format auto-detection and parsing
│   │   └── LocalStorageService.cs    # JS interop — dark mode preference only
│   ├── ColorPalette.cs               # 10-color soft palette + Random() helper
│   └── NavIcons.cs                   # SVG letter-in-box icons for navigation
│
└── FinTool.Server/                   # ASP.NET Core minimal API (port 5111)
    ├── Program.cs                    # Startup, config, schema init, endpoint registration
    ├── GlobalUsings.cs               # Project-wide global usings
    ├── Data/
    │   └── AppDbContext.cs           # EF Core DbContext
    ├── Models/
    │   └── Entities.cs               # All entity classes and request/response records
    └── Endpoints/
        ├── AuthEndpoints.cs          # Auth routes + JWT/password helpers
        ├── TransactionEndpoints.cs   # Transaction CRUD
        ├── BudgetEndpoints.cs        # Budget/revenue categories and drafts
        ├── AccountEndpoints.cs       # Accounts and goals
        ├── MiscEndpoints.cs          # Merchant cache, closed months, recurring
        └── AiEndpoints.cs            # /api/classify, /api/chat, RunClaudeAsync
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

### Running from source (development)

The server now hosts the Blazor frontend — only one process is needed:

```powershell
dotnet watch --project FinTool.Server --launch-profile http
```

Then open [http://localhost:5111](http://localhost:5111) in your browser.

**First run:** the SQLite database is created automatically — no migration commands needed.

**Fresh clone:** run `dotnet restore` once before the above if packages haven't been downloaded yet.

**Default credentials:** `Admin` / `Admin123!` — change the password after the first login via the Users page.

---

## Deployment

Pre-built self-contained packages (no .NET installation required on the target machine) can be produced with the included scripts.

The version is tracked automatically in `version.txt` and the patch number is incremented on every run. Pass an explicit version to override (e.g. for a minor or major bump):

```powershell
# Windows PowerShell — auto-increments patch version
.\publish.ps1

# Override to a specific version
.\publish.ps1 -Version 2.0.0
```

```bash
# From the repo root on Linux/macOS — auto-increments patch version
./publish.sh

# Override to a specific version
./publish.sh 2.0.0
```

Each run produces a `release/` folder containing:

| File | Target |
|---|---|
| `family-finance-<ver>-win-x64.zip` | Windows x64 |
| `family-finance-<ver>-linux-x64.tar.gz` | Linux x64 — preserves execute permissions |
| `family-finance-<ver>-linux-arm64.tar.gz` | Linux ARM64 / Raspberry Pi |

Every package is self-contained: no .NET runtime needed on the target machine. Each contains a single executable and a `wwwroot/` folder with the Blazor WebAssembly runtime.

### Windows

1. Unzip `family-finance-*-win-x64.zip` to any folder (e.g. `C:\FamilyFinance`).
2. Double-click `FinTool.Server.exe` — or run it from a terminal:
   ```cmd
   FinTool.Server.exe
   ```
3. Open [http://localhost:5111](http://localhost:5111) in your browser.

The database is created automatically at `%LocalAppData%\FamilyFinance\family-finance.db` on first run.

To run on a different port:
```cmd
set ASPNETCORE_URLS=http://0.0.0.0:8080
FinTool.Server.exe
```

### Linux

Use the **`.tar.gz`** package — it preserves the executable bit so no `chmod` is needed:

1. Copy the archive to your machine and extract it:
   ```bash
   tar xzf family-finance-*-linux-arm64.tar.gz -C family-finance
   cd family-finance
   ```
   *(substitute `linux-x64` if you are not on ARM64)*
2. Run the server:
   ```bash
   ./FinTool.Server
   ```
3. Open `http://<your-machine-ip>:5111` in any browser on your network.

The database is created automatically at `~/.local/share/FamilyFinance/family-finance.db` on first run.

#### Setting up Claude AI (optional)

The AI classification and chat features require the [Claude Code CLI](https://claude.ai/code) to be installed and authenticated on the machine running the server.

1. **Install Claude Code** following the instructions at <https://claude.ai/code>.
   On Raspberry Pi / Linux ARM64 the typical install is via `npm`:
   ```bash
   npm install -g @anthropic-ai/claude-code
   ```
2. **Authenticate** by running Claude once interactively — this stores credentials in `~/.config/@anthropic-ai/claude-code/` so the server can use them without a browser:
   ```bash
   claude
   ```
   Follow the login prompt. Once complete, verify it works in non-interactive mode:
   ```bash
   echo "Say hello" | claude -p
   ```
3. **Ensure `claude` is on the PATH** seen by the server process. If you installed via `npm` into a user-local prefix (e.g. `~/.npm-global/bin`), add it to your shell profile:
   ```bash
   echo 'export PATH="$HOME/.npm-global/bin:$PATH"' >> ~/.profile
   source ~/.profile
   ```
   Then restart the server so it inherits the updated PATH.

If Claude is not installed the server still starts normally — all data features work, and the AI buttons will return an error message instead of a response.

#### Run as a systemd service (keep alive after logout)

Create `/etc/systemd/system/family-finance.service`:

```ini
[Unit]
Description=Family Finance App
After=network.target

[Service]
WorkingDirectory=/opt/family-finance
ExecStart=/opt/family-finance/FinTool.Server
Restart=always
User=your-username
Environment=ASPNETCORE_ENVIRONMENT=Production
# Uncomment and set if claude is installed in a non-standard location:
# Environment=PATH=/home/your-username/.npm-global/bin:/usr/local/bin:/usr/bin:/bin

[Install]
WantedBy=multi-user.target
```

Then enable and start it:

```bash
sudo cp -r family-finance/ /opt/family-finance
sudo systemctl daemon-reload
sudo systemctl enable family-finance
sudo systemctl start family-finance
```

To run on a different port, add `Environment=ASPNETCORE_URLS=http://0.0.0.0:8080` to the `[Service]` block.

---

## How to Use

### 1. Sign in

Open the app and sign in with the default credentials (`Admin` / `Admin123!`). You can add more users and change passwords from the **Users** page once logged in.

### 2. Set up categories

Go to **Settings** and create your expense categories (name, monthly budget, color) and income categories. Mark anything you don't want affecting your totals (transfers, savings moves) as ignored.

### 3. Import transactions

1. Log into [AccèsD](https://accesd.desjardins.com), navigate to your account's transaction history, and **select all and copy** the table.
2. In Family Finance, click **Import** (top right of the Transactions page).
3. Paste into the text area. The format is auto-detected (credit card, BONIDOLLARS card, or debit account).
4. Click **Import & Classify** — the app checks the merchant cache, then sends uncached expenses to the local Claude server for classification.
5. Review the suggested categories. Adjust any that are wrong. Income transactions are automatically flagged as revenue.
6. Click **Confirm All** to save.

### 4. Tag accounts and goals

After importing, use the **Account** and **Goal** dropdowns directly in each transaction row to assign which account the transaction belongs to and whether it contributes to a savings goal.

### 5. Close a month

Once a month is fully reviewed, click **Close Month** in the Transactions header. This locks all transactions for that month — dropdowns become read-only and the delete button is hidden. Useful to protect historical data when you later rename categories.

### 6. Plan future budgets

Open **Planner** from the navigation. Click **Import history** to seed a draft from your actual recent spending, or **New Draft** to start from scratch. Once you're happy with a draft, click **Apply to Settings** to make it your live budget.

### 7. Track savings goals

Open **Goals**, add a goal with a target amount and optional starting balance. Then tag transactions to that goal from the Transactions page — progress updates automatically.

---

## Data Storage

All application data is persisted in a **SQLite database** managed by `FinTool.Server`.

**Database location:** `%LocalAppData%\FamilyFinance\family-finance.db`

| Table | Contents |
|---|---|
| `Transactions` | All confirmed transactions; `GoalId` (nullable) links to a savings goal |
| `BudgetCategories` | Expense category definitions with monthly budget amounts |
| `RevenueCategories` | Income category definitions |
| `Goals` | Savings targets; `CurrentAmount` is a manual starting balance; actual progress adds tagged transaction amounts |
| `Accounts` | Bank accounts and credit cards with accent colors |
| `BudgetDrafts` | Planner drafts; `ExpensesJson` and `RevenueJson` stored as JSON text columns |
| `MerchantCache` | Learned description → category mappings (PK is normalised uppercase description) |
| `ClosedMonths` | Locked month keys (`"yyyy-MM"`) |
| `Users` | Usernames, hashed passwords, display names, emails, and roles |

The schema is created automatically on first startup — no migrations to run. Columns added to existing tables use `ALTER TABLE … ADD COLUMN` wrapped in `try/catch` so they're applied once and ignored on subsequent starts.

**Dark mode preference** is the only thing stored in browser `localStorage` (key `fintool_darkmode`). The JWT session token is stored in `sessionStorage`.

> Back up or copy `family-finance.db` to migrate to another machine.

---

## API Reference (FinTool.Server)

`FinTool.Server` is a minimal ASP.NET Core app running on `http://localhost:5111`. All responses use PascalCase JSON. All endpoints except `/api/auth/login` and `/api/ping` require a valid Bearer token.

### Authentication

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/login` | Public | Exchange username + password for a JWT |
| `GET` | `/api/auth/validate` | Required | Check whether the current token is still valid |
| `GET` | `/api/auth/users` | Owner only | List all user accounts |
| `POST` | `/api/auth/register` | Owner only | Create a new user account |
| `PUT` | `/api/auth/users/{id}` | Required | Update own or (Owner) any user's profile |
| `DELETE` | `/api/auth/users/{id}` | Owner only | Delete a user account |

### Transactions

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/transactions` | All transactions, ordered by date desc |
| `POST` | `/api/transactions/batch` | Import array; skips duplicates (same date + description + amount) |
| `PUT` | `/api/transactions/{id}` | Update category, account, goal, and flags on one transaction |
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

### Goals & Accounts

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/goals` | All savings goals |
| `POST` | `/api/goals` | Create or update (upsert by Id) |
| `DELETE` | `/api/goals/{id}` | Delete a goal |
| `GET` | `/api/accounts` | All accounts, ordered by name |
| `POST` | `/api/accounts` | Create or update (upsert by Id) |
| `DELETE` | `/api/accounts/{id}` | Delete an account |

### Planner & Utility

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/budget-drafts` | All saved drafts |
| `PUT` | `/api/budget-drafts` | Replace all drafts atomically |
| `GET` | `/api/recurring` | Detected recurring transactions across all confirmed data |
| `GET` | `/api/merchant-cache` | All cached description → category mappings |
| `POST` | `/api/merchant-cache/lookup` | Look up cached category for one description |
| `POST` | `/api/merchant-cache/set` | Store a description → category mapping |
| `GET` | `/api/closed-months` | All locked month keys |
| `POST` | `/api/closed-months` | Lock a month |
| `GET` | `/api/ping` | Health check (public) |

### AI Classification & Chat

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/classify` | Classify a batch of transaction descriptions into budget categories |
| `POST` | `/api/chat` | Ask the AI assistant about a specific month's transactions |
| `POST` | `/api/chat/budget` | Guided budget planning conversation |

The server builds a prompt and pipes it to `claude -p` via `Process.Start`. Responses are parsed for structured output; the call times out after 120 seconds.

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
| Backend | ASP.NET Core minimal API (.NET 8) with JWT Bearer authentication |
