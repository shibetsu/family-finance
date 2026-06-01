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

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<TxEntity>().ToTable("Transactions");
        mb.Entity<BudgetCategoryEntity>().ToTable("BudgetCategories");
        mb.Entity<RevenueCategoryEntity>().ToTable("RevenueCategories");
        mb.Entity<MerchantEntry>().ToTable("MerchantCache").HasKey(m => m.Description);
        mb.Entity<ClosedMonthEntry>().ToTable("ClosedMonths").HasKey(c => c.MonthKey);
        mb.Entity<BudgetDraftEntity>().ToTable("BudgetDrafts");
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
