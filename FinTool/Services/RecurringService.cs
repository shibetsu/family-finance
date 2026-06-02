using System.Net.Http.Json;

namespace FinTool.Services;

public class RecurringService(HttpClient http)
{
    public async Task<List<RecurringItem>> GetAllAsync()
        => await http.GetFromJsonAsync<List<RecurringItem>>("api/recurring") ?? [];

    public record RecurringItem(
        string  Description,
        string? Category,
        bool    IsRevenue,
        decimal AvgAmount,
        decimal LastAmount,
        string  LastMonth,
        int     Occurrences,
        string  Status);
}
