static class AccountEndpoints
{
    public static void MapAccountEndpoints(this RouteGroupBuilder api)
    {
        // Accounts (OwnerOnly)
        api.MapGet("/api/accounts", async (AppDbContext db) =>
            await db.Accounts.OrderBy(a => a.Name).ToListAsync())
            .RequireAuthorization("OwnerOnly");

        api.MapPost("/api/accounts", async (AccountEntity account, AppDbContext db) =>
        {
            var existing = await db.Accounts.FindAsync(account.Id);
            if (existing is null)
                db.Accounts.Add(account);
            else
            {
                existing.Name  = account.Name;
                existing.Type  = account.Type;
                existing.Color = account.Color;
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("OwnerOnly");

        api.MapDelete("/api/accounts/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var acct = await db.Accounts.FindAsync(id);
            if (acct is null) return Results.NotFound();
            db.Accounts.Remove(acct);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("OwnerOnly");

        // Goals
        api.MapGet("/api/goals", async (AppDbContext db) =>
            await db.Goals.OrderBy(g => g.Name).ToListAsync());

        api.MapPost("/api/goals", async (GoalEntity goal, AppDbContext db) =>
        {
            var existing = await db.Goals.FindAsync(goal.Id);
            if (existing is null)
                db.Goals.Add(goal);
            else
            {
                existing.Name          = goal.Name;
                existing.Color         = goal.Color;
                existing.TargetAmount  = goal.TargetAmount;
                existing.CurrentAmount = goal.CurrentAmount;
                existing.Notes         = goal.Notes;
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapDelete("/api/goals/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var goal = await db.Goals.FindAsync(id);
            if (goal is null) return Results.NotFound();
            db.Goals.Remove(goal);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}
