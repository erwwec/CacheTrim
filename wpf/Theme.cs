using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace CacheTrim;

internal static class Theme
{
    public const string Indigo = "Indigo";
    public const string Ocean = "Ocean";
    public const string Graphite = "Graphite";
    public const string LiquidGlass = "LiquidGlass";
    public const string Violet = "Violet";
    public const string Rose = "Rose";

    public static Color Bg { get; private set; } = Color.FromRgb(15, 17, 19);
    public static Color Surface { get; private set; } = Color.FromRgb(28, 30, 33);
    public static Color SurfaceAlt { get; private set; } = Color.FromRgb(38, 40, 43);
    public static Color Line { get; private set; } = Color.FromRgb(55, 58, 62);
    public static Color Text { get; private set; } = Color.FromRgb(245, 245, 245);
    public static Color Muted { get; private set; } = Color.FromRgb(174, 177, 181);
    public static Color Accent { get; private set; } = Color.FromRgb(96, 205, 255);
    public static bool IsGlass { get; private set; }

    public static void Apply(string name, string accentHex)
    {
        IsGlass = name == LiquidGlass;
        if (name == Ocean)
        {
            Bg = Color.FromRgb(13, 18, 23); Surface = Color.FromRgb(21, 28, 34); SurfaceAlt = Color.FromRgb(30, 40, 48);
            Line = Color.FromRgb(43, 57, 68); Text = Color.FromRgb(239, 245, 249); Muted = Color.FromRgb(160, 178, 190); Accent = Color.FromRgb(78, 168, 255);
        }
        else if (name == Violet)
        {
            Bg = Color.FromRgb(16, 15, 23); Surface = Color.FromRgb(27, 25, 37); SurfaceAlt = Color.FromRgb(39, 35, 52);
            Line = Color.FromRgb(57, 51, 74); Text = Color.FromRgb(246, 243, 252); Muted = Color.FromRgb(184, 176, 201); Accent = Color.FromRgb(180, 153, 255);
        }
        else if (name == Rose)
        {
            Bg = Color.FromRgb(22, 15, 19); Surface = Color.FromRgb(34, 24, 30); SurfaceAlt = Color.FromRgb(48, 34, 42);
            Line = Color.FromRgb(68, 48, 58); Text = Color.FromRgb(250, 242, 246); Muted = Color.FromRgb(196, 174, 184); Accent = Color.FromRgb(255, 143, 183);
        }
        else if (name == Graphite || name == LiquidGlass)
        {
            Bg = Color.FromRgb(15, 17, 19); Surface = Color.FromRgb(28, 30, 33); SurfaceAlt = Color.FromRgb(38, 40, 43);
            Line = Color.FromRgb(55, 58, 62); Text = Color.FromRgb(245, 245, 245); Muted = Color.FromRgb(174, 177, 181);
            Accent = name == LiquidGlass ? Color.FromRgb(143, 203, 255) : Color.FromRgb(96, 205, 255);
        }
        else
        {
            Bg = Color.FromRgb(15, 17, 19); Surface = Color.FromRgb(28, 30, 33); SurfaceAlt = Color.FromRgb(38, 40, 43);
            Line = Color.FromRgb(55, 58, 62); Text = Color.FromRgb(245, 245, 245); Muted = Color.FromRgb(174, 177, 181); Accent = Color.FromRgb(96, 205, 255);
        }
        if (TryHex(accentHex, out Color custom)) Accent = custom;
    }

    public static bool IsGlassName(string value) => value == LiquidGlass;
    public static int ToIndex(string value) => value == Ocean ? 1 : value == Graphite ? 2 : value == LiquidGlass ? 3 : value == Violet ? 4 : value == Rose ? 5 : 0;
    public static string FromIndex(int index) => index == 1 ? Ocean : index == 2 ? Graphite : index == 3 ? LiquidGlass : index == 4 ? Violet : index == 5 ? Rose : Indigo;
    public static string DefaultAccentHex(string value) => value == Ocean ? "#4EA8FF" : value == LiquidGlass ? "#8FCBFF" : value == Violet ? "#B499FF" : value == Rose ? "#FF8FB7" : "#60CDFF";

    public static bool TryHex(string? text, out Color color)
    {
        color = Accent;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().TrimStart('#');
        if (text.Length != 6 || !byte.TryParse(text.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) ||
            !byte.TryParse(text.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) ||
            !byte.TryParse(text.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b)) return false;
        color = Color.FromRgb(r, g, b);
        return true;
    }
}
