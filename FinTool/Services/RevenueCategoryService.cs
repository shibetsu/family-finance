using FinTool.Models;

namespace FinTool.Services;

public class RevenueCategoryService(LocalStorageService storage)
{
    private const string Key = "fintool_revenue_categories";
    private List<RevenueCategory>? _cache;

    public async Task<List<RevenueCategory>> GetCategoriesAsync()
    {
        _cache ??= await storage.GetAsync<List<RevenueCategory>>(Key) ?? [];
        return _cache;
    }

    public async Task SaveAsync(RevenueCategory category)
    {
        var list = await GetCategoriesAsync();
        var existing = list.FirstOrDefault(c => c.Id == category.Id);
        if (existing is null)
            list.Add(category);
        else
        {
            existing.Name = category.Name;
            existing.Color = category.Color;
            existing.IsIgnored = category.IsIgnored;
        }
        await storage.SetAsync(Key, _cache);
    }

    public async Task DeleteAsync(Guid id)
    {
        var list = await GetCategoriesAsync();
        list.RemoveAll(c => c.Id == id);
        await storage.SetAsync(Key, _cache);
    }
}
