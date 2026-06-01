namespace FinTool.Models;

public class BudgetCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal MonthlyAmount { get; set; }
    public string Color { get; set; } = "#594AE2";
    public bool IsIgnored { get; set; }
}
