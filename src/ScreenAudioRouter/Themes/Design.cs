using System.Windows;
using System.Windows.Media;

namespace ScreenAudioRouter.Themes;

/// <summary>
/// macOS-inspired palette and metrics for a calm, high-quality utility UI.
/// </summary>
public static class Design
{
    // Color tokens
    public static readonly Color WindowBg = Color.FromRgb(0xF5, 0xF5, 0xF7);
    public static readonly Color Surface = Colors.White;
    public static readonly Color SurfaceMuted = Color.FromRgb(0xEC, 0xEC, 0xEF);
    public static readonly Color Sidebar = Color.FromRgb(0xEC, 0xEC, 0xEF);
    public static readonly Color Ink = Color.FromRgb(0x1D, 0x1D, 0x1F);
    public static readonly Color InkSecondary = Color.FromRgb(0x6E, 0x6E, 0x73);
    public static readonly Color InkTertiary = Color.FromRgb(0x8E, 0x8E, 0x93);
    public static readonly Color Accent = Color.FromRgb(0x00, 0x71, 0xE3);
    public static readonly Color AccentPressed = Color.FromRgb(0x00, 0x58, 0xB0);
    public static readonly Color Success = Color.FromRgb(0x28, 0xA7, 0x45);
    public static readonly Color Warning = Color.FromRgb(0xFF, 0x9F, 0x0A);
    public static readonly Color Danger = Color.FromRgb(0xFF, 0x3B, 0x30);
    public static readonly Color Separator = Color.FromRgb(0xD2, 0xD2, 0xD7);
    public static readonly Color CardStroke = Color.FromRgb(0xE5, 0xE5, 0xEA);

    public static readonly Brush WindowBgBrush = new SolidColorBrush(WindowBg);
    public static readonly Brush SurfaceBrush = new SolidColorBrush(Surface);
    public static readonly Brush SurfaceMutedBrush = new SolidColorBrush(SurfaceMuted);
    public static readonly Brush SidebarBrush = new SolidColorBrush(Sidebar);
    public static readonly Brush InkBrush = new SolidColorBrush(Ink);
    public static readonly Brush InkSecondaryBrush = new SolidColorBrush(InkSecondary);
    public static readonly Brush InkTertiaryBrush = new SolidColorBrush(InkTertiary);
    public static readonly Brush AccentBrush = new SolidColorBrush(Accent);
    public static readonly Brush AccentPressedBrush = new SolidColorBrush(AccentPressed);
    public static readonly Brush SuccessBrush = new SolidColorBrush(Success);
    public static readonly Brush WarningBrush = new SolidColorBrush(Warning);
    public static readonly Brush DangerBrush = new SolidColorBrush(Danger);
    public static readonly Brush SeparatorBrush = new SolidColorBrush(Separator);
    public static readonly Brush CardStrokeBrush = new SolidColorBrush(CardStroke);

    // Type
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, sans-serif");
    public static readonly FontFamily FontDisplay = new("Segoe UI Variable Display, Segoe UI Semibold, Segoe UI, sans-serif");
    public static readonly FontFamily Mono = new("Cascadia Code, Consolas, monospace");

    // Metrics
    public const double RadiusS = 8;
    public const double RadiusM = 12;
    public const double RadiusL = 16;
    public const double PadS = 8;
    public const double PadM = 16;
    public const double PadL = 20;

    static Design()
    {
        foreach (var b in new Brush[]
        {
            WindowBgBrush, SurfaceBrush, SurfaceMutedBrush, SidebarBrush, InkBrush, InkSecondaryBrush,
            InkTertiaryBrush, AccentBrush, AccentPressedBrush, SuccessBrush, WarningBrush, DangerBrush,
            SeparatorBrush, CardStrokeBrush
        })
        {
            b.Freeze();
        }
    }
}
