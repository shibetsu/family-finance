using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class ClaudeService(HttpClient http)
{
    private const string ClassifyUrl = "http://localhost:5111/api/classify";
    private const string PingUrl     = "http://localhost:5111/api/ping";

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var resp = await http.GetAsync(PingUrl);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // Returns Transaction.Id → suggested category name.
    // Silently returns empty on any failure so the UI degrades gracefully.
    public async Task<Dictionary<Guid, string>> ClassifyAsync(
        List<Transaction> transactions,
        List<BudgetCategory> categories)
    {
        if (transactions.Count == 0 || categories.Count == 0) return [];

        var request = new
        {
            transactions = transactions.Select(t => t.Description).ToArray(),
            categories   = categories.Select(c => c.Name).ToArray()
        };

        HttpResponseMessage resp;
        try { resp = await http.PostAsJsonAsync(ClassifyUrl, request); }
        catch { return []; }

        if (!resp.IsSuccessStatusCode) return [];

        try
        {
            var results = await resp.Content.ReadFromJsonAsync<ClassifyResult[]>();
            return results?
                .Where(r => r.Index >= 1 && r.Index <= transactions.Count)
                .ToDictionary(r => transactions[r.Index - 1].Id, r => r.Category)
                ?? [];
        }
        catch { return []; }
    }

    private record ClassifyResult(int Index, string Category);
}
