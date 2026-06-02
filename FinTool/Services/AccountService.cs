using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class AccountService(HttpClient http)
{
    private List<Account>? _cache;

    public async Task<List<Account>> GetAllAsync()
    {
        _cache ??= await http.GetFromJsonAsync<List<Account>>("api/accounts") ?? [];
        return _cache;
    }

    public async Task SaveAsync(Account account)
    {
        await http.PostAsJsonAsync("api/accounts", account);
        _cache = null;
    }

    public async Task DeleteAsync(Guid id)
    {
        await http.DeleteAsync($"api/accounts/{id}");
        _cache = null;
    }
}
