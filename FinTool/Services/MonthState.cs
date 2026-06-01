namespace FinTool.Services;

public class MonthState
{
    private DateOnly _month;

    public DateOnly CurrentMonth { get; }
    public DateOnly Month => _month;

    public event Action? OnChange;

    public MonthState()
    {
        var today    = DateOnly.FromDateTime(DateTime.Now);
        CurrentMonth = new DateOnly(today.Year, today.Month, 1);
        _month       = CurrentMonth;
    }

    public void SetMonth(DateOnly month)
    {
        if (month == _month) return;
        _month = month;
        OnChange?.Invoke();
    }

    public void PrevMonth() => SetMonth(_month.AddMonths(-1));

    public void NextMonth()
    {
        if (_month < CurrentMonth)
            SetMonth(_month.AddMonths(1));
    }
}
