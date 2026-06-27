static class AccountEndpoints
{
    public static void MapAccountEndpoints(this RouteGroupBuilder api)
    {
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
