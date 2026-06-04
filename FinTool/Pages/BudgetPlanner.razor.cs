using System.Globalization;
using Microsoft.AspNetCore.Components;
using FinTool.Models;
using FinTool.Services;
using MudBlazor;

namespace FinTool.Pages;

public partial class BudgetPlanner
{
    private bool _loading = true;

    private List<Transaction>     _allTx      = [];
    private List<BudgetCategory>  _budgetCats = [];
    private List<RevenueCategory> _revCats    = [];
    private List<BudgetDraft>     _drafts     = [];
    private int                   _activeIdx  = 0;

    private Dictionary<string, decimal> _histExpense = [];
    private Dictionary<string, decimal> _histRevenue = [];
    private List<(int Months, string Label)> _importOptions = [];

    private BudgetDraft ActiveDraft  => _drafts[_activeIdx];
    private decimal     TotalExpense => ActiveDraft.Expenses.Sum(r => r.Amount);
    private decimal     TotalRevenue => ActiveDraft.Revenue.Sum(r => r.Amount);
    private decimal     Net          => TotalRevenue - TotalExpense;

    private static readonly CultureInfo _ca = CultureInfo.GetCultureInfo("en-CA");
    private static string Fmt(decimal v) => v.ToString("C0", _ca);

    private static string TabStyle(bool active) =>
        "display:flex;align-items:center;gap:2px;padding:4px 6px 4px 10px;" +
        "border-radius:8px;cursor:pointer;white-space:nowrap;flex-shrink:0;transition:all .15s;" +
        "border:1px solid " + (active ? "var(--mud-palette-primary)" : "var(--mud-palette-divider)") + ";" +
        "background:" + (active ? "var(--mud-palette-action-default-hover)" : "transparent");

    private static string InputStyle(bool active, int nameLen) =>
        "border:none;outline:none;background:transparent;font-size:0.875rem;color:inherit;cursor:text;" +
        "font-weight:" + (active ? "600" : "400") + ";" +
        "width:" + Math.Max(50, nameLen * 8) + "px";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await Task.Delay(1);

        _allTx      = await TxSvc.GetAllAsync();
        _budgetCats = await BudgetSvc.GetCategoriesAsync();
        _revCats    = await RevenueSvc.GetCategoriesAsync();
        _drafts     = await PlannerSvc.GetDraftsAsync();

        ComputeHistory();
        _loading = false;
        StateHasChanged();
    }

    private void ComputeHistory()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Find up to 3 contiguous past months that have data
        var months = new List<DateOnly>();
        for (var i = 1; i <= 3; i++)
        {
            var m = today.AddMonths(-i);
            if (_allTx.Any(t => t.Date.Year == m.Year && t.Date.Month == m.Month))
                months.Add(m);
            else
                break;
        }

        if (months.Count == 0) return;

        _histExpense = _allTx
            .Where(t => !t.IsRevenue && t.Amount > 0 &&
                        months.Any(m => t.Date.Year == m.Year && t.Date.Month == m.Month))
            .GroupBy(t => t.Category ?? "Unclassified")
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(t => t.Amount) / months.Count, 0));

        _histRevenue = _allTx
            .Where(t => t.IsRevenue &&
                        months.Any(m => t.Date.Year == m.Year && t.Date.Month == m.Month))
            .GroupBy(t => t.Category ?? "Unclassified")
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(t => -t.Amount) / months.Count, 0));

        // Build import options (1, 2, 3 months if contiguous data exists)
        _importOptions = [];
        for (var n = 1; n <= months.Count; n++)
        {
            var slice = months.Take(n).ToList();
            var label = n == 1
                ? slice[0].ToString("MMMM yyyy", CultureInfo.InvariantCulture)
                : $"{slice[^1].ToString("MMM", CultureInfo.InvariantCulture)} – {slice[0].ToString("MMM yyyy", CultureInfo.InvariantCulture)}";
            _importOptions.Add((n, label));
        }

        _importOptions.Reverse();
    }

    private async Task AddDraft()
    {
        var n = _drafts.Count + 1;
        _drafts.Add(new BudgetDraft { Name = $"Draft {n}" });
        _activeIdx = _drafts.Count - 1;
        await SaveAsync();
    }

    private async Task DeleteDraftAsync(int idx)
    {
        _drafts.RemoveAt(idx);
        _activeIdx = Math.Max(0, Math.Min(_activeIdx, _drafts.Count - 1));
        await SaveAsync();
    }

    private async Task ImportAsync(int months, string label)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var targetMonths = Enumerable.Range(1, months)
            .Select(i => today.AddMonths(-i))
            .ToList();

        var colorMap = _budgetCats.ToDictionary(c => c.Name, c => c.Color, StringComparer.OrdinalIgnoreCase);

        var expenses = _allTx
            .Where(t => !t.IsRevenue && t.Amount > 0 &&
                        targetMonths.Any(m => t.Date.Year == m.Year && t.Date.Month == m.Month))
            .GroupBy(t => t.Category ?? "Unclassified")
            .Select(g => new DraftRow
            {
                Name   = g.Key,
                Color  = colorMap.GetValueOrDefault(g.Key, "#594AE2"),
                Amount = Math.Round(g.Sum(t => t.Amount) / months, 0)
            })
            .OrderByDescending(r => r.Amount)
            .ToList();

        var revColorMap = _revCats.ToDictionary(c => c.Name, c => c.Color, StringComparer.OrdinalIgnoreCase);

        var revenue = _allTx
            .Where(t => t.IsRevenue &&
                        targetMonths.Any(m => t.Date.Year == m.Year && t.Date.Month == m.Month))
            .GroupBy(t => t.Category ?? "Unclassified")
            .Select(g => new DraftRow
            {
                Name   = g.Key,
                Color  = revColorMap.GetValueOrDefault(g.Key, "#4CAF50"),
                Amount = Math.Round(g.Sum(t => -t.Amount) / months, 0)
            })
            .OrderByDescending(r => r.Amount)
            .ToList();

        var draft = new BudgetDraft
        {
            Name     = $"Import {label}",
            Expenses = expenses,
            Revenue  = revenue
        };

        _drafts.Add(draft);
        _activeIdx = _drafts.Count - 1;
        await SaveAsync();
    }

    private Task SaveAsync() => PlannerSvc.SaveDraftsAsync(_drafts);

    private async Task ApplyToSettingsAsync()
    {
        var confirmed = await DialogSvc.ShowMessageBox(
            "Apply to Settings",
            $"This will replace all budget and revenue categories with the {ActiveDraft.Expenses.Count} expense and {ActiveDraft.Revenue.Count} revenue entries from \"{ActiveDraft.Name}\". Existing categories will be overwritten.",
            yesText: "Apply", cancelText: "Cancel");
        if (confirmed != true) return;

        foreach (var cat in _budgetCats)
            await BudgetSvc.DeleteAsync(cat.Id);
        foreach (var row in ActiveDraft.Expenses)
            await BudgetSvc.SaveAsync(new BudgetCategory { Name = row.Name, MonthlyAmount = row.Amount, Color = row.Color });

        foreach (var cat in _revCats)
            await RevenueSvc.DeleteAsync(cat.Id);
        foreach (var row in ActiveDraft.Revenue)
            await RevenueSvc.SaveAsync(new RevenueCategory { Name = row.Name, Color = row.Color });

        _budgetCats = await BudgetSvc.GetCategoriesAsync();
        _revCats    = await RevenueSvc.GetCategoriesAsync();

        Snackbar.Add($"\"{ActiveDraft.Name}\" applied to Settings.", Severity.Success);
    }

    private async Task OnAiDraftCreated(BudgetDraft draft)
    {
        _drafts.Add(draft);
        _activeIdx = _drafts.Count - 1;
        await SaveAsync();
    }
}
