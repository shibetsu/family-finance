static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/app-settings", async (AppDbContext db) =>
        {
            var cfg = await db.EmailConfig.FindAsync(1) ?? new EmailConfigEntity();
            return Results.Ok(new { cfg.AppBaseUrl });
        }).RequireAuthorization("OwnerOnly");

        app.MapPut("/api/app-settings", async (UpdateAppSettingsRequest req, AppDbContext db) =>
        {
            var cfg = await db.EmailConfig.FindAsync(1);
            if (cfg is null)
            {
                cfg = new EmailConfigEntity { Id = 1 };
                db.EmailConfig.Add(cfg);
            }
            cfg.AppBaseUrl = req.AppBaseUrl ?? cfg.AppBaseUrl;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("OwnerOnly");
    }
}
