using System.Globalization;
using Microsoft.AspNetCore.Components;
using FinTool.Models;
using FinTool.Services;
using MudBlazor;

namespace FinTool.Components;

public partial class TransactionsTab
{
    [Parameter] public string? InitialMonth    { get; set; }
    [Parameter] public string? InitialCategory { get; set; }

    private List<Transaction> _confirmed = [];
    private List<Transaction> _filtered  = [];
    private List<BudgetCategory>  _categories        = [];
    private List<RevenueCategory> _revenueCategories = [];
    private List<Account>         _accounts          = [];
    private Dictionary<Guid, Account> _accountMap    = [];
    private List<Goal>            _goals             = [];
    private Dictionary<Guid, Goal> _goalMap          = [];
    private HashSet<string> _categoryFilter  = [];
    private HashSet<Guid>   _accountFilter   = [];
    private HashSet<string> _closedMonths    = [];
    private HashSet<Guid>   _selected        = [];
    private string          _searchTerm     = "";
    private bool            _initialized    = false;
    private bool            _loading        = true;
    private bool            _pendingFilter  = false;

    private bool _isMonthClosed =>
        _closedMonths.Contains(ClosedMonthService.MonthKey(MonthSvc.Month));

    private bool _allSelected  => _filtered.Count > 0 && _selected.Count == _filtered.Count;
    private bool _noneSelected => _selected.Count == 0;

    private IEnumerable<string> CategoryFilterOptions =>
        _categories.Select(c => c.Name)
            .Concat(_revenueCategories.Select(c => c.Name))
            .Append("Unclassified")
            .Distinct()
            .OrderBy(x => x);

    protected override void OnInitialized()
    {
        if (InitialMonth is not null &&
            DateOnly.TryParseExact(InitialMonth, "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedMonth))
            MonthSvc.SetMonth(parsedMonth);

        if (!string.IsNullOrEmpty(InitialCategory))
            _categoryFilter = InitialCategory.Split(',')
                .Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet();
        else if (FilterState.CategoryFilter.Count > 0)
            _categoryFilter = FilterState.CategoryFilter.ToHashSet();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(1);
            _categories        = await BudgetSvc.GetCategoriesAsync();
            _revenueCategories = await RevenueSvc.GetCategoriesAsync();
            _accounts          = await AccountSvc.GetAllAsync();
            _accountMap        = _accounts.ToDictionary(a => a.Id);
            _goals             = await GoalSvc.GetAllAsync();
            _goalMap           = _goals.ToDictionary(g => g.Id);
            _confirmed = (await TxSvc.GetAllAsync())
                .OrderByDescending(t => t.Date).ThenBy(t => t.Description)
                .ToList();
            _closedMonths = await ClosedMonthSvc.GetClosedAsync();
            SaveState();
            ApplyFilter();
            _loading     = false;
            _initialized = true;
            MonthSvc.OnChange += OnMonthChanged;
            StateHasChanged();
            return;
        }

        if (_pendingFilter)
        {
            _pendingFilter = false;
            await Task.Delay(1);
            ApplyFilter();
            _loading = false;
            StateHasChanged();
        }
    }

    protected override Task OnParametersSetAsync()
    {
        if (!_initialized) return base.OnParametersSetAsync();

        if (InitialMonth is not null &&
            DateOnly.TryParseExact(InitialMonth, "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var pm))
            MonthSvc.SetMonth(pm);

        var newCats = string.IsNullOrEmpty(InitialCategory)
            ? new HashSet<string>()
            : InitialCategory.Split(',').Select(c => c.Trim())
                .Where(c => c.Length > 0).ToHashSet();

        if (newCats.Count > 0 && !newCats.SetEquals(_categoryFilter))
        {
            _categoryFilter = newCats;
            SaveState();
            ApplyFilter();
        }

        return base.OnParametersSetAsync();
    }

    private void OnMonthChanged()
    {
        UpdateUrl();
        _loading       = true;
        _pendingFilter = true;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() => MonthSvc.OnChange -= OnMonthChanged;

    private void ApplyFilter()
    {
        var q = _confirmed
            .Where(t => t.Date.Year == MonthSvc.Month.Year && t.Date.Month == MonthSvc.Month.Month);
        if (_categoryFilter.Count > 0)
            q = q.Where(t => _categoryFilter.Contains(t.Category ?? "Unclassified"));
        if (_accountFilter.Count > 0)
            q = q.Where(t => t.AccountId.HasValue && _accountFilter.Contains(t.AccountId.Value));
        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            var term = _searchTerm.Trim().ToLowerInvariant();
            q = q.Where(t =>
                t.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture).ToLowerInvariant().Contains(term) ||
                t.Description.ToLowerInvariant().Contains(term) ||
                (t.Category?.ToLowerInvariant().Contains(term) ?? false) ||
                FormatAmount(t.Amount).ToLowerInvariant().Contains(term) ||
                t.AccountType.ToLowerInvariant().Contains(term));
        }
        _filtered = q.ToList();
        _selected.IntersectWith(_filtered.Select(t => t.Id));
    }

    private void OnSearchChanged(string value)
    {
        _searchTerm = value ?? "";
        ApplyFilter();
    }

    private void SaveState() => FilterState.CategoryFilter = _categoryFilter.ToHashSet();

    private void UpdateUrl()
    {
        var cats = string.Join(",", _categoryFilter.OrderBy(x => x));
        var url  = $"/transactions?month={MonthSvc.Month:yyyy-MM}";
        if (cats.Length > 0) url += $"&category={Uri.EscapeDataString(cats)}";
        Nav.NavigateTo(url, new NavigationOptions { ReplaceHistoryEntry = true });
    }

    private void ToggleCategoryFilter(string name, bool selected)
    {
        if (selected) _categoryFilter.Add(name);
        else _categoryFilter.Remove(name);
        SaveState(); UpdateUrl(); ApplyFilter();
    }

    private void ToggleAccountFilter(Guid id, bool selected)
    {
        if (selected) _accountFilter.Add(id);
        else _accountFilter.Remove(id);
        ApplyFilter();
    }

    private async Task CloseMonthAsync()
    {
        var confirm = await DialogSvc.ShowMessageBox(
            "Close Month",
            $"Lock all transactions for {MonthSvc.Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}? " +
            "This will prevent any further edits to this month.",
            yesText: "Close Month", noText: "Cancel");

        if (confirm == true)
        {
            await ClosedMonthSvc.CloseMonthAsync(MonthSvc.Month);
            _closedMonths = await ClosedMonthSvc.GetClosedAsync();
        }
    }

    private async Task OpenImportAsync()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true };
        var dialog = await DialogSvc.ShowAsync<ImportDialog>("", options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            _confirmed = (await TxSvc.GetAllAsync())
                .OrderByDescending(t => t.Date).ThenBy(t => t.Description)
                .ToList();
            ApplyFilter();
        }
    }

    private void ToggleSelect(Transaction tx)
    {
        if (!_selected.Remove(tx.Id))
            _selected.Add(tx.Id);
    }

    private void ToggleSelectAll()
    {
        if (_allSelected)
            _selected.Clear();
        else
            _selected = _filtered.Select(t => t.Id).ToHashSet();
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selected.Count == 0 || _isMonthClosed) return;

        var confirm = await DialogSvc.ShowMessageBox(
            "Delete transactions",
            $"Permanently delete {_selected.Count} selected transaction(s)?",
            yesText: "Delete", noText: "Cancel");

        if (confirm != true) return;

        await TxSvc.DeleteBatchAsync(_selected);

        _confirmed.RemoveAll(t => _selected.Contains(t.Id));
        _selected.Clear();
        ApplyFilter();
    }

    private async Task DeleteAsync(Transaction tx)
    {
        if (_isMonthClosed) return;
        await TxSvc.DeleteAsync(tx.Id);
        _confirmed.RemoveAll(t => t.Id == tx.Id);
        ApplyFilter();
    }

    private async Task UpdateAccountAsync(Transaction tx, Guid? accountId)
    {
        if (_isMonthClosed) return;
        tx.AccountId = accountId;
        await TxSvc.UpdateAsync(tx);
    }

    private async Task UpdateGoalAsync(Transaction tx, Guid? goalId)
    {
        if (_isMonthClosed) return;
        tx.GoalId = goalId;
        await TxSvc.UpdateAsync(tx);
    }

    private async Task UpdateCategoryAsync(Transaction tx, string? newCategory)
    {
        if (_isMonthClosed) return;
        tx.Category = newCategory;
        await TxSvc.UpdateAsync(tx);
        if (!string.IsNullOrEmpty(newCategory))
            await CacheSvc.SetCategoryAsync(tx.Description, newCategory);
    }

    private static Color AmountColor(decimal amount) =>
        amount >= 0 ? Color.Error : Color.Success;

    private static string FormatAmount(decimal amount) =>
        amount >= 0 ? $"${amount:N2}" : $"+${Math.Abs(amount):N2}";
}
