using System.Globalization;
using Microsoft.AspNetCore.Components;
using FinTool.Models;
using FinTool.Services;
using MudBlazor;

namespace FinTool.Components;

public partial class DashboardTab
{
    [Parameter] public string? InitialMonth { get; set; }

    private static readonly CultureInfo _cad = CultureInfo.GetCultureInfo("en-CA");

    private bool _initialized    = false;
    private bool _loading        = true;
    private bool _pendingCompute = false;

    private List<BudgetCategory>  _categories        = [];
    private List<RevenueCategory> _revenueCategories = [];
    private List<Transaction>     _allTx             = [];

    private bool    _hasData;
    private decimal _totalBudget;
    private decimal _totalSpent;
    private decimal _totalRevenue;
    private int     _txCount;

    private List<CatRow>      _categoryRows = [];
    private List<RevRow>      _revenueRows  = [];
    private List<Transaction> _ignoredTx    = [];
    private decimal           _ignoredExpenseTotal;
    private decimal           _ignoredRevenueTotal;
    private bool              _showIgnored = true;
    private Dictionary<string,string> _categoryColorMap = [];

    private double[]          _donutData          = [];
    private string[]          _donutLabels        = [];
    private ChartOptions      _donutOptions       = new();
    private int               _selectedDonutIndex = 0;
    private List<ChartSeries> _trendSeries        = [];
    private string[]          _trendLabels  = [];
    private ChartOptions      _trendOptions = new();

    protected override void OnInitialized()
    {
        if (InitialMonth is not null &&
            DateOnly.TryParseExact(InitialMonth, "yyyy-MM",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            MonthSvc.SetMonth(parsed);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(1);
            _categories        = await BudgetSvc.GetCategoriesAsync();
            _revenueCategories = await RevenueSvc.GetCategoriesAsync();
            _allTx             = await TxSvc.GetAllAsync();
            Compute();
            _loading     = false;
            _initialized = true;
            MonthSvc.OnChange += OnMonthChanged;
            StateHasChanged();
            return;
        }

        if (_pendingCompute)
        {
            _pendingCompute = false;
            await Task.Delay(1);
            Compute();
            _loading = false;
            StateHasChanged();
        }
    }

    protected override Task OnParametersSetAsync()
    {
        if (!_initialized) return base.OnParametersSetAsync();

        if (InitialMonth is not null &&
            DateOnly.TryParseExact(InitialMonth, "yyyy-MM",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            MonthSvc.SetMonth(parsed);

        return base.OnParametersSetAsync();
    }

    private void OnMonthChanged()
    {
        UpdateUrl();
        _loading        = true;
        _pendingCompute = true;
        InvokeAsync(StateHasChanged);
    }

    private void UpdateUrl() =>
        Nav.NavigateTo($"/?month={MonthSvc.Month:yyyy-MM}",
            new NavigationOptions { ReplaceHistoryEntry = true });

    public void Dispose() => MonthSvc.OnChange -= OnMonthChanged;

    private void Compute()
    {
        var monthTx = _allTx
            .Where(t => t.Date.Year == MonthSvc.Month.Year && t.Date.Month == MonthSvc.Month.Month)
            .ToList();

        // Ignored category name sets — these are excluded from all totals and charts
        var ignoredBudget  = _categories.Where(c => c.IsIgnored)
            .Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignoredRevenue = _revenueCategories.Where(c => c.IsIgnored)
            .Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build a color map for ignored-transaction rows
        _categoryColorMap = _categories
            .ToDictionary(c => c.Name, c => c.Color, StringComparer.OrdinalIgnoreCase);
        foreach (var rc in _revenueCategories)
            _categoryColorMap.TryAdd(rc.Name, rc.Color);

        // Collect ignored transactions for display
        _ignoredTx = monthTx
            .Where(t =>
                (!t.IsRevenue && t.Amount > 0 && ignoredBudget.Contains(t.Category ?? "")) ||
                (t.IsRevenue && ignoredRevenue.Contains(t.Category ?? "")))
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Description)
            .ToList();
        _ignoredExpenseTotal = _ignoredTx.Where(t => !t.IsRevenue).Sum(t => t.Amount);
        _ignoredRevenueTotal = _ignoredTx.Where(t => t.IsRevenue).Sum(t => -t.Amount);

        _txCount      = monthTx.Count;
        _totalBudget  = _categories.Where(c => !c.IsIgnored).Sum(c => c.MonthlyAmount);
        _totalSpent   = monthTx
            .Where(t => t.Amount > 0 && !t.IsRevenue && !ignoredBudget.Contains(t.Category ?? ""))
            .Sum(t => t.Amount);
        _totalRevenue = monthTx
            .Where(t => t.IsRevenue && !ignoredRevenue.Contains(t.Category ?? ""))
            .Sum(t => -t.Amount);
        _hasData      = monthTx.Count > 0;

        // Expense category rows (non-ignored only)
        _categoryRows = _categories
            .Where(c => !c.IsIgnored)
            .Select(cat =>
            {
                var txs = monthTx
                    .Where(t => !t.IsRevenue && t.Amount > 0 &&
                                string.Equals(t.Category, cat.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var spent = txs.Sum(t => t.Amount);
                var count = txs.Count;
                return new CatRow(cat.Name, cat.Color, cat.MonthlyAmount, spent, count, count > 0 ? spent / count : 0);
            })
            .OrderByDescending(r => r.Spent)
            .ToList();

        // Revenue breakdown rows (non-ignored only)
        var uncategorisedRevenue = monthTx
            .Where(t => t.IsRevenue && string.IsNullOrEmpty(t.Category))
            .Sum(t => -t.Amount);

        _revenueRows = _revenueCategories
            .Where(c => !c.IsIgnored)
            .Select(cat =>
            {
                var txs = monthTx
                    .Where(t => t.IsRevenue &&
                                string.Equals(t.Category, cat.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var amount = txs.Sum(t => -t.Amount);
                var count  = txs.Count;
                return new RevRow(cat.Name, cat.Color, amount, count, count > 0 ? amount / count : 0);
            })
            .Where(r => r.Amount > 0)
            .ToList();

        if (uncategorisedRevenue > 0)
        {
            var uncatCount = monthTx.Count(t => t.IsRevenue && string.IsNullOrEmpty(t.Category));
            _revenueRows.Add(new RevRow("Unclassified", "#9E9E9E", uncategorisedRevenue,
                uncatCount, uncatCount > 0 ? uncategorisedRevenue / uncatCount : 0));
        }

        // Donut: categories with spending
        var donut = _categoryRows.Where(r => r.Spent > 0).ToList();
        _donutData   = donut.Select(r => (double)r.Spent).ToArray();
        _donutLabels = donut.Select(r => r.Name).ToArray();
        _donutOptions = new ChartOptions
        {
            ChartPalette = donut.Select(r => r.Color).ToArray(),
            ShowLegend   = false
        };

        // Trend: last 6 months, expenses AND revenue
        _trendLabels = Enumerable.Range(0, 6)
            .Select(i => MonthSvc.Month.AddMonths(i - 5).ToString("MMM yy", CultureInfo.InvariantCulture))
            .ToArray();

        _trendSeries =
        [
            new ChartSeries
            {
                Name = "Expenses",
                Data = Enumerable.Range(0, 6).Select(i =>
                {
                    var m = MonthSvc.Month.AddMonths(i - 5);
                    return (double)_allTx
                        .Where(t => t.Date.Year == m.Year && t.Date.Month == m.Month
                                 && t.Amount > 0 && !t.IsRevenue)
                        .Sum(t => t.Amount);
                }).ToArray()
            },
            new ChartSeries
            {
                Name = "Revenue",
                Data = Enumerable.Range(0, 6).Select(i =>
                {
                    var m = MonthSvc.Month.AddMonths(i - 5);
                    return (double)_allTx
                        .Where(t => t.Date.Year == m.Year && t.Date.Month == m.Month && t.IsRevenue)
                        .Sum(t => -t.Amount);
                }).ToArray()
            }
        ];
        _trendOptions = new ChartOptions
        {
            ChartPalette        = ["#594AE2", "#4CAF50"],
            InterpolationOption = InterpolationOption.NaturalSpline,
            YAxisFormat         = "C0"
        };
    }

    private void GoToCategory(string categoryName)
    {
        var month    = MonthSvc.Month.ToString("yyyy-MM");
        var category = Uri.EscapeDataString(categoryName);
        Nav.NavigateTo($"/transactions?month={month}&category={category}");
    }

    private static string Fmt(decimal v) =>
        v.ToString("C0", CultureInfo.GetCultureInfo("en-CA"));

    private record CatRow(string Name, string Color, decimal Budget, decimal Spent, int Count, decimal Avg);
    private record RevRow(string Name, string Color, decimal Amount, int Count, decimal Avg);
}
