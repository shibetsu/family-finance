using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace FinTool.Services;

public class AuthHeaderHandler(TokenHolder tokenHolder, LocalStorageService storage, NavigationManager nav) : DelegatingHandler
{
    private const string TokenKey = "fintool_auth_token";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = tokenHolder.Token;

        if (string.IsNullOrEmpty(token))
        {
            try { token = await storage.GetAsync<string>(TokenKey); }
            catch { /* JS interop not available during pre-render */ }
        }

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Console.WriteLine($"[Auth] {request.Method} {request.RequestUri?.PathAndQuery} | token={(!string.IsNullOrEmpty(token) ? "SET" : "MISSING")}");

        var response = await base.SendAsync(request, ct);

        // Mid-session token expiry: clear credentials and send the user to login.
        // Skip auth endpoints themselves to avoid loops (validate/login handle their own 401s).
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && request.RequestUri?.AbsolutePath.StartsWith("/api/auth/") != true)
        {
            tokenHolder.Token = null;
            try { await storage.SetAsync<string?>(TokenKey, null); } catch { }
            nav.NavigateTo("/login", replace: true);
        }

        return response;
    }
}
