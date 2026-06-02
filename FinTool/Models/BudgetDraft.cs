namespace FinTool.Models;

public class BudgetDraft
{
    public Guid          Id       { get; set; } = Guid.NewGuid();
    public string        Name     { get; set; } = "Draft";
    public List<DraftRow> Expenses { get; set; } = [];
    public List<DraftRow> Revenue  { get; set; } = [];
    public string        Notes    { get; set; } = "";
}

public class DraftRow
{
    public string  Name   { get; set; } = "";
    public string  Color  { get; set; } = "#594AE2";
    public decimal Amount { get; set; }
}
