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
// Anchor content root to the executable's directory so wwwroot is always found
// regardless of what working directory the process is launched from (systemd, scripts, etc.)
var executableDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory();

// Search upward for version.txt. In production executableDir == deploy dir (version.txt copied there
// by publish scripts). In dev AppContext.BaseDirectory is bin/Debug/net8.0/ — 3 levels up is repo root.
static string FindVersion(params string[] startDirs)
{
    foreach (var start in startDirs)
    {
        var dir = start;
        for (var i = 0; i < 5; i++)
        {
            var f = Path.Combine(dir, "version.txt");
            if (File.Exists(f)) return File.ReadAllText(f).Trim();
            var parent = Path.GetDirectoryName(dir);
            if (parent is null || parent == dir) break;
            dir = parent;
        }
    }
    return "dev";
}
var appVersion = FindVersion(AppContext.BaseDirectory, executableDir);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = executableDir,
    WebRootPath = Path.Combine(executableDir, "wwwroot")
});

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

builder.Services.AddScoped<EmailService>();

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
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS PasswordResetTokens (
            Id        TEXT NOT NULL PRIMARY KEY,
            UserId    TEXT NOT NULL,
            Token     TEXT NOT NULL UNIQUE,
            ExpiresAt TEXT NOT NULL,
            Used      INTEGER NOT NULL DEFAULT 0
        )");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS EmailConfig (
            Id          INTEGER NOT NULL PRIMARY KEY,
            SmtpHost    TEXT NOT NULL DEFAULT '',
            SmtpPort    INTEGER NOT NULL DEFAULT 587,
            Username    TEXT NOT NULL DEFAULT '',
            Password    TEXT NOT NULL DEFAULT '',
            FromAddress TEXT NOT NULL DEFAULT '',
            FromName    TEXT NOT NULL DEFAULT 'Family Finance',
            AppBaseUrl  TEXT NOT NULL DEFAULT 'http://localhost:5111'
        )");
    if (!db.EmailConfig.Any())
        db.EmailConfig.Add(new EmailConfigEntity { Id = 1 });
    db.SaveChanges();
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
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok", version = appVersion }));
app.MapAuthEndpoints(jwtKey);

var api = app.MapGroup("").RequireAuthorization();
api.MapTransactionEndpoints();
api.MapBudgetEndpoints();
api.MapAccountEndpoints();
api.MapMiscEndpoints();
api.MapAiEndpoints();
app.MapSettingsEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
