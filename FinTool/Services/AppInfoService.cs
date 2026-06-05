using System.Net.Http.Json;

namespace FinTool.Services;

public class AppInfoService(HttpClient http)
{
    private string? _version;

    public async Task<string> GetVersionAsync()
    {
        if (_version is not null) return _version;
        try
        {
            var resp = await http.GetFromJsonAsync<PingResponse>("api/ping");
            _version = resp?.Version ?? "–";
        }
        catch { _version = "–"; }
        return _version;
    }

    private record PingResponse(string Status, string? Version);
}
