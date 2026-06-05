// ---------------------------------------------------------------------------
// Database path  (%LocalAppData%\FamilyFinance\family-finance.db)
// ---------------------------------------------------------------------------
var dbDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyFinance");
var dbPath = Path.Combine(dbDir, "family-finance.db");
Directory.CreateDirectory(dbDir);

// ---------------------------------------------------------------------------
// JWT
// ---------------------------------------------------------------------------
const string JwtSecret = "FamilyFinanceLocalSecretKey2024$NotForProduction";
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));

// ---------------------------------------------------------------------------
// Builder
// ---------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = jwtKey
        };
    });
builder.Services.AddAuthorization(o =>
    o.AddPolicy("OwnerOnly", p => p.RequireRole("Owner")));

// PascalCase responses to match Blazor model property names;
// case-insensitive reads so PostAsJsonAsync (camelCase) also binds correctly
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy        = null;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();
app.UseCors();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// Schema initialization (idempotent — runs on every startup)
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS Users (
            Id           TEXT NOT NULL PRIMARY KEY,
            Username     TEXT NOT NULL UNIQUE,
            PasswordHash TEXT NOT NULL,
            Salt         TEXT NOT NULL
        )");
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Email TEXT NOT NULL DEFAULT ''"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN DisplayName TEXT NOT NULL DEFAULT ''"); }
    catch { /* column already exists */ }
    // Existing rows get 'Owner' so the seeded Admin keeps full access
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'Owner'"); }
    catch { /* column already exists */ }
    if (!db.Users.Any())
    {
        var (hash, salt) = AuthEndpoints.HashPassword("Admin123!");
        db.Users.Add(new UserEntity { Id = Guid.NewGuid(), Username = "Admin", PasswordHash = hash, Salt = salt });
        db.SaveChanges();
    }
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints(jwtKey);

var api = app.MapGroup("").RequireAuthorization();
api.MapTransactionEndpoints();
api.MapBudgetEndpoints();
api.MapAccountEndpoints();
api.MapMiscEndpoints();
api.MapAiEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
