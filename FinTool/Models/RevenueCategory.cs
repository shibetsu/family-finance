namespace FinTool.Models;

public class RevenueCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4CAF50";
    public bool IsIgnored { get; set; }
}
