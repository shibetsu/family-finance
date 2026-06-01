namespace FinTool.Services;

public class TransactionFilterState
{
    public DateOnly?      Month          { get; set; }
    public HashSet<string> CategoryFilter { get; set; } = [];
}
