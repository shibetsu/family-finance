static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app, SymmetricSecurityKey jwtKey)
    {
        app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
                !VerifyPassword(req.Password, user.PasswordHash, user.Salt))
                return Results.Unauthorized();
            return Results.Ok(new
            {
                Token    = GenerateToken(user.Username, user.DisplayName, user.Role, jwtKey),
                Username = user.Username
            });
        });

        // ── Register (Owner only) ────────────────────────────────────────────
        app.MapPost("/api/auth/register", async (RegisterRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username))
                return Results.BadRequest(new { Error = "Username is required." });
            if (await db.Users.AnyAsync(u => u.Username == req.Username))
                return Results.BadRequest(new { Error = "Username already exists." });

            var role   = req.Role is "Owner" or "User" ? req.Role : "User";
            var userId = Guid.NewGuid();
            db.Users.Add(new UserEntity
            {
                Id           = userId,
                Username     = req.Username,
                PasswordHash = "",
                Salt         = "",
                Email        = req.Email ?? "",
                DisplayName  = req.DisplayName ?? "",
                Role         = role
            });

            var (tokenValue, resetToken) = CreateResetToken(userId);
            db.PasswordResetTokens.Add(resetToken);
            await db.SaveChangesAsync();

            var setPasswordUrl = await BuildSetPasswordUrl(db, tokenValue);
            return Results.Ok(new { SetPasswordUrl = setPasswordUrl });
        }).RequireAuthorization("OwnerOnly");

        // ── Update user ──────────────────────────────────────────────────────
        app.MapPut("/api/auth/users/{id:guid}", async (Guid id, UpdateUserRequest req, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(req.Username) && req.Username != user.Username)
            {
                if (await db.Users.AnyAsync(u => u.Username == req.Username))
                    return Results.BadRequest(new { Error = "Username already exists." });
                user.Username = req.Username;
            }
            if (req.Email is not null)               user.Email       = req.Email;
            if (req.DisplayName is not null)         user.DisplayName = req.DisplayName;
            if (req.Role is "Owner" or "User")       user.Role        = req.Role;
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                var (hash, salt) = HashPassword(req.Password);
                user.PasswordHash = hash;
                user.Salt         = salt;
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        // ── List users ───────────────────────────────────────────────────────
        app.MapGet("/api/auth/users", async (AppDbContext db) =>
            await db.Users
                .Select(u => new
                {
                    u.Id, u.Username, u.Email, u.DisplayName, u.Role,
                    HasPassword = u.PasswordHash != ""
                })
                .ToListAsync()
        ).RequireAuthorization("OwnerOnly");

        // ── Delete user ──────────────────────────────────────────────────────
        app.MapDelete("/api/auth/users/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("OwnerOnly");

        app.MapGet("/api/auth/validate", () => Results.Ok()).RequireAuthorization();

        // ── Get setup link (Owner only) ──────────────────────────────────────
        app.MapPost("/api/auth/resend-invite/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            var old = await db.PasswordResetTokens
                .Where(t => t.UserId == id && !t.Used).ToListAsync();
            foreach (var t in old) t.Used = true;

            var (tokenValue, resetToken) = CreateResetToken(id);
            db.PasswordResetTokens.Add(resetToken);
            await db.SaveChangesAsync();

            var setPasswordUrl = await BuildSetPasswordUrl(db, tokenValue);
            return Results.Ok(new { SetPasswordUrl = setPasswordUrl });
        }).RequireAuthorization("OwnerOnly");

        // ── Validate token (public) ──────────────────────────────────────────
        app.MapGet("/api/auth/set-password", async (string token, AppDbContext db) =>
        {
            var entry = await db.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.ExpiresAt > DateTime.UtcNow);
            if (entry is null) return Results.BadRequest(new { Error = "Invalid or expired link." });
            var user = await db.Users.FindAsync(entry.UserId);
            if (user is null) return Results.NotFound();
            return Results.Ok(new { user.Username, user.DisplayName });
        });

        // ── Set password (public) ────────────────────────────────────────────
        app.MapPost("/api/auth/set-password", async (SetPasswordRequest req, AppDbContext db) =>
        {
            var entry = await db.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == req.Token && !t.Used && t.ExpiresAt > DateTime.UtcNow);
            if (entry is null)
                return Results.BadRequest(new { Error = "Invalid or expired link." });
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Results.BadRequest(new { Error = "Password must be at least 8 characters." });
            var user = await db.Users.FindAsync(entry.UserId);
            if (user is null) return Results.NotFound();
            var (hash, salt) = HashPassword(req.Password);
            user.PasswordHash = hash;
            user.Salt         = salt;
            entry.Used        = true;
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (string tokenValue, PasswordResetTokenEntity entity) CreateResetToken(Guid userId)
    {
        var tokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var entity = new PasswordResetTokenEntity
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            Token     = tokenValue,
            ExpiresAt = DateTime.UtcNow.AddHours(48),
            Used      = false
        };
        return (tokenValue, entity);
    }

    private static async Task<string> BuildSetPasswordUrl(AppDbContext db, string token)
    {
        var cfg     = await db.EmailConfig.FindAsync(1);
        var baseUrl = (cfg?.AppBaseUrl ?? "http://localhost:5111").TrimEnd('/');
        return $"{baseUrl}/set-password?token={token}";
    }

    internal static string GenerateToken(string username, string displayName, string role, SymmetricSecurityKey key)
    {
        var claims = new[]
        {
            new Claim("name",         username),
            new Claim("display_name", string.IsNullOrEmpty(displayName) ? username : displayName),
            new Claim("role",         role)
        };
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims:             claims,
            expires:            DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), saltBytes, 100_000,
            HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    internal static bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), saltBytes, 100_000,
            HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(
            hashBytes, Convert.FromBase64String(hash));
    }
}
