using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

// ---------------------------------------------------------------------------
// Database path  (%LocalAppData%\FamilyFinance\family-finance.db)
// ---------------------------------------------------------------------------
var dbDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyFinance");
var dbPath = Path.Combine(dbDir, "family-finance.db");
Directory.CreateDirectory(dbDir);

// ---------------------------------------------------------------------------
// Builder
// ---------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// PascalCase responses to match Blazor model property names;
// case-insensitive reads so PostAsJsonAsync (camelCase) also binds correctly
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy        = null;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();
app.UseCors();

// Ensure schema exists on every startup (idempotent)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    // EnsureCreated won't add new tables to an existing DB, so create any new ones explicitly
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS BudgetDrafts (
            Id           TEXT NOT NULL PRIMARY KEY,
            Name         TEXT NOT NULL DEFAULT '',
            ExpensesJson TEXT NOT NULL DEFAULT '[]',
            RevenueJson  TEXT NOT NULL DEFAULT '[]'
        )");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS Accounts (
            Id    TEXT NOT NULL PRIMARY KEY,
            Name  TEXT NOT NULL DEFAULT '',
            Type  TEXT NOT NULL DEFAULT 'credit',
            Color TEXT NOT NULL DEFAULT '#594AE2'
        )");
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Transactions ADD COLUMN AccountId TEXT"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Transactions ADD COLUMN GoalId TEXT"); }
    catch { /* column already exists */ }
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS Goals (
            Id            TEXT NOT NULL PRIMARY KEY,
            Name          TEXT NOT NULL DEFAULT '',
            Color         TEXT NOT NULL DEFAULT '#594AE2',
            TargetAmount  REAL NOT NULL DEFAULT 0,
            CurrentAmount REAL NOT NULL DEFAULT 0,
            Notes         TEXT NOT NULL DEFAULT ''
        )");
}

// ---------------------------------------------------------------------------
// Health
// ---------------------------------------------------------------------------
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

// ---------------------------------------------------------------------------
// Transactions
// ---------------------------------------------------------------------------
app.MapGet("/api/transactions", async (AppDbContext db) =>
    await db.Transactions.OrderByDescending(t => t.Date).ThenBy(t => t.Description).ToListAsync());

app.MapPost("/api/transactions/batch", async (TxEntity[] incoming, AppDbContext db) =>
{
    var added = 0;
    // Load minimal set for dup detection rather than hitting DB per row
    var existing = (await db.Transactions
        .Select(t => $"{t.Date}|{t.Description}|{t.Amount}")
        .ToListAsync())
        .ToHashSet();

    foreach (var tx in incoming)
    {
        if (existing.Contains($"{tx.Date}|{tx.Description}|{tx.Amount}")) continue;
        db.Transactions.Add(tx);
        added++;
    }
    if (added > 0) await db.SaveChangesAsync();
    return Results.Ok(new { added });
});

app.MapPut("/api/transactions/{id:guid}", async (Guid id, TxEntity updated, AppDbContext db) =>
{
    var tx = await db.Transactions.FindAsync(id);
    if (tx is null) return Results.NotFound();
    tx.Category    = updated.Category;
    tx.IsRevenue   = updated.IsRevenue;
    tx.IsConfirmed = updated.IsConfirmed;
    tx.AccountId   = updated.AccountId;
    tx.GoalId      = updated.GoalId;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/api/transactions/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var tx = await db.Transactions.FindAsync(id);
    if (tx is null) return Results.NotFound();
    db.Transactions.Remove(tx);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/transactions/batch-delete", async (Guid[] ids, AppDbContext db) =>
{
    await db.Transactions.Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync();
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// Budget categories
// ---------------------------------------------------------------------------
app.MapGet("/api/budget-categories", async (AppDbContext db) =>
    await db.BudgetCategories.ToListAsync());

app.MapPost("/api/budget-categories", async (BudgetCategoryEntity cat, AppDbContext db) =>
{
    var existing = await db.BudgetCategories.FindAsync(cat.Id);
    if (existing is null)
    {
        db.BudgetCategories.Add(cat);
    }
    else
    {
        existing.Name          = cat.Name;
        existing.MonthlyAmount = cat.MonthlyAmount;
        existing.Color         = cat.Color;
        existing.IsIgnored     = cat.IsIgnored;
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/api/budget-categories/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var cat = await db.BudgetCategories.FindAsync(id);
    if (cat is null) return Results.NotFound();
    db.BudgetCategories.Remove(cat);
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// Revenue categories
// ---------------------------------------------------------------------------
app.MapGet("/api/revenue-categories", async (AppDbContext db) =>
    await db.RevenueCategories.ToListAsync());

app.MapPost("/api/revenue-categories", async (RevenueCategoryEntity cat, AppDbContext db) =>
{
    var existing = await db.RevenueCategories.FindAsync(cat.Id);
    if (existing is null)
    {
        db.RevenueCategories.Add(cat);
    }
    else
    {
        existing.Name      = cat.Name;
        existing.Color     = cat.Color;
        existing.IsIgnored = cat.IsIgnored;
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/api/revenue-categories/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var cat = await db.RevenueCategories.FindAsync(id);
    if (cat is null) return Results.NotFound();
    db.RevenueCategories.Remove(cat);
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// Accounts
// ---------------------------------------------------------------------------
app.MapGet("/api/accounts", async (AppDbContext db) =>
    await db.Accounts.OrderBy(a => a.Name).ToListAsync());

app.MapPost("/api/accounts", async (AccountEntity account, AppDbContext db) =>
{
    var existing = await db.Accounts.FindAsync(account.Id);
    if (existing is null)
        db.Accounts.Add(account);
    else
    {
        existing.Name  = account.Name;
        existing.Type  = account.Type;
        existing.Color = account.Color;
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/api/accounts/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var acct = await db.Accounts.FindAsync(id);
    if (acct is null) return Results.NotFound();
    db.Accounts.Remove(acct);
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// Goals
// ---------------------------------------------------------------------------
app.MapGet("/api/goals", async (AppDbContext db) =>
    await db.Goals.OrderBy(g => g.Name).ToListAsync());

app.MapPost("/api/goals", async (GoalEntity goal, AppDbContext db) =>
{
    var existing = await db.Goals.FindAsync(goal.Id);
    if (existing is null)
        db.Goals.Add(goal);
    else
    {
        existing.Name          = goal.Name;
        existing.Color         = goal.Color;
        existing.TargetAmount  = goal.TargetAmount;
        existing.CurrentAmount = goal.CurrentAmount;
        existing.Notes         = goal.Notes;
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/api/goals/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var goal = await db.Goals.FindAsync(id);
    if (goal is null) return Results.NotFound();
    db.Goals.Remove(goal);
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// Merchant cache
// ---------------------------------------------------------------------------
app.MapGet("/api/merchant-cache", async (AppDbContext db) =>
    await db.MerchantCache.ToDictionaryAsync(m => m.Description, m => m.Category));

app.MapPost("/api/merchant-cache/lookup", async (LookupRequest req, AppDbContext db) =>
{
    var key   = req.Description.Trim().ToUpperInvariant();
    var entry = await db.MerchantCache.FindAsync(key);
    return entry is null ? Results.Ok(new { Category = (string?)null }) : Results.Ok(new { entry.Category });
});

app.MapPost("/api/merchant-cache/set", async (SetCacheRequest req, AppDbContext db) =>
{
    var key   = req.Description.Trim().ToUpperInvariant();
    var entry = await db.MerchantCache.FindAsync(key);
    if (entry is null)
        db.MerchantCache.Add(new MerchantEntry { Description = key, Category = req.Category });
    else
        entry.Category = req.Category;
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// Closed months
// ---------------------------------------------------------------------------
app.MapGet("/api/closed-months", async (AppDbContext db) =>
    await db.ClosedMonths.Select(m => m.MonthKey).ToListAsync());

app.MapPost("/api/closed-months", async (CloseMonthRequest req, AppDbContext db) =>
{
    if (!await db.ClosedMonths.AnyAsync(m => m.MonthKey == req.MonthKey))
    {
        db.ClosedMonths.Add(new ClosedMonthEntry { MonthKey = req.MonthKey });
        await db.SaveChangesAsync();
    }
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// Budget drafts
// ---------------------------------------------------------------------------
var draftJsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

app.MapGet("/api/budget-drafts", async (AppDbContext db) =>
    (await db.BudgetDrafts.ToListAsync()).Select(e => new
    {
        e.Id,
        e.Name,
        Expenses = JsonSerializer.Deserialize<object[]>(e.ExpensesJson, draftJsonOpts) ?? Array.Empty<object>(),
        Revenue  = JsonSerializer.Deserialize<object[]>(e.RevenueJson,  draftJsonOpts) ?? Array.Empty<object>()
    }));

app.MapPut("/api/budget-drafts", async (BudgetDraftDto[] incoming, AppDbContext db) =>
{
    db.BudgetDrafts.RemoveRange(await db.BudgetDrafts.ToListAsync());
    foreach (var d in incoming)
        db.BudgetDrafts.Add(new BudgetDraftEntity
        {
            Id           = d.Id,
            Name         = d.Name,
            ExpensesJson = JsonSerializer.Serialize(d.Expenses),
            RevenueJson  = JsonSerializer.Serialize(d.Revenue)
        });
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// AI classification
// ---------------------------------------------------------------------------
app.MapPost("/api/classify", async (ClassifyRequest req, CancellationToken ct) =>
{
    if (req.Transactions.Length == 0 || req.Categories.Length == 0)
        return Results.Ok(Array.Empty<ClassifyResult>());

    var categoryList = string.Join(", ", req.Categories);
    var txLines = req.Transactions
        .Select((t, i) => $"{i + 1}. {t}")
        .Aggregate((a, b) => $"{a}\n{b}");

    var prompt = $$"""
        Classify each financial transaction into exactly one of these budget categories: {{categoryList}}

        Transactions:
        {{txLines}}

        Reply with ONLY a valid JSON array — no explanation, no markdown fences:
        [{"index":1,"category":"CategoryName"}]
        """;

    var raw = await RunClaudeAsync(prompt, ct);
    if (raw is null)
        return Results.Problem("Claude process failed or timed out.");

    var start = raw.IndexOf('[');
    var end   = raw.LastIndexOf(']');
    if (start < 0 || end <= start)
        return Results.Ok(Array.Empty<ClassifyResult>());

    try
    {
        var opts    = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var results = JsonSerializer.Deserialize<ClassifyResult[]>(raw[start..(end + 1)], opts);
        return Results.Ok(results ?? []);
    }
    catch
    {
        return Results.Ok(Array.Empty<ClassifyResult>());
    }
});

// ---------------------------------------------------------------------------
// Recurring transactions
// ---------------------------------------------------------------------------
app.MapGet("/api/recurring", async (AppDbContext db) =>
{
    var allTx = await db.Transactions
        .Where(t => t.Amount != 0 && t.IsConfirmed)
        .ToListAsync();

    if (!allTx.Any()) return Results.Ok(Array.Empty<object>());

    var mostRecentMonth = allTx.Max(t => t.Date[..7]) ?? "";

    static int MonthDiff(string recent, string older)
    {
        var r = DateOnly.ParseExact(recent + "-01", "yyyy-MM-dd", null);
        var o = DateOnly.ParseExact(older  + "-01", "yyyy-MM-dd", null);
        return (r.Year - o.Year) * 12 + r.Month - o.Month;
    }

    var items = allTx
        .GroupBy(t => (t.Description, t.IsRevenue))
        .Where(g => g.Select(t => t.Date[..7]).Distinct().Count() >= 2)
        .Select(g =>
        {
            var months      = g.Select(t => t.Date[..7]).Distinct().OrderBy(x => x).ToList();
            var lastMonth   = months.Last();
            var ordered     = g.OrderByDescending(t => t.Date).ToList();
            var avgAmount   = Math.Round(Math.Abs(g.Average(t => t.Amount)), 2);
            var lastAmount  = Math.Round(Math.Abs(ordered.First().Amount), 2);
            var monthsAgo   = MonthDiff(mostRecentMonth, lastMonth);
            var status      = monthsAgo == 0 ? "active" : monthsAgo <= 3 ? "paused" : "cancelled";
            return new
            {
                Description = g.Key.Description,
                Category    = ordered.First().Category,
                IsRevenue   = g.Key.IsRevenue,
                AvgAmount   = avgAmount,
                LastAmount  = lastAmount,
                LastMonth   = lastMonth,
                Occurrences = months.Count,
                Status      = status
            };
        })
        .OrderByDescending(x => x.AvgAmount)
        .ToList();

    return Results.Ok(items);
});

// ---------------------------------------------------------------------------
// Chat assistant
// ---------------------------------------------------------------------------
app.MapPost("/api/chat", async (ChatRequest req, AppDbContext db, CancellationToken ct) =>
{
    var txs = await db.Transactions
        .Where(t => t.Date.StartsWith(req.Month + "-"))
        .OrderBy(t => t.Date).ThenBy(t => t.Description)
        .ToListAsync(ct);

    var budgetCats  = await db.BudgetCategories.OrderBy(c => c.Name).ToListAsync(ct);
    var revenueCats = await db.RevenueCategories.OrderBy(c => c.Name).ToListAsync(ct);

    var expenses = txs.Where(t => !t.IsRevenue && t.Amount > 0).ToList();
    var revenues = txs.Where(t =>  t.IsRevenue).ToList();

    var totalExpenses = expenses.Sum(t => t.Amount);
    var totalRevenue  = revenues.Sum(t => -t.Amount);
    var totalBudget   = budgetCats.Where(c => !c.IsIgnored).Sum(c => c.MonthlyAmount);

    var expByCat = expenses
        .GroupBy(t => t.Category ?? "Uncategorized")
        .Select(g => (Category: g.Key, Total: g.Sum(t => t.Amount), Count: g.Count()))
        .OrderByDescending(x => x.Total).ToList();

    var revByCat = revenues
        .GroupBy(t => t.Category ?? "Uncategorized")
        .Select(g => (Category: g.Key, Total: g.Sum(t => -t.Amount), Count: g.Count()))
        .OrderByDescending(x => x.Total).ToList();

    var monthDisplay = req.Month;
    if (DateTime.TryParseExact(req.Month + "-01", "yyyy-MM-dd",
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out var dt))
        monthDisplay = dt.ToString("MMMM yyyy");

    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"You are FinTool's personal finance assistant. The user is reviewing their finances for {monthDisplay}.");
    sb.AppendLine("Answer questions concisely and insightfully. Highlight trends, warnings, or savings opportunities when relevant.");
    sb.AppendLine("Format amounts as currency. Be conversational but precise. Keep responses under 150 words unless detail is specifically needed.");
    sb.AppendLine();
    sb.AppendLine("=== EXPENSE BUDGET CATEGORIES ===");
    foreach (var cat in budgetCats.Where(c => !c.IsIgnored))
    {
        var spent = expByCat.FirstOrDefault(x => x.Category == cat.Name).Total;
        var count = expByCat.FirstOrDefault(x => x.Category == cat.Name).Count;
        var pct   = cat.MonthlyAmount > 0 ? spent / cat.MonthlyAmount * 100 : 0;
        sb.AppendLine($"- {cat.Name}: budget ${cat.MonthlyAmount:F2}/mo | spent ${spent:F2} ({pct:F0}%) | {count} txns");
    }
    sb.AppendLine();
    sb.AppendLine("=== REVENUE ===");
    if (revByCat.Count > 0)
        foreach (var r in revByCat)
            sb.AppendLine($"- {r.Category}: ${r.Total:F2} ({r.Count} transactions)");
    else
        sb.AppendLine("No revenue recorded this month.");
    sb.AppendLine();
    sb.AppendLine("=== ALL TRANSACTIONS THIS MONTH ===");
    if (txs.Count > 0)
        foreach (var t in txs)
            sb.AppendLine($"- {t.Date} | {t.Description} | {(t.IsRevenue ? "+" : "-")}${Math.Abs(t.Amount):F2} | {t.Category ?? "Uncategorized"}");
    else
        sb.AppendLine("No transactions recorded this month.");
    sb.AppendLine();
    sb.AppendLine("=== MONTHLY SUMMARY ===");
    sb.AppendLine($"Total Expenses: ${totalExpenses:F2} / Budget: ${totalBudget:F2} ({(totalBudget > 0 ? totalExpenses / totalBudget * 100 : 0):F0}% used)");
    sb.AppendLine($"Total Revenue:  ${totalRevenue:F2}");
    sb.AppendLine($"Net Balance:    ${totalRevenue - totalExpenses:F2}");
    sb.AppendLine($"Transactions:   {txs.Count}");
    sb.AppendLine();

    if (req.History.Length > 0)
    {
        sb.AppendLine("=== CONVERSATION SO FAR ===");
        foreach (var msg in req.History)
            sb.AppendLine($"{(msg.Role == "user" ? "User" : "Assistant")}: {msg.Content}");
        sb.AppendLine();
    }

    sb.AppendLine($"User: {req.Message}");
    sb.AppendLine("Assistant:");

    var raw = await RunClaudeAsync(sb.ToString(), ct);
    if (raw is null) return Results.Problem("Claude is unavailable.");

    return Results.Ok(new { response = raw.Trim() });
});

// ---------------------------------------------------------------------------
// Budget planning assistant
// ---------------------------------------------------------------------------
app.MapPost("/api/chat/budget", async (BudgetChatRequest req, AppDbContext db, CancellationToken ct) =>
{
    var budgetCats  = await db.BudgetCategories.OrderBy(c => c.Name).ToListAsync(ct);
    var revenueCats = await db.RevenueCategories.OrderBy(c => c.Name).ToListAsync(ct);
    var allTx       = await db.Transactions.ToListAsync(ct);

    // Up to 3 contiguous months of history
    var today = DateOnly.FromDateTime(DateTime.Now);
    var histMonths = Enumerable.Range(1, 3)
        .Select(i => today.AddMonths(-i))
        .Where(m => allTx.Any(t => t.Date.StartsWith(m.ToString("yyyy-MM"))))
        .ToList();

    var histExpense = allTx
        .Where(t => !t.IsRevenue && t.Amount > 0 &&
                    histMonths.Any(m => t.Date.StartsWith(m.ToString("yyyy-MM"))))
        .GroupBy(t => t.Category ?? "Unclassified")
        .ToDictionary(g => g.Key,
            g => Math.Round(g.Sum(t => t.Amount) / Math.Max(1, histMonths.Count), 0));

    var histRevenue = allTx
        .Where(t => t.IsRevenue &&
                    histMonths.Any(m => t.Date.StartsWith(m.ToString("yyyy-MM"))))
        .GroupBy(t => t.Category ?? "Unclassified")
        .ToDictionary(g => g.Key,
            g => Math.Round(g.Sum(t => -t.Amount) / Math.Max(1, histMonths.Count), 0));

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("You are FinTool's budget planning assistant. You help the user build a new monthly budget draft through a friendly, guided conversation.");
    sb.AppendLine();
    sb.AppendLine("Your goal: gather the information needed to produce a complete monthly budget draft, then create it.");
    sb.AppendLine();
    sb.AppendLine("Typical flow:");
    sb.AppendLine("1. Ask if they want to base the draft on: (A) current categories, (B) spending trends, or (C) from scratch.");
    sb.AppendLine("2. Walk through expense categories — suggest amounts, ask if they want to add, remove, or adjust any.");
    sb.AppendLine("3. Walk through revenue — ask about income sources and amounts.");
    sb.AppendLine("4. Propose the full budget in a clear summary and ask for confirmation.");
    sb.AppendLine("5. Once the user confirms, output the draft in the exact format described below.");
    sb.AppendLine();
    sb.AppendLine("IMPORTANT — When the user confirms they are happy with the draft, end your response with:");
    sb.AppendLine("<<CREATE_DRAFT>>");
    sb.AppendLine("{\"name\":\"Draft Name\",\"expenses\":[{\"name\":\"Food\",\"amount\":500,\"color\":\"#594AE2\"}],\"revenue\":[{\"name\":\"Salary\",\"amount\":3000,\"color\":\"#4CAF50\"}]}");
    sb.AppendLine("<<END_DRAFT>>");
    sb.AppendLine("Use a single-line JSON object. Amounts are monthly figures. Use existing category colors when available.");
    sb.AppendLine();
    sb.AppendLine("Style rules:");
    sb.AppendLine("- Ask one or two questions at a time. Be concise and helpful.");
    sb.AppendLine("- Support markdown: **bold**, *italic*, bullet lists with '- '.");
    sb.AppendLine("- When suggesting a budget summary, show it as a bullet list with amounts.");
    sb.AppendLine();

    sb.AppendLine("=== CURRENT EXPENSE BUDGET CATEGORIES ===");
    var activeBudget = budgetCats.Where(c => !c.IsIgnored).ToList();
    if (activeBudget.Count > 0)
        foreach (var c in activeBudget)
            sb.AppendLine($"- {c.Name}: ${c.MonthlyAmount:F0}/mo (color: {c.Color})");
    else
        sb.AppendLine("(none defined)");

    sb.AppendLine();
    sb.AppendLine("=== CURRENT REVENUE CATEGORIES ===");
    var activeRev = revenueCats.Where(c => !c.IsIgnored).ToList();
    if (activeRev.Count > 0)
        foreach (var c in activeRev)
            sb.AppendLine($"- {c.Name} (color: {c.Color})");
    else
        sb.AppendLine("(none defined)");

    sb.AppendLine();
    var monthLabel = histMonths.Count > 0
        ? $"avg over {histMonths.Count} month{(histMonths.Count == 1 ? "" : "s")}"
        : "no historical data";
    sb.AppendLine($"=== SPENDING TRENDS ({monthLabel}) ===");
    if (histExpense.Count > 0)
        foreach (var kv in histExpense.OrderByDescending(x => x.Value))
            sb.AppendLine($"- {kv.Key}: ~${kv.Value:F0}/mo");
    else
        sb.AppendLine("(no data)");

    sb.AppendLine();
    sb.AppendLine("=== REVENUE TRENDS ===");
    if (histRevenue.Count > 0)
        foreach (var kv in histRevenue.OrderByDescending(x => x.Value))
            sb.AppendLine($"- {kv.Key}: ~${kv.Value:F0}/mo");
    else
        sb.AppendLine("(no data)");

    sb.AppendLine();
    if (req.History.Length > 0)
    {
        sb.AppendLine("=== CONVERSATION SO FAR ===");
        foreach (var msg in req.History)
            sb.AppendLine($"{(msg.Role == "user" ? "User" : "Assistant")}: {msg.Content}");
        sb.AppendLine();
    }

    sb.AppendLine($"User: {req.Message}");
    sb.AppendLine("Assistant:");

    var raw = await RunClaudeAsync(sb.ToString(), ct);
    if (raw is null) return Results.Problem("Claude is unavailable.");

    return Results.Ok(new { response = raw.Trim() });
});

app.Run("http://localhost:5111");

// ---------------------------------------------------------------------------
// Claude runner
// ---------------------------------------------------------------------------
static async Task<string?> RunClaudeAsync(string prompt, CancellationToken ct)
{
    var psi = new ProcessStartInfo
    {
        FileName               = OperatingSystem.IsWindows() ? "cmd.exe" : "claude",
        Arguments              = OperatingSystem.IsWindows() ? "/c claude -p" : "-p",
        RedirectStandardInput  = true,
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = true
    };

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(120));

    try
    {
        using var proc = Process.Start(psi);
        if (proc is null) return null;
        await proc.StandardInput.WriteAsync(prompt);
        proc.StandardInput.Close();
        var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
        await proc.WaitForExitAsync(cts.Token);
        return output;
    }
    catch { return null; }
}

// ---------------------------------------------------------------------------
// EF Core entities  (property names match FinTool.Models exactly)
// ---------------------------------------------------------------------------
class TxEntity
{
    public Guid    Id          { get; set; } = Guid.NewGuid();
    public string  Date        { get; set; } = "";   // stored as "yyyy-MM-dd"
    public string  Description { get; set; } = "";
    public decimal Amount      { get; set; }
    public string  AccountType { get; set; } = "credit";
    public string? Category    { get; set; }
    public bool    IsConfirmed { get; set; }
    public bool    IsRevenue   { get; set; }
    public Guid?   AccountId   { get; set; }
    public Guid?   GoalId      { get; set; }
}

class AccountEntity
{
    public Guid   Id    { get; set; } = Guid.NewGuid();
    public string Name  { get; set; } = "";
    public string Type  { get; set; } = "credit";
    public string Color { get; set; } = "#594AE2";
}

class BudgetCategoryEntity
{
    public Guid    Id            { get; set; } = Guid.NewGuid();
    public string  Name          { get; set; } = "";
    public decimal MonthlyAmount { get; set; }
    public string  Color         { get; set; } = "#594AE2";
    public bool    IsIgnored     { get; set; }
}

class RevenueCategoryEntity
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public string Name      { get; set; } = "";
    public string Color     { get; set; } = "#4CAF50";
    public bool   IsIgnored { get; set; }
}

class MerchantEntry
{
    public string Description { get; set; } = "";  // PK (normalised)
    public string Category    { get; set; } = "";
}

class ClosedMonthEntry
{
    public string MonthKey { get; set; } = "";     // PK, e.g. "2025-01"
}

class GoalEntity
{
    public Guid    Id            { get; set; } = Guid.NewGuid();
    public string  Name          { get; set; } = "";
    public string  Color         { get; set; } = "#594AE2";
    public decimal TargetAmount  { get; set; }
    public decimal CurrentAmount { get; set; }
    public string  Notes         { get; set; } = "";
}

class BudgetDraftEntity
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public string Name         { get; set; } = "";
    public string ExpensesJson { get; set; } = "[]";
    public string RevenueJson  { get; set; } = "[]";
}

record DraftRowDto(string Name, string Color, decimal Amount);
record BudgetDraftDto(Guid Id, string Name, DraftRowDto[] Expenses, DraftRowDto[] Revenue);

// ---------------------------------------------------------------------------
// DbContext
// ---------------------------------------------------------------------------
class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TxEntity>             Transactions     => Set<TxEntity>();
    public DbSet<BudgetCategoryEntity> BudgetCategories => Set<BudgetCategoryEntity>();
    public DbSet<RevenueCategoryEntity>RevenueCategories=> Set<RevenueCategoryEntity>();
    public DbSet<MerchantEntry>        MerchantCache    => Set<MerchantEntry>();
    public DbSet<ClosedMonthEntry>     ClosedMonths     => Set<ClosedMonthEntry>();
    public DbSet<BudgetDraftEntity>    BudgetDrafts     => Set<BudgetDraftEntity>();
    public DbSet<AccountEntity>        Accounts         => Set<AccountEntity>();
    public DbSet<GoalEntity>           Goals            => Set<GoalEntity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<TxEntity>().ToTable("Transactions");
        mb.Entity<BudgetCategoryEntity>().ToTable("BudgetCategories");
        mb.Entity<RevenueCategoryEntity>().ToTable("RevenueCategories");
        mb.Entity<MerchantEntry>().ToTable("MerchantCache").HasKey(m => m.Description);
        mb.Entity<ClosedMonthEntry>().ToTable("ClosedMonths").HasKey(c => c.MonthKey);
        mb.Entity<BudgetDraftEntity>().ToTable("BudgetDrafts");
        mb.Entity<AccountEntity>().ToTable("Accounts");
        mb.Entity<GoalEntity>().ToTable("Goals");
    }
}

// ---------------------------------------------------------------------------
// Request / response records
// ---------------------------------------------------------------------------
record LookupRequest(string Description);
record SetCacheRequest(string Description, string Category);
record CloseMonthRequest(string MonthKey);

record ClassifyRequest(
    [property: JsonPropertyName("transactions")] string[] Transactions,
    [property: JsonPropertyName("categories")]   string[] Categories);

record ClassifyResult(
    [property: JsonPropertyName("index")]    int    Index,
    [property: JsonPropertyName("category")] string Category);

record ChatTurn(string Role, string Content);
record ChatRequest(string Month, string Message, ChatTurn[] History);
record BudgetChatRequest(string Message, ChatTurn[] History);
