using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class GoalService(HttpClient http)
{
    private List<Goal>? _cache;

    public async Task<List<Goal>> GetAllAsync()
    {
        _cache ??= await http.GetFromJsonAsync<List<Goal>>("api/goals") ?? [];
        return _cache;
    }

    public async Task SaveAsync(Goal goal)
    {
        await http.PostAsJsonAsync("api/goals", goal);
        _cache = null;
    }

    public async Task DeleteAsync(Guid id)
    {
        await http.DeleteAsync($"api/goals/{id}");
        _cache = null;
    }
}
