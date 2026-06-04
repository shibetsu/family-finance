static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/api/transactions", async (AppDbContext db) =>
            await db.Transactions.OrderByDescending(t => t.Date).ThenBy(t => t.Description).ToListAsync());

        api.MapPost("/api/transactions/batch", async (TxEntity[] incoming, AppDbContext db) =>
        {
            var added = 0;
            var existing = (await db.Transactions
                .Select(t => $"{t.Date}|{t.Description}|{t.Amount}")
                .ToListAsync())
                .ToHashSet();

            foreach (var tx in incoming)
            {
                if (existing.Contains($"{tx.Date}|{tx.Description}|{tx.Amount}")) continue;
                db.Transactions.Add(tx);
                added++;
            }
            if (added > 0) await db.SaveChangesAsync();
            return Results.Ok(new { added });
        });

        api.MapPut("/api/transactions/{id:guid}", async (Guid id, TxEntity updated, AppDbContext db) =>
        {
            var tx = await db.Transactions.FindAsync(id);
            if (tx is null) return Results.NotFound();
            tx.Category    = updated.Category;
            tx.IsRevenue   = updated.IsRevenue;
            tx.IsConfirmed = updated.IsConfirmed;
            tx.AccountId   = updated.AccountId;
            tx.GoalId      = updated.GoalId;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapDelete("/api/transactions/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var tx = await db.Transactions.FindAsync(id);
            if (tx is null) return Results.NotFound();
            db.Transactions.Remove(tx);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapPost("/api/transactions/batch-delete", async (Guid[] ids, AppDbContext db) =>
        {
            await db.Transactions.Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync();
            return Results.Ok();
        });
    }
}
