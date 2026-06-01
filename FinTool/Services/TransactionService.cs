using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class TransactionService(HttpClient http)
{
    public async Task<List<Transaction>> GetAllAsync() =>
        await http.GetFromJsonAsync<List<Transaction>>("api/transactions") ?? [];

    public async Task<int> AddRangeAsync(IEnumerable<Transaction> incoming)
    {
        var resp   = await http.PostAsJsonAsync("api/transactions/batch", incoming.ToArray());
        var result = await resp.Content.ReadFromJsonAsync<BatchResult>();
        return result?.Added ?? 0;
    }

    public async Task UpdateAsync(Transaction updated) =>
        await http.PutAsJsonAsync($"api/transactions/{updated.Id}", updated);

    public async Task DeleteAsync(Guid id) =>
        await http.DeleteAsync($"api/transactions/{id}");

    private record BatchResult(int Added);
}
