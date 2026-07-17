using System.Windows;
using System.Windows.Media;

namespace DesktopWidgets;

public static class WidgetTheme
{
    private readonly record struct GlassProfile(double Fill, double Highlight, double Reflection, double Edge, double Shadow);

    public static readonly FontFamily UiFont = new("Segoe UI Variable, Microsoft YaHei UI");

    public static bool IsDark(AppSettings settings)
    {
        if (settings.Theme == "Dark") return true;
        if (settings.Theme == "Light") return false;
        return Microsoft.Win32.Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is int value && value == 0;
    }

    public static Brush CardBrush(AppSettings settings, WidgetKind kind)
    {
        var profile = Profile(kind);
        var alpha = MaterialAlpha(profile.Fill, settings);
        return IsDark(settings)
            ? new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb((byte)(alpha * 1.02), 57, 72, 90), 0),
                    new(Color.FromArgb((byte)(alpha * 0.76), 25, 34, 46), 0.48),
                    new(Color.FromArgb((byte)(alpha * 0.86), 17, 25, 36), 1)
                }, new Point(0.04, 0), new Point(0.96, 1))
            : new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb((byte)(alpha * 1.03), 237, 246, 249), 0),
                    new(Color.FromArgb((byte)(alpha * 0.78), 195, 214, 222), 0.48),
                    new(Color.FromArgb((byte)(alpha * 0.88), 146, 170, 183), 1)
                }, new Point(0.04, 0), new Point(0.96, 1));
    }

    public static Brush GlassHighlightBrush(AppSettings settings, WidgetKind kind)
    {
        var alpha = MaterialAlpha(Profile(kind).Highlight, settings);
        return new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(alpha, 255, 255, 255), 0),
                new(Color.FromArgb((byte)(alpha * 0.34), 232, 246, 255), 0.43),
                new(Color.FromArgb(0, 255, 255, 255), 1)
            })
        {
            Center = new Point(0.14, 0.02),
            GradientOrigin = new Point(0.14, 0.02),
            RadiusX = 0.98,
            RadiusY = 0.74
        };
    }

    public static Brush FrostDiffusionBrush(AppSettings settings, WidgetKind kind)
    {
        var profile = Profile(kind);
        var blur = Math.Clamp(settings.BlurPercent / 100d, 0, 1);
        var opacity = Math.Clamp(settings.OpacityPercent / 100d, 0, 1);
        var alpha = (byte)Math.Clamp(Math.Round(255 * (0.008 + profile.Reflection * 0.50 * blur) * opacity), 0, 255);
        return IsDark(settings)
            ? new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(alpha, 30, 42, 54), 0),
                    new(Color.FromArgb((byte)(alpha * 0.62), 31, 44, 55), 0.52),
                    new(Color.FromArgb((byte)(alpha * 0.30), 24, 35, 45), 1)
                }, new Point(0.1, 0), new Point(0.94, 1))
            : new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(alpha, 229, 240, 242), 0),
                    new(Color.FromArgb((byte)(alpha * 0.62), 198, 214, 218), 0.52),
                    new(Color.FromArgb((byte)(alpha * 0.30), 169, 187, 192), 1)
                }, new Point(0.1, 0), new Point(0.94, 1));
    }

    public static double FrostDiffusionRadius(AppSettings settings) =>
        2 + Math.Clamp(settings.BlurPercent / 100d, 0, 1) * 12;

    public static Brush GlassReflectionBrush(AppSettings settings, WidgetKind kind)
    {
        var alpha = MaterialAlpha(Profile(kind).Reflection, settings);
        return new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(alpha, 150, 184, 200), 0),
                new(Color.FromArgb((byte)(alpha * 0.38), 165, 202, 204), 0.36),
                new(Color.FromArgb(0, 158, 191, 202), 1)
            })
        {
            Center = new Point(0.91, 0.87),
            GradientOrigin = new Point(0.91, 0.87),
            RadiusX = 0.86,
            RadiusY = 0.70
        };
    }

    public static Brush GlassEdgeBrush(AppSettings settings, WidgetKind kind)
    {
        var alpha = MaterialAlpha(Profile(kind).Edge, settings);
        return new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(alpha, 255, 255, 255), 0),
                new(Color.FromArgb((byte)(alpha * 0.56), 223, 242, 251), 0.43),
                new(Color.FromArgb((byte)(alpha * 0.32), 202, 225, 239), 1)
            }, new Point(0, 0), new Point(1, 1));
    }

    public static double ShadowOpacity(AppSettings settings, WidgetKind kind, bool active)
    {
        var baseOpacity = Profile(kind).Shadow * Math.Clamp(settings.OpacityPercent / 100d, 0.2, 1);
        return Math.Clamp(baseOpacity + (active ? 0.035 : 0), 0.08, 0.20);
    }

    public static Color ShadowColor(AppSettings settings) =>
        IsDark(settings) ? Color.FromRgb(7, 14, 25) : Color.FromRgb(15, 23, 42);

    public static Brush PrimaryBrush(AppSettings settings) =>
        new SolidColorBrush(IsDark(settings) ? Color.FromArgb(248, 241, 247, 246) : Color.FromArgb(244, 24, 40, 43));

    public static Brush SecondaryBrush(AppSettings settings) =>
        new SolidColorBrush(IsDark(settings) ? Color.FromArgb(205, 207, 223, 224) : Color.FromArgb(196, 42, 62, 67));

    public static Brush AccentBrush(AppSettings settings) =>
        new SolidColorBrush(IsDark(settings) ? Color.FromRgb(207, 235, 248) : Color.FromRgb(224, 242, 253));

    public static double RadiusValue(WidgetSize size) => size switch
    {
        WidgetSize.Small => 30,
        WidgetSize.Medium => 37,
        _ => 77
    };

    public static CornerRadius Radius(WidgetSize size) => new(RadiusValue(size));

    private static GlassProfile Profile(WidgetKind kind) => kind switch
    {
        WidgetKind.Clock => new GlassProfile(0.17, 0.28, 0.040, 0.35, 0.12),
        WidgetKind.Weather => new GlassProfile(0.21, 0.30, 0.055, 0.38, 0.14),
        WidgetKind.Calendar => new GlassProfile(0.20, 0.29, 0.050, 0.37, 0.13),
        _ => new GlassProfile(0.19, 0.29, 0.050, 0.36, 0.13)
    };

    private static byte MaterialAlpha(double alpha, AppSettings settings)
    {
        var opacity = Math.Clamp(settings.OpacityPercent / 100d, 0, 1);
        var blur = Math.Clamp(settings.BlurPercent / 100d, 0, 1);
        var blurDensity = 0.40 + blur * 0.60;
        return (byte)Math.Clamp(Math.Round(255 * alpha * opacity * blurDensity), 0, 255);
    }
}
