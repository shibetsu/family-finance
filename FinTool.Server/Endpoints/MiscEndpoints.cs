static class MiscEndpoints
{
    public static void MapMiscEndpoints(this RouteGroupBuilder api)
    {
        // Merchant cache
        api.MapGet("/api/merchant-cache", async (AppDbContext db) =>
            await db.MerchantCache.ToDictionaryAsync(m => m.Description, m => m.Category));

        api.MapPost("/api/merchant-cache/lookup", async (LookupRequest req, AppDbContext db) =>
        {
            var key   = req.Description.Trim().ToUpperInvariant();
            var entry = await db.MerchantCache.FindAsync(key);
            return entry is null ? Results.Ok(new { Category = (string?)null }) : Results.Ok(new { entry.Category });
        });

        api.MapPost("/api/merchant-cache/set", async (SetCacheRequest req, AppDbContext db) =>
        {
            var key   = req.Description.Trim().ToUpperInvariant();
            var entry = await db.MerchantCache.FindAsync(key);
            if (entry is null)
                db.MerchantCache.Add(new MerchantEntry { Description = key, Category = req.Category });
            else
                entry.Category = req.Category;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Closed months
        api.MapGet("/api/closed-months", async (AppDbContext db) =>
            await db.ClosedMonths.Select(m => m.MonthKey).ToListAsync());

        api.MapPost("/api/closed-months", async (CloseMonthRequest req, AppDbContext db) =>
        {
            if (!await db.ClosedMonths.AnyAsync(m => m.MonthKey == req.MonthKey))
            {
                db.ClosedMonths.Add(new ClosedMonthEntry { MonthKey = req.MonthKey });
                await db.SaveChangesAsync();
            }
            return Results.Ok();
        });

        // Recurring transactions (derived from confirmed transaction history)
        api.MapGet("/api/recurring", async (AppDbContext db) =>
        {
            var allTx = await db.Transactions
                .Where(t => t.Amount != 0 && t.IsConfirmed)
                .ToListAsync();

            if (!allTx.Any()) return Results.Ok(Array.Empty<object>());

            var mostRecentMonth = allTx.Max(t => t.Date[..7]) ?? "";

            var items = allTx
                .GroupBy(t => (t.Description, t.IsRevenue))
                .Where(g => g.Select(t => t.Date[..7]).Distinct().Count() >= 2)
                .Select(g =>
                {
                    var months     = g.Select(t => t.Date[..7]).Distinct().OrderBy(x => x).ToList();
                    var lastMonth  = months.Last();
                    var ordered    = g.OrderByDescending(t => t.Date).ToList();
                    var avgAmount  = Math.Round(Math.Abs(g.Average(t => t.Amount)), 2);
                    var lastAmount = Math.Round(Math.Abs(ordered.First().Amount), 2);
                    var monthsAgo  = MonthDiff(mostRecentMonth, lastMonth);
                    var status     = monthsAgo == 0 ? "active" : monthsAgo <= 3 ? "paused" : "cancelled";
                    return new
                    {
                        Description = g.Key.Description,
                        Category    = ordered.First().Category,
                        IsRevenue   = g.Key.IsRevenue,
                        AvgAmount   = avgAmount,
                        LastAmount  = lastAmount,
                        LastMonth   = lastMonth,
                        Occurrences = months.Count,
                        Status      = status
                    };
                })
                .OrderByDescending(x => x.AvgAmount)
                .ToList();

            return Results.Ok(items);
        });
    }

    private static int MonthDiff(string recent, string older)
    {
        var r = DateOnly.ParseExact(recent + "-01", "yyyy-MM-dd", null);
        var o = DateOnly.ParseExact(older  + "-01", "yyyy-MM-dd", null);
        return (r.Year - o.Year) * 12 + r.Month - o.Month;
    }
}
