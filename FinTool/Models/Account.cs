namespace FinTool.Models;

public class Account
{
    public Guid   Id    { get; set; } = Guid.NewGuid();
    public string Name  { get; set; } = "";
    public string Type  { get; set; } = "credit";
    public string Color { get; set; } = "#594AE2";
}
