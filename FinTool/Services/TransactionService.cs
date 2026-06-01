using FinTool.Models;

namespace FinTool.Services;

public class TransactionService(LocalStorageService storage)
{
    private const string Key = "fintool_transactions";
    private List<Transaction>? _cache;

    public async Task<List<Transaction>> GetAllAsync()
    {
        _cache ??= await storage.GetAsync<List<Transaction>>(Key) ?? [];
        return _cache;
    }

    // Skips duplicates: same date + description + amount already in store.
    public async Task<int> AddRangeAsync(IEnumerable<Transaction> incoming)
    {
        var all = await GetAllAsync();
        var added = 0;
        foreach (var t in incoming)
        {
            if (all.Any(x => x.Date == t.Date && x.Description == t.Description && x.Amount == t.Amount))
                continue;
            all.Add(t);
            added++;
        }
        if (added > 0)
            await storage.SetAsync(Key, _cache);
        return added;
    }

    public async Task UpdateAsync(Transaction updated)
    {
        var all = await GetAllAsync();
        var i = all.FindIndex(t => t.Id == updated.Id);
        if (i >= 0) all[i] = updated;
        await storage.SetAsync(Key, _cache);
    }

    public async Task DeleteAsync(Guid id)
    {
        var all = await GetAllAsync();
        all.RemoveAll(t => t.Id == id);
        await storage.SetAsync(Key, _cache);
    }
}
