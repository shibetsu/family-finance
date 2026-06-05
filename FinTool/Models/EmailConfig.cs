namespace FinTool.Models;

public class EmailConfig
{
    public string FromAddress { get; set; } = "";
    public string FromName    { get; set; } = "Family Finance";
    public string AppBaseUrl  { get; set; } = "http://localhost:5111";
}
