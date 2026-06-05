static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/api/settings/email", async (AppDbContext db) =>
        {
            var cfg = await db.EmailConfig.FindAsync(1) ?? new EmailConfigEntity();
            return Results.Ok(new
            {
                cfg.FromAddress,
                cfg.FromName,
                cfg.AppBaseUrl
            });
        }).RequireAuthorization("OwnerOnly");

        api.MapPut("/api/settings/email", async (UpdateEmailConfigRequest req, AppDbContext db) =>
        {
            var cfg = await db.EmailConfig.FindAsync(1);
            if (cfg is null)
            {
                cfg = new EmailConfigEntity { Id = 1 };
                db.EmailConfig.Add(cfg);
            }
            cfg.FromAddress = req.FromAddress ?? cfg.FromAddress;
            cfg.FromName    = req.FromName    ?? cfg.FromName;
            cfg.AppBaseUrl  = req.AppBaseUrl  ?? cfg.AppBaseUrl;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("OwnerOnly");

        api.MapPost("/api/settings/email/test", async (TestEmailRequest req, EmailService emailSvc) =>
        {
            if (string.IsNullOrWhiteSpace(req.To))
                return Results.BadRequest(new { Error = "Recipient address is required." });
            try
            {
                await emailSvc.SendTestEmailAsync(req.To);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).RequireAuthorization("OwnerOnly");
    }
}
