using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class BudgetPlannerService(HttpClient http)
{
    public async Task<List<BudgetDraft>> GetDraftsAsync()
        => await http.GetFromJsonAsync<List<BudgetDraft>>("api/budget-drafts") ?? [];

    public async Task SaveDraftsAsync(List<BudgetDraft> drafts)
        => await http.PutAsJsonAsync("api/budget-drafts", drafts);
}
