using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class BudgetService(HttpClient http)
{
    private List<BudgetCategory>? _cache;

    public async Task<List<BudgetCategory>> GetCategoriesAsync()
    {
        _cache ??= await http.GetFromJsonAsync<List<BudgetCategory>>("api/budget-categories") ?? [];
        return _cache;
    }

    public async Task SaveAsync(BudgetCategory category)
    {
        await http.PostAsJsonAsync("api/budget-categories", category);
        _cache = null;
    }

    public async Task DeleteAsync(Guid id)
    {
        await http.DeleteAsync($"api/budget-categories/{id}");
        _cache = null;
    }
}
