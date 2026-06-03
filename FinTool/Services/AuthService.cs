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

    public async Task<bool> RegisterAsync(string username, string password, string? email = null, string? displayName = null, string role = "User")
    {
        var resp = await http.PostAsJsonAsync("api/auth/register",
            new { Username = username, Password = password, Email = email, DisplayName = displayName, Role = role });
        return resp.IsSuccessStatusCode;
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
    public record UserDto(Guid Id, string Username, string Email, string DisplayName, string Role);
}
