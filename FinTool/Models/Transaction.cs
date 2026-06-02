namespace FinTool.Models;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }   // positive = expense (money out), negative = income/refund
    public string AccountType { get; set; } = "credit";   // "credit" or "debit"
    public string? Category { get; set; }
    public bool  IsConfirmed { get; set; }
    public bool  IsRevenue   { get; set; }
    public Guid? AccountId   { get; set; }
    public Guid? GoalId      { get; set; }
}
