using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class TransactionService(HttpClient http)
{
    private List<Transaction>? _cache;

    public async Task<List<Transaction>> GetAllAsync()
    {
        _cache ??= await http.GetFromJsonAsync<List<Transaction>>("api/transactions") ?? [];
        return _cache;
    }

    public async Task<int> AddRangeAsync(IEnumerable<Transaction> incoming)
    {
        var resp   = await http.PostAsJsonAsync("api/transactions/batch", incoming.ToArray());
        var result = await resp.Content.ReadFromJsonAsync<BatchResult>();
        _cache = null; // server deduplicates; refetch to get canonical list
        return result?.Added ?? 0;
    }

    public async Task UpdateAsync(Transaction updated)
    {
        await http.PutAsJsonAsync($"api/transactions/{updated.Id}", updated);
        // object is mutated by reference in the component, cache stays consistent
    }

    public async Task DeleteAsync(Guid id)
    {
        await http.DeleteAsync($"api/transactions/{id}");
        _cache?.RemoveAll(t => t.Id == id);
    }

    public async Task DeleteBatchAsync(IEnumerable<Guid> ids)
    {
        var idSet = ids.ToHashSet();
        await http.PostAsJsonAsync("api/transactions/batch-delete", idSet.ToArray());
        _cache?.RemoveAll(t => idSet.Contains(t.Id));
    }

    private record BatchResult(int Added);
}
