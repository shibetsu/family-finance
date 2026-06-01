using System.Net.Http.Json;

namespace FinTool.Services;

public class ClosedMonthService(HttpClient http)
{
    private HashSet<string>? _cache;

    public async Task<HashSet<string>> GetClosedAsync()
    {
        if (_cache is not null) return _cache;
        var list = await http.GetFromJsonAsync<List<string>>("api/closed-months");
        _cache = list?.ToHashSet() ?? [];
        return _cache;
    }

    public async Task CloseMonthAsync(DateOnly month)
    {
        var key = MonthKey(month);
        await http.PostAsJsonAsync("api/closed-months", new { MonthKey = key });
        _cache?.Add(key);
    }

    public static string MonthKey(DateOnly month) => $"{month.Year:D4}-{month.Month:D2}";
}
