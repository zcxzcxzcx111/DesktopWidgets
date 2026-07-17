using System.Windows;
using System.Windows.Media;

namespace DesktopWidgets;

public static class WidgetTheme
{
    public static readonly FontFamily UiFont = new("Segoe UI Variable, Microsoft YaHei UI");

    public static bool IsDark(AppSettings settings)
    {
        if (settings.Theme == "Dark") return true;
        if (settings.Theme == "Light") return false;
        return Microsoft.Win32.Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is int value && value == 0;
    }

    public static Brush CardBrush(AppSettings settings)
    {
        var opacity = settings.OpacityPercent / 100d;
        var alpha = (byte)Math.Clamp(opacity * 190, 0, 224);
        return IsDark(settings)
            ? new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb((byte)(alpha * 0.96), 74, 96, 117), 0),
                    new(Color.FromArgb((byte)(alpha * 0.72), 37, 54, 72), 0.42),
                    new(Color.FromArgb((byte)(alpha * 0.88), 24, 37, 54), 1)
                }, new Point(0.04, 0), new Point(0.96, 1))
            : new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb((byte)(alpha * 0.88), 225, 239, 249), 0),
                    new(Color.FromArgb((byte)(alpha * 0.62), 151, 180, 204), 0.46),
                    new(Color.FromArgb((byte)(alpha * 0.80), 104, 133, 160), 1)
                }, new Point(0.04, 0), new Point(0.96, 1));
    }

    public static Brush GlassHighlightBrush(AppSettings settings) => new RadialGradientBrush(
        new GradientStopCollection
        {
            new(IsDark(settings) ? Color.FromArgb(78, 232, 246, 255) : Color.FromArgb(106, 255, 255, 255), 0),
            new(IsDark(settings) ? Color.FromArgb(24, 190, 218, 242) : Color.FromArgb(30, 231, 247, 255), 0.46),
            new(Color.FromArgb(0, 255, 255, 255), 1)
        })
    {
        Center = new Point(0.16, 0.04),
        GradientOrigin = new Point(0.16, 0.04),
        RadiusX = 0.96,
        RadiusY = 0.78
    };

    public static Brush GlassReflectionBrush(AppSettings settings) => new LinearGradientBrush(
        new GradientStopCollection
        {
            new(IsDark(settings) ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(58, 255, 255, 255), 0),
            new(IsDark(settings) ? Color.FromArgb(16, 230, 245, 255) : Color.FromArgb(24, 255, 255, 255), 0.22),
            new(Color.FromArgb(0, 255, 255, 255), 0.58)
        }, new Point(0, 0), new Point(0.84, 0.58));

    public static Brush GlassEdgeBrush(AppSettings settings) => new LinearGradientBrush(
        new GradientStopCollection
        {
            new(IsDark(settings) ? Color.FromArgb(156, 232, 247, 255) : Color.FromArgb(188, 255, 255, 255), 0),
            new(IsDark(settings) ? Color.FromArgb(76, 168, 206, 231) : Color.FromArgb(96, 221, 239, 251), 0.5),
            new(IsDark(settings) ? Color.FromArgb(110, 202, 224, 242) : Color.FromArgb(128, 242, 250, 255), 1)
        }, new Point(0, 0), new Point(1, 1));

    public static Color ShadowColor(AppSettings settings) =>
        IsDark(settings) ? Color.FromArgb(118, 2, 10, 20) : Color.FromArgb(82, 18, 38, 62);

    public static Brush PrimaryBrush(AppSettings settings) =>
        new SolidColorBrush(IsDark(settings) ? Color.FromRgb(241, 247, 246) : Color.FromRgb(27, 46, 48));

    public static Brush SecondaryBrush(AppSettings settings) =>
        new SolidColorBrush(IsDark(settings) ? Color.FromRgb(184, 202, 200) : Color.FromRgb(79, 99, 101));

    public static Brush AccentBrush(AppSettings settings) =>
        new SolidColorBrush(IsDark(settings) ? Color.FromRgb(207, 235, 248) : Color.FromRgb(224, 242, 253));

    public static double RadiusValue(WidgetSize size) => size switch
    {
        WidgetSize.Small => 30,
        WidgetSize.Medium => 37,
        _ => 77
    };

    public static CornerRadius Radius(WidgetSize size) => new(RadiusValue(size));
}
