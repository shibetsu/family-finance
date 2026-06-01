using System.Net.Http.Json;

namespace FinTool.Services;

public class ClosedMonthService(HttpClient http)
{
    public async Task<HashSet<string>> GetClosedAsync()
    {
        var list = await http.GetFromJsonAsync<List<string>>("api/closed-months");
        return list?.ToHashSet() ?? [];
    }

    public async Task CloseMonthAsync(DateOnly month) =>
        await http.PostAsJsonAsync("api/closed-months",
            new { MonthKey = MonthKey(month) });

    public static string MonthKey(DateOnly month) => $"{month.Year:D4}-{month.Month:D2}";
}
