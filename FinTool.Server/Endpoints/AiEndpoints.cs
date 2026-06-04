static class AiEndpoints
{
    public static void MapAiEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/api/classify", async (ClassifyRequest req, CancellationToken ct) =>
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

            var start = raw.IndexOf('[');
            var end   = raw.LastIndexOf(']');
            if (start < 0 || end <= start)
                return Results.Ok(Array.Empty<ClassifyResult>());

            try
            {
                var opts    = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var results = JsonSerializer.Deserialize<ClassifyResult[]>(raw[start..(end + 1)], opts);
                return Results.Ok(results ?? []);
            }
            catch
            {
                return Results.Ok(Array.Empty<ClassifyResult>());
            }
        });

        api.MapPost("/api/chat", async (ChatRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var txs = await db.Transactions
                .Where(t => t.Date.StartsWith(req.Month + "-"))
                .OrderBy(t => t.Date).ThenBy(t => t.Description)
                .ToListAsync(ct);

            var budgetCats  = await db.BudgetCategories.OrderBy(c => c.Name).ToListAsync(ct);
            var revenueCats = await db.RevenueCategories.OrderBy(c => c.Name).ToListAsync(ct);

            var expenses = txs.Where(t => !t.IsRevenue && t.Amount > 0).ToList();
            var revenues = txs.Where(t =>  t.IsRevenue).ToList();

            var totalExpenses = expenses.Sum(t => t.Amount);
            var totalRevenue  = revenues.Sum(t => -t.Amount);
            var totalBudget   = budgetCats.Where(c => !c.IsIgnored).Sum(c => c.MonthlyAmount);

            var expByCat = expenses
                .GroupBy(t => t.Category ?? "Uncategorized")
                .Select(g => (Category: g.Key, Total: g.Sum(t => t.Amount), Count: g.Count()))
                .OrderByDescending(x => x.Total).ToList();

            var revByCat = revenues
                .GroupBy(t => t.Category ?? "Uncategorized")
                .Select(g => (Category: g.Key, Total: g.Sum(t => -t.Amount), Count: g.Count()))
                .OrderByDescending(x => x.Total).ToList();

            var monthDisplay = req.Month;
            if (DateTime.TryParseExact(req.Month + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
                monthDisplay = dt.ToString("MMMM yyyy");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"You are FinTool's personal finance assistant. The user is reviewing their finances for {monthDisplay}.");
            sb.AppendLine("Answer questions concisely and insightfully. Highlight trends, warnings, or savings opportunities when relevant.");
            sb.AppendLine("Format amounts as currency. Be conversational but precise. Keep responses under 150 words unless detail is specifically needed.");
            sb.AppendLine();
            sb.AppendLine("=== EXPENSE BUDGET CATEGORIES ===");
            foreach (var cat in budgetCats.Where(c => !c.IsIgnored))
            {
                var spent = expByCat.FirstOrDefault(x => x.Category == cat.Name).Total;
                var count = expByCat.FirstOrDefault(x => x.Category == cat.Name).Count;
                var pct   = cat.MonthlyAmount > 0 ? spent / cat.MonthlyAmount * 100 : 0;
                sb.AppendLine($"- {cat.Name}: budget ${cat.MonthlyAmount:F2}/mo | spent ${spent:F2} ({pct:F0}%) | {count} txns");
            }
            sb.AppendLine();
            sb.AppendLine("=== REVENUE ===");
            if (revByCat.Count > 0)
                foreach (var r in revByCat)
                    sb.AppendLine($"- {r.Category}: ${r.Total:F2} ({r.Count} transactions)");
            else
                sb.AppendLine("No revenue recorded this month.");
            sb.AppendLine();
            sb.AppendLine("=== ALL TRANSACTIONS THIS MONTH ===");
            if (txs.Count > 0)
                foreach (var t in txs)
                    sb.AppendLine($"- {t.Date} | {t.Description} | {(t.IsRevenue ? "+" : "-")}${Math.Abs(t.Amount):F2} | {t.Category ?? "Uncategorized"}");
            else
                sb.AppendLine("No transactions recorded this month.");
            sb.AppendLine();
            sb.AppendLine("=== MONTHLY SUMMARY ===");
            sb.AppendLine($"Total Expenses: ${totalExpenses:F2} / Budget: ${totalBudget:F2} ({(totalBudget > 0 ? totalExpenses / totalBudget * 100 : 0):F0}% used)");
            sb.AppendLine($"Total Revenue:  ${totalRevenue:F2}");
            sb.AppendLine($"Net Balance:    ${totalRevenue - totalExpenses:F2}");
            sb.AppendLine($"Transactions:   {txs.Count}");
            sb.AppendLine();

            if (req.History.Length > 0)
            {
                sb.AppendLine("=== CONVERSATION SO FAR ===");
                foreach (var msg in req.History)
                    sb.AppendLine($"{(msg.Role == "user" ? "User" : "Assistant")}: {msg.Content}");
                sb.AppendLine();
            }

            sb.AppendLine($"User: {req.Message}");
            sb.AppendLine("Assistant:");

            var raw = await RunClaudeAsync(sb.ToString(), ct);
            if (raw is null) return Results.Problem("Claude is unavailable.");

            return Results.Ok(new { response = raw.Trim() });
        });

        api.MapPost("/api/chat/budget", async (BudgetChatRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var budgetCats  = await db.BudgetCategories.OrderBy(c => c.Name).ToListAsync(ct);
            var revenueCats = await db.RevenueCategories.OrderBy(c => c.Name).ToListAsync(ct);
            var allTx       = await db.Transactions.ToListAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.Now);
            var histMonths = Enumerable.Range(1, 3)
                .Select(i => today.AddMonths(-i))
                .Where(m => allTx.Any(t => t.Date.StartsWith(m.ToString("yyyy-MM"))))
                .ToList();

            var histExpense = allTx
                .Where(t => !t.IsRevenue && t.Amount > 0 &&
                            histMonths.Any(m => t.Date.StartsWith(m.ToString("yyyy-MM"))))
                .GroupBy(t => t.Category ?? "Unclassified")
                .ToDictionary(g => g.Key,
                    g => Math.Round(g.Sum(t => t.Amount) / Math.Max(1, histMonths.Count), 0));

            var histRevenue = allTx
                .Where(t => t.IsRevenue &&
                            histMonths.Any(m => t.Date.StartsWith(m.ToString("yyyy-MM"))))
                .GroupBy(t => t.Category ?? "Unclassified")
                .ToDictionary(g => g.Key,
                    g => Math.Round(g.Sum(t => -t.Amount) / Math.Max(1, histMonths.Count), 0));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("You are FinTool's budget planning assistant. You help the user build a new monthly budget draft through a friendly, guided conversation.");
            sb.AppendLine();
            sb.AppendLine("Your goal: gather the information needed to produce a complete monthly budget draft, then create it.");
            sb.AppendLine();
            sb.AppendLine("Typical flow:");
            sb.AppendLine("1. Ask if they want to base the draft on: (A) current categories, (B) spending trends, or (C) from scratch.");
            sb.AppendLine("2. Walk through expense categories — suggest amounts, ask if they want to add, remove, or adjust any.");
            sb.AppendLine("3. Walk through revenue — ask about income sources and amounts.");
            sb.AppendLine("4. Propose the full budget in a clear summary and ask for confirmation.");
            sb.AppendLine("5. Once the user confirms, output the draft in the exact format described below.");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT — When the user confirms they are happy with the draft, end your response with:");
            sb.AppendLine("<<CREATE_DRAFT>>");
            sb.AppendLine("{\"name\":\"Draft Name\",\"expenses\":[{\"name\":\"Food\",\"amount\":500,\"color\":\"#594AE2\"}],\"revenue\":[{\"name\":\"Salary\",\"amount\":3000,\"color\":\"#4CAF50\"}]}");
            sb.AppendLine("<<END_DRAFT>>");
            sb.AppendLine("Use a single-line JSON object. Amounts are monthly figures. Use existing category colors when available.");
            sb.AppendLine();
            sb.AppendLine("Style rules:");
            sb.AppendLine("- Ask one or two questions at a time. Be concise and helpful.");
            sb.AppendLine("- Support markdown: **bold**, *italic*, bullet lists with '- '.");
            sb.AppendLine("- When suggesting a budget summary, show it as a bullet list with amounts.");
            sb.AppendLine();

            sb.AppendLine("=== CURRENT EXPENSE BUDGET CATEGORIES ===");
            var activeBudget = budgetCats.Where(c => !c.IsIgnored).ToList();
            if (activeBudget.Count > 0)
                foreach (var c in activeBudget)
                    sb.AppendLine($"- {c.Name}: ${c.MonthlyAmount:F0}/mo (color: {c.Color})");
            else
                sb.AppendLine("(none defined)");

            sb.AppendLine();
            sb.AppendLine("=== CURRENT REVENUE CATEGORIES ===");
            var activeRev = revenueCats.Where(c => !c.IsIgnored).ToList();
            if (activeRev.Count > 0)
                foreach (var c in activeRev)
                    sb.AppendLine($"- {c.Name} (color: {c.Color})");
            else
                sb.AppendLine("(none defined)");

            sb.AppendLine();
            var monthLabel = histMonths.Count > 0
                ? $"avg over {histMonths.Count} month{(histMonths.Count == 1 ? "" : "s")}"
                : "no historical data";
            sb.AppendLine($"=== SPENDING TRENDS ({monthLabel}) ===");
            if (histExpense.Count > 0)
                foreach (var kv in histExpense.OrderByDescending(x => x.Value))
                    sb.AppendLine($"- {kv.Key}: ~${kv.Value:F0}/mo");
            else
                sb.AppendLine("(no data)");

            sb.AppendLine();
            sb.AppendLine("=== REVENUE TRENDS ===");
            if (histRevenue.Count > 0)
                foreach (var kv in histRevenue.OrderByDescending(x => x.Value))
                    sb.AppendLine($"- {kv.Key}: ~${kv.Value:F0}/mo");
            else
                sb.AppendLine("(no data)");

            sb.AppendLine();
            if (req.History.Length > 0)
            {
                sb.AppendLine("=== CONVERSATION SO FAR ===");
                foreach (var msg in req.History)
                    sb.AppendLine($"{(msg.Role == "user" ? "User" : "Assistant")}: {msg.Content}");
                sb.AppendLine();
            }

            sb.AppendLine($"User: {req.Message}");
            sb.AppendLine("Assistant:");

            var raw = await RunClaudeAsync(sb.ToString(), ct);
            if (raw is null) return Results.Problem("Claude is unavailable.");

            return Results.Ok(new { response = raw.Trim() });
        });
    }

    internal static async Task<string?> RunClaudeAsync(string prompt, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = OperatingSystem.IsWindows() ? "cmd.exe" : "claude",
            Arguments              = OperatingSystem.IsWindows() ? "/c claude -p" : "-p",
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
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
}
