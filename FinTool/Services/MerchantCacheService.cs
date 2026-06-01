using System.Net.Http.Json;

namespace FinTool.Services;

public class MerchantCacheService(HttpClient http)
{
    public async Task<string?> GetCategoryAsync(string description)
    {
        var resp = await http.PostAsJsonAsync("api/merchant-cache/lookup",
            new { Description = description });
        var result = await resp.Content.ReadFromJsonAsync<CategoryResult>();
        return result?.Category;
    }

    public async Task SetCategoryAsync(string description, string category) =>
        await http.PostAsJsonAsync("api/merchant-cache/set",
            new { Description = description, Category = category });

    private record CategoryResult(string? Category);
}
