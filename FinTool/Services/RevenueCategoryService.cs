using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class RevenueCategoryService(HttpClient http)
{
    public async Task<List<RevenueCategory>> GetCategoriesAsync() =>
        await http.GetFromJsonAsync<List<RevenueCategory>>("api/revenue-categories") ?? [];

    public async Task SaveAsync(RevenueCategory category) =>
        await http.PostAsJsonAsync("api/revenue-categories", category);

    public async Task DeleteAsync(Guid id) =>
        await http.DeleteAsync($"api/revenue-categories/{id}");
}
