using System.Net.Http.Json;
using System.Text.Json;

namespace FinTool.Services;

public class ChatService(HttpClient http)
{
    public record ChatTurn(string Role, string Content);

    // null  → network/connection error (server unreachable)
    // IsError=true  → server reachable but Claude CLI failed; Content has the reason
    // IsError=false → success; Content is the assistant's reply
    public record ChatResult(bool IsError, string Content);

    public async Task<ChatResult?> SendAsync(string month, string message, List<ChatTurn> history)
    {
        var req = new { Month = month, Message = message, History = history.ToArray() };
        return await PostChat("api/chat", req);
    }

    public async Task<ChatResult?> SendBudgetAsync(string message, List<ChatTurn> history)
    {
        var req = new { Message = message, History = history.ToArray() };
        return await PostChat("api/chat/budget", req);
    }

    private async Task<ChatResult?> PostChat(string url, object req)
    {
        try
        {
            var resp = await http.PostAsJsonAsync(url, req);
            if (!resp.IsSuccessStatusCode)
            {
                var detail = await TryReadProblemDetail(resp);
                return new ChatResult(IsError: true,
                    Content: detail ?? "Claude returned an unexpected error.");
            }
            var result = await resp.Content.ReadFromJsonAsync<ChatResponse>();
            return result?.Response is string s
                ? new ChatResult(IsError: false, Content: s)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TryReadProblemDetail(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var d))
                return d.GetString();
        }
        catch { }
        return null;
    }

    private record ChatResponse(string Response);
}
