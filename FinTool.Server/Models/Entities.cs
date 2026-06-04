// EF Core entities  (property names match FinTool.Models exactly)
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

class UserEntity
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public string Username     { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt         { get; set; } = "";
    public string Email        { get; set; } = "";
    public string DisplayName  { get; set; } = "";
    public string Role         { get; set; } = "User";
}

record DraftRowDto(string Name, string Color, decimal Amount);
record BudgetDraftDto(Guid Id, string Name, DraftRowDto[] Expenses, DraftRowDto[] Revenue);

// Request / response records
record LookupRequest(string Description);
record SetCacheRequest(string Description, string Category);
record CloseMonthRequest(string MonthKey);
record LoginRequest(string Username, string Password);
record RegisterRequest(string Username, string Password, string? Email, string? DisplayName, string? Role);
record UpdateUserRequest(string? Username, string? Email, string? DisplayName, string? Role, string? Password);

record ClassifyRequest(
    [property: JsonPropertyName("transactions")] string[] Transactions,
    [property: JsonPropertyName("categories")]   string[] Categories);

record ClassifyResult(
    [property: JsonPropertyName("index")]    int    Index,
    [property: JsonPropertyName("category")] string Category);

record ChatTurn(string Role, string Content);
record ChatRequest(string Month, string Message, ChatTurn[] History);
record BudgetChatRequest(string Message, ChatTurn[] History);
