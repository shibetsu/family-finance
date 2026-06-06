using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class AppSettingsService(HttpClient http)
{
    public async Task<AppSettings?> GetAsync()
    {
        var resp = await http.GetAsync("api/app-settings");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AppSettings>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(string appBaseUrl)
    {
        var resp = await http.PutAsJsonAsync("api/app-settings", new { AppBaseUrl = appBaseUrl });
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
            return (false, err?.Error ?? "An error occurred.");
        }
        catch { return (false, "An error occurred."); }
    }

    private record ErrorBody(string? Error);
}
