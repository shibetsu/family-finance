static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app, SymmetricSecurityKey jwtKey)
    {
        app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user is null || !VerifyPassword(req.Password, user.PasswordHash, user.Salt))
                return Results.Unauthorized();
            return Results.Ok(new { Token = GenerateToken(user.Username, user.DisplayName, user.Role, jwtKey), Username = user.Username });
        });

        app.MapPost("/api/auth/register", async (RegisterRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { Error = "Username and password are required." });
            if (await db.Users.AnyAsync(u => u.Username == req.Username))
                return Results.BadRequest(new { Error = "Username already exists." });
            var role = req.Role is "Owner" or "User" ? req.Role : "User";
            var (hash, salt) = HashPassword(req.Password);
            db.Users.Add(new UserEntity { Id = Guid.NewGuid(), Username = req.Username, PasswordHash = hash, Salt = salt, Email = req.Email ?? "", DisplayName = req.DisplayName ?? "", Role = role });
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("OwnerOnly");

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
            if (req.Email is not null)        user.Email       = req.Email;
            if (req.DisplayName is not null)  user.DisplayName = req.DisplayName;
            if (req.Role is "Owner" or "User") user.Role       = req.Role;
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                var (hash, salt) = HashPassword(req.Password);
                user.PasswordHash = hash;
                user.Salt         = salt;
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        app.MapGet("/api/auth/users", async (AppDbContext db) =>
            await db.Users.Select(u => new { u.Id, u.Username, u.Email, u.DisplayName, u.Role }).ToListAsync()
        ).RequireAuthorization("OwnerOnly");

        app.MapDelete("/api/auth/users/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("OwnerOnly");

        app.MapGet("/api/auth/validate", () => Results.Ok()).RequireAuthorization();
    }

    internal static string GenerateToken(string username, string displayName, string role, SymmetricSecurityKey key)
    {
        var claims = new[]
        {
            new Claim("name", username),
            new Claim("display_name", string.IsNullOrEmpty(displayName) ? username : displayName),
            new Claim("role", role)
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
