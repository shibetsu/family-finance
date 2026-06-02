namespace FinTool;

public static class ColorPalette
{
    public static readonly string[] Soft =
    [
        "#E07878", // coral
        "#E89060", // terracotta
        "#D4B860", // amber
        "#7CC870", // sage
        "#50B898", // seafoam
        "#5898D0", // cornflower
        "#7080D0", // periwinkle
        "#A870C8", // lavender
        "#D070A0", // rose
        "#7898A8"  // slate
    ];

    public static string Random() => Soft[System.Random.Shared.Next(Soft.Length)];
}
