namespace FinTool.Services;

public class ClosedMonthService(LocalStorageService storage)
{
    private const string Key = "fintool_closed_months";
    private HashSet<string>? _cache;

    public async Task<HashSet<string>> GetClosedAsync()
    {
        _cache ??= (await storage.GetAsync<List<string>>(Key) ?? []).ToHashSet();
        return _cache;
    }

    public async Task CloseMonthAsync(DateOnly month)
    {
        var closed = await GetClosedAsync();
        if (closed.Add(MonthKey(month)))
            await storage.SetAsync(Key, closed.ToList());
    }

    public static string MonthKey(DateOnly month) => $"{month.Year:D4}-{month.Month:D2}";
}
