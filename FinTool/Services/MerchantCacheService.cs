using System.Net.Http.Json;

namespace FinTool.Services;

public class MerchantCacheService(HttpClient http)
{
    private Dictionary<string, string>? _cache;

    private async Task<Dictionary<string, string>> LoadAsync()
    {
        _cache ??= await http.GetFromJsonAsync<Dictionary<string, string>>("api/merchant-cache") ?? [];
        return _cache;
    }

    public async Task<string?> GetCategoryAsync(string description)
    {
        var cache = await LoadAsync();
        return cache.TryGetValue(description.Trim().ToUpperInvariant(), out var cat) ? cat : null;
    }

    public async Task SetCategoryAsync(string description, string category)
    {
        var cache = await LoadAsync();
        var key   = description.Trim().ToUpperInvariant();
        cache[key] = category;
        await http.PostAsJsonAsync("api/merchant-cache/set",
            new { Description = description, Category = category });
    }
}
