using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace FinTool.Services;

public class AppAuthStateProvider(LocalStorageService storage, TokenHolder tokenHolder, HttpClient http)
    : AuthenticationStateProvider
{
    private const string TokenKey = "fintool_auth_token";
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await storage.GetAsync<string>(TokenKey);
            if (string.IsNullOrEmpty(token)) return Anonymous;
            var principal = ParseJwt(token);
            if (principal.Identity?.IsAuthenticated != true) return Anonymous;

            // Set token before the validate call so AuthHeaderHandler attaches it
            tokenHolder.Token = token;

            // Confirm the token is still accepted by the server.
            // Without this, a stale/expired token makes the client think it's
            // authenticated while every API call returns 401.
            try
            {
                var resp = await http.GetAsync("api/auth/validate");
                if (!resp.IsSuccessStatusCode)
                {
                    await storage.SetAsync<string?>(TokenKey, null);
                    tokenHolder.Token = null;
                    return Anonymous;
                }
            }
            catch
            {
                // Server unreachable — trust the local token so offline startup works
            }

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            Console.WriteLine($"[AppAuthStateProvider] Authenticated as {principal.Identity.Name}");
            return new AuthenticationState(principal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppAuthStateProvider] GetAuthenticationStateAsync error: {ex.Message}");
            return Anonymous;
        }
    }

    public void MarkAuthenticated(string token)
    {
        tokenHolder.Token = token;
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var principal = ParseJwt(token);
        Console.WriteLine($"[AppAuthStateProvider] MarkAuthenticated: {principal.Identity?.Name}");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    public void MarkLoggedOut()
    {
        tokenHolder.Token = null;
        http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static ClaimsPrincipal ParseJwt(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return new ClaimsPrincipal(new ClaimsIdentity());

        // JWT uses base64url encoding — fix padding and alphabet
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            var claims = new List<Claim>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var val = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()!
                    : prop.Value.ToString();
                if (prop.Name == "name")
                    claims.Add(new Claim(ClaimTypes.Name, val));
                else if (prop.Name == "display_name")
                    claims.Add(new Claim(ClaimTypes.GivenName, val));
                else if (prop.Name == "role")
                    claims.Add(new Claim(ClaimTypes.Role, val));
                else
                    claims.Add(new Claim(prop.Name, val));
            }
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        }
        catch { return new ClaimsPrincipal(new ClaimsIdentity()); }
    }
}
