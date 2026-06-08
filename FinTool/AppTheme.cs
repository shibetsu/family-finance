using MudBlazor;

namespace FinTool;

public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary          = "#0a0a0a",
            Secondary        = "#757575",
            AppbarBackground = "#ffffff",
            AppbarText       = "#0a0a0a",
            Background       = "#f5f5f5",
            Surface          = "#ffffff",
            DrawerBackground = "#efefef",
            DrawerText       = "#0a0a0a",
            DrawerIcon       = "#0a0a0a",
            TextPrimary      = "#0a0a0a",
            TextSecondary    = "#5c5c5c",
            Divider          = "#e0e0e0",
            ActionDefault    = "#424242",
            TableLines       = "#e0e0e0",
            TableHover       = "#f5f5f5",
        },
        PaletteDark = new PaletteDark
        {
            Primary              = "#ffffff",
            PrimaryContrastText  = "#0a0a0a",
            Secondary            = "#9e9e9e",
            AppbarBackground = "#0d0d0d",
            AppbarText       = "#f0f0f0",
            Background       = "#0d0d0d",
            BackgroundGray   = "#111111",
            Surface          = "#1a1a1a",
            DrawerBackground = "#111111",
            DrawerText       = "#f0f0f0",
            DrawerIcon       = "#f0f0f0",
            TextPrimary      = "#f0f0f0",
            TextSecondary    = "#a0a0a0",
            Divider          = "#2a2a2a",
            ActionDefault    = "#909090",
            TableLines       = "#2a2a2a",
            TableHover       = "#1a1a1a",
        },
        Typography = new Typography
        {
            Default   = new Default   { FontFamily = ["Inter", "system-ui", "sans-serif"] },
            H1        = new H1        { FontFamily = ["Playfair Display", "Georgia", "serif"], FontWeight = 700 },
            H2        = new H2        { FontFamily = ["Playfair Display", "Georgia", "serif"], FontWeight = 700 },
            H3        = new H3        { FontFamily = ["Playfair Display", "Georgia", "serif"], FontWeight = 600 },
            H4        = new H4        { FontFamily = ["Playfair Display", "Georgia", "serif"], FontWeight = 600 },
            H5        = new H5        { FontFamily = ["Playfair Display", "Georgia", "serif"], FontWeight = 600 },
            H6        = new H6        { FontFamily = ["Inter", "system-ui", "sans-serif"],     FontWeight = 600 },
            Subtitle1 = new Subtitle1 { FontFamily = ["Inter", "system-ui", "sans-serif"], FontWeight = 500 },
            Subtitle2 = new Subtitle2 { FontFamily = ["Inter", "system-ui", "sans-serif"], FontWeight = 500 },
            Button    = new MudBlazor.Button
            {
                FontFamily    = ["Inter", "system-ui", "sans-serif"],
                FontWeight    = 500,
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft     = "268px",
            DefaultBorderRadius = "12px"
        }
    };
}
