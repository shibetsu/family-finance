using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/classify", async (ClassifyRequest req, CancellationToken ct) =>
{
    if (req.Transactions.Length == 0 || req.Categories.Length == 0)
        return Results.Ok(Array.Empty<ClassifyResult>());

    var categoryList = string.Join(", ", req.Categories);
    var txLines = req.Transactions
        .Select((t, i) => $"{i + 1}. {t}")
        .Aggregate((a, b) => $"{a}\n{b}");

    var prompt = $$"""
        Classify each financial transaction into exactly one of these budget categories: {{categoryList}}

        Transactions:
        {{txLines}}

        Reply with ONLY a valid JSON array — no explanation, no markdown fences:
        [{"index":1,"category":"CategoryName"}]
        """;

    var raw = await RunClaudeAsync(prompt, ct);
    if (raw is null)
        return Results.Problem("Claude process failed or timed out.");

    // Extract the JSON array even if Claude added surrounding text
    var start = raw.IndexOf('[');
    var end   = raw.LastIndexOf(']');
    if (start < 0 || end <= start)
        return Results.Ok(Array.Empty<ClassifyResult>());

    try
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var results = JsonSerializer.Deserialize<ClassifyResult[]>(raw[start..(end + 1)], opts);
        return Results.Ok(results ?? []);
    }
    catch
    {
        return Results.Ok(Array.Empty<ClassifyResult>());
    }
});

app.Run("http://localhost:5111");

// ---------------------------------------------------------------------------

static async Task<string?> RunClaudeAsync(string prompt, CancellationToken ct)
{
    // On Windows, claude is a .cmd script — invoke through cmd.exe
    var psi = new ProcessStartInfo
    {
        FileName              = OperatingSystem.IsWindows() ? "cmd.exe" : "claude",
        Arguments             = OperatingSystem.IsWindows() ? "/c claude -p" : "-p",
        RedirectStandardInput  = true,
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute       = false,
        CreateNoWindow        = true
    };

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(120));

    try
    {
        using var proc = Process.Start(psi);
        if (proc is null) return null;

        await proc.StandardInput.WriteAsync(prompt);
        proc.StandardInput.Close();

        var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
        await proc.WaitForExitAsync(cts.Token);
        return output;
    }
    catch { return null; }
}

record ClassifyRequest(
    [property: JsonPropertyName("transactions")] string[] Transactions,
    [property: JsonPropertyName("categories")]   string[] Categories);

record ClassifyResult(
    [property: JsonPropertyName("index")]    int    Index,
    [property: JsonPropertyName("category")] string Category);
