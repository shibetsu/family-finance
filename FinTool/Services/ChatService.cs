using System.Net.Http.Json;

namespace FinTool.Services;

public class ChatService(HttpClient http)
{
    public record ChatTurn(string Role, string Content);

    public async Task<string?> SendAsync(string month, string message, List<ChatTurn> history)
    {
        var req = new { Month = month, Message = message, History = history.ToArray() };
        return await PostChat("api/chat", req);
    }

    public async Task<string?> SendBudgetAsync(string message, List<ChatTurn> history)
    {
        var req = new { Message = message, History = history.ToArray() };
        return await PostChat("api/chat/budget", req);
    }

    private async Task<string?> PostChat(string url, object req)
    {
        try
        {
            var resp = await http.PostAsJsonAsync(url, req);
            if (!resp.IsSuccessStatusCode) return null;
            var result = await resp.Content.ReadFromJsonAsync<ChatResponse>();
            return result?.Response;
        }
        catch { return null; }
    }

    private record ChatResponse(string Response);
}
