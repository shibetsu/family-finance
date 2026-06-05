using System.Net.Http.Json;
using FinTool.Models;

namespace FinTool.Services;

public class EmailConfigService(HttpClient http)
{
    public async Task<EmailConfig?> GetAsync()
        => await http.GetFromJsonAsync<EmailConfig>("api/settings/email");

    public async Task<(bool Ok, string? Error)> SaveAsync(string fromAddress, string fromName, string appBaseUrl)
    {
        var resp = await http.PutAsJsonAsync("api/settings/email", new
        {
            FromAddress = fromAddress,
            FromName    = fromName,
            AppBaseUrl  = appBaseUrl
        });
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
            return (false, err?.Error ?? "An error occurred.");
        }
        catch { return (false, "An error occurred."); }
    }

    public async Task<(bool Ok, string? Error)> SendTestAsync(string to)
    {
        var resp = await http.PostAsJsonAsync("api/settings/email/test", new { To = to });
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
