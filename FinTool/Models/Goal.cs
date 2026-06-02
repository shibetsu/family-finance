namespace FinTool.Models;

public class Goal
{
    public Guid    Id            { get; set; } = Guid.NewGuid();
    public string  Name          { get; set; } = "";
    public string  Color         { get; set; } = "#594AE2";
    public decimal TargetAmount  { get; set; }
    public decimal CurrentAmount { get; set; }
    public string  Notes         { get; set; } = "";
}
