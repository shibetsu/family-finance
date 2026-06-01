using FinTool.Models;

namespace FinTool.Services;

public class BudgetService(LocalStorageService storage)
{
    private const string Key = "fintool_budget_categories";
    private List<BudgetCategory>? _cache;

    public async Task<List<BudgetCategory>> GetCategoriesAsync()
    {
        _cache ??= await storage.GetAsync<List<BudgetCategory>>(Key) ?? [];
        return _cache;
    }

    public async Task SaveAsync(BudgetCategory category)
    {
        var list = await GetCategoriesAsync();
        var existing = list.FirstOrDefault(c => c.Id == category.Id);
        if (existing is null)
            list.Add(category);
        else
        {
            existing.Name = category.Name;
            existing.MonthlyAmount = category.MonthlyAmount;
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
