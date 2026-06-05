using System.Net.Http.Json;

namespace FinTool.Services;

public class AuthService(HttpClient http, LocalStorageService storage, AppAuthStateProvider authProvider)
{
    private const string TokenKey = "fintool_auth_token";

    public async Task<bool> LoginAsync(string username, string password)
    {
        var resp = await http.PostAsJsonAsync("api/auth/login",
            new { Username = username, Password = password });
        if (!resp.IsSuccessStatusCode) return false;
        var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        if (result?.Token is null) return false;
        await storage.SetAsync(TokenKey, result.Token);
        authProvider.MarkAuthenticated(result.Token);
        return true;
    }

    public async Task LogoutAsync()
    {
        await storage.SetAsync<string?>(TokenKey, null);
        authProvider.MarkLoggedOut();
    }

    public async Task<RegisterResult> RegisterAsync(string username, string? email, string? displayName = null, string role = "User")
    {
        var resp = await http.PostAsJsonAsync("api/auth/register",
            new { Username = username, Email = email, DisplayName = displayName, Role = role });
        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
                return new RegisterResult(false, err?.Error ?? "An error occurred.", null, null);
            }
            catch { return new RegisterResult(false, "An error occurred.", null, null); }
        }
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return new RegisterResult(true, null, body?.Warning, body?.SetPasswordUrl);
        }
        catch { return new RegisterResult(true, null, null, null); }
    }

    public async Task<RegisterResult> ResendInviteAsync(Guid id)
    {
        var resp = await http.PostAsync($"api/auth/resend-invite/{id}", null);
        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
                return new RegisterResult(false, err?.Error ?? "An error occurred.", null, null);
            }
            catch { return new RegisterResult(false, "An error occurred.", null, null); }
        }
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return new RegisterResult(true, null, body?.Warning, body?.SetPasswordUrl);
        }
        catch { return new RegisterResult(true, null, null, null); }
    }

    public async Task<(bool Ok, string? Username, string? DisplayName, string? Error)> ValidateSetPasswordTokenAsync(string token)
    {
        var resp = await http.GetAsync($"api/auth/set-password?token={Uri.EscapeDataString(token)}");
        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
                return (false, null, null, err?.Error ?? "Invalid or expired link.");
            }
            catch { return (false, null, null, "Invalid or expired link."); }
        }
        var body = await resp.Content.ReadFromJsonAsync<TokenValidationResponse>();
        return (true, body?.Username, body?.DisplayName, null);
    }

    public async Task<(bool Ok, string? Error)> SetPasswordAsync(string token, string password)
    {
        var resp = await http.PostAsJsonAsync("api/auth/set-password",
            new { Token = token, Password = password });
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
            return (false, err?.Error ?? "An error occurred.");
        }
        catch { return (false, "An error occurred."); }
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        return await http.GetFromJsonAsync<List<UserDto>>("api/auth/users") ?? [];
    }

    public async Task<(bool Ok, string? Error)> UpdateUserAsync(Guid id, string? username, string? email, string? displayName, string? role, string? password)
    {
        var resp = await http.PutAsJsonAsync($"api/auth/users/{id}",
            new { Username = username, Email = email, DisplayName = displayName, Role = role, Password = password });
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
            return (false, err?.Error);
        }
        catch { return (false, "An error occurred."); }
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var resp = await http.DeleteAsync($"api/auth/users/{id}");
        return resp.IsSuccessStatusCode;
    }

    private record LoginResponse(string Token, string Username);
    private record ErrorBody(string? Error);
    private record RegisterResponse(string? Warning, string? SetPasswordUrl);
    private record TokenValidationResponse(string Username, string DisplayName);

    public record RegisterResult(bool Ok, string? Error, string? Warning, string? SetPasswordUrl);
    public record UserDto(Guid Id, string Username, string Email, string DisplayName, string Role, bool HasPassword);
}
