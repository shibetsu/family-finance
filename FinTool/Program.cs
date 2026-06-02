using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using FinTool;
using FinTool.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// All data services and the Claude service talk to FinTool.Server on port 5111
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost:5111/") });

builder.Services.AddMudServices();
builder.Services.AddScoped<LocalStorageService>();  // kept for dark-mode preference only
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<MerchantCacheService>();
builder.Services.AddScoped<RevenueCategoryService>();
builder.Services.AddScoped<ClaudeService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<RecurringService>();
builder.Services.AddScoped<ClosedMonthService>();
builder.Services.AddScoped<TransactionFilterState>();
builder.Services.AddScoped<MonthState>();
builder.Services.AddScoped<BudgetPlannerService>();
builder.Services.AddScoped<GoalService>();

await builder.Build().RunAsync();
