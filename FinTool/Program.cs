using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using FinTool;
using FinTool.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<MerchantCacheService>();
builder.Services.AddScoped<RevenueCategoryService>();
builder.Services.AddScoped<ClaudeService>();
builder.Services.AddScoped<ClosedMonthService>();

await builder.Build().RunAsync();
