static class BudgetEndpoints
{
    private static readonly JsonSerializerOptions _draftJsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static void MapBudgetEndpoints(this RouteGroupBuilder api)
    {
        // Budget categories
        api.MapGet("/api/budget-categories", async (AppDbContext db) =>
            await db.BudgetCategories.ToListAsync());

        api.MapPost("/api/budget-categories", async (BudgetCategoryEntity cat, AppDbContext db) =>
        {
            var existing = await db.BudgetCategories.FindAsync(cat.Id);
            if (existing is null)
            {
                db.BudgetCategories.Add(cat);
            }
            else
            {
                existing.Name          = cat.Name;
                existing.MonthlyAmount = cat.MonthlyAmount;
                existing.Color         = cat.Color;
                existing.IsIgnored     = cat.IsIgnored;
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapDelete("/api/budget-categories/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var cat = await db.BudgetCategories.FindAsync(id);
            if (cat is null) return Results.NotFound();
            db.BudgetCategories.Remove(cat);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Revenue categories
        api.MapGet("/api/revenue-categories", async (AppDbContext db) =>
            await db.RevenueCategories.ToListAsync());

        api.MapPost("/api/revenue-categories", async (RevenueCategoryEntity cat, AppDbContext db) =>
        {
            var existing = await db.RevenueCategories.FindAsync(cat.Id);
            if (existing is null)
            {
                db.RevenueCategories.Add(cat);
            }
            else
            {
                existing.Name      = cat.Name;
                existing.Color     = cat.Color;
                existing.IsIgnored = cat.IsIgnored;
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapDelete("/api/revenue-categories/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var cat = await db.RevenueCategories.FindAsync(id);
            if (cat is null) return Results.NotFound();
            db.RevenueCategories.Remove(cat);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Budget drafts
        api.MapGet("/api/budget-drafts", async (AppDbContext db) =>
            (await db.BudgetDrafts.ToListAsync()).Select(e => new
            {
                e.Id,
                e.Name,
                Expenses = JsonSerializer.Deserialize<object[]>(e.ExpensesJson, _draftJsonOpts) ?? Array.Empty<object>(),
                Revenue  = JsonSerializer.Deserialize<object[]>(e.RevenueJson,  _draftJsonOpts) ?? Array.Empty<object>()
            }));

        api.MapPut("/api/budget-drafts", async (BudgetDraftDto[] incoming, AppDbContext db) =>
        {
            db.BudgetDrafts.RemoveRange(await db.BudgetDrafts.ToListAsync());
            foreach (var d in incoming)
                db.BudgetDrafts.Add(new BudgetDraftEntity
                {
                    Id           = d.Id,
                    Name         = d.Name,
                    ExpensesJson = JsonSerializer.Serialize(d.Expenses),
                    RevenueJson  = JsonSerializer.Serialize(d.Revenue)
                });
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}
