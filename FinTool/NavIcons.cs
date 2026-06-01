namespace FinTool;

public static class NavIcons
{
    private static string Box(char letter) =>
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>" +
        "<rect x='1.5' y='1.5' width='21' height='21' rx='5' ry='5' fill='#0a0a0a'/>" +
        $"<text x='12' y='17' font-family='Georgia,serif' font-size='14' font-weight='700' text-anchor='middle' fill='#ffffff'>{letter}</text>" +
        "</svg>";

    public static readonly string Dashboard    = Box('D');
    public static readonly string Transactions = Box('T');
    public static readonly string Budget       = Box('B');
    public static readonly string Revenue      = Box('R');
    public static readonly string Planner      = Box('P');
}
