using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using FinTool;
using FinTool.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// TokenHolder is a singleton; AuthHeaderHandler reads it synchronously on every request.
// IHttpClientFactory sets up the handler chain correctly for WASM's Fetch backend.
builder.Services.AddSingleton<TokenHolder>();
builder.Services.AddTransient<AuthHeaderHandler>();
builder.Services.AddHttpClient("API", c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<AuthHeaderHandler>();
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

// Auth
builder.Services.AddScoped<AppAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AppAuthStateProvider>());
builder.Services.AddAuthorizationCore(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddMudServices();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<AuthService>();
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
builder.Services.AddScoped<AppInfoService>();

await builder.Build().RunAsync();
