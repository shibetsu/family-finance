using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class BudgetService(HttpClient http)
{
    public async Task<List<BudgetCategory>> GetCategoriesAsync() =>
        await http.GetFromJsonAsync<List<BudgetCategory>>("api/budget-categories") ?? [];

    public async Task SaveAsync(BudgetCategory category) =>
        await http.PostAsJsonAsync("api/budget-categories", category);

    public async Task DeleteAsync(Guid id) =>
        await http.DeleteAsync($"api/budget-categories/{id}");
}
