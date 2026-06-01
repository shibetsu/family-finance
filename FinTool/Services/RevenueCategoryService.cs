using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class RevenueCategoryService(HttpClient http)
{
    private List<RevenueCategory>? _cache;

    public async Task<List<RevenueCategory>> GetCategoriesAsync()
    {
        _cache ??= await http.GetFromJsonAsync<List<RevenueCategory>>("api/revenue-categories") ?? [];
        return _cache;
    }

    public async Task SaveAsync(RevenueCategory category)
    {
        await http.PostAsJsonAsync("api/revenue-categories", category);
        _cache = null;
    }

    public async Task DeleteAsync(Guid id)
    {
        await http.DeleteAsync($"api/revenue-categories/{id}");
        _cache = null;
    }
}
