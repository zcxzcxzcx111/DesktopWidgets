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
                    new(Color.FromArgb((byte)(alpha * 1.03), 248, 252, 255), 0),
                    new(Color.FromArgb((byte)(alpha * 0.78), 204, 221, 232), 0.48),
                    new(Color.FromArgb((byte)(alpha * 0.88), 151, 177, 195), 1)
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
        var alpha = (byte)Math.Clamp(Math.Round(255 * (0.025 + profile.Reflection * 2.85 * blur) * opacity), 0, 255);
        return IsDark(settings)
            ? new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(alpha, 26, 38, 52), 0),
                    new(Color.FromArgb((byte)(alpha * 0.70), 29, 47, 62), 0.52),
                    new(Color.FromArgb((byte)(alpha * 0.42), 17, 31, 43), 1)
                }, new Point(0.1, 0), new Point(0.94, 1))
            : new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(alpha, 239, 248, 254), 0),
                    new(Color.FromArgb((byte)(alpha * 0.70), 202, 222, 235), 0.52),
                    new(Color.FromArgb((byte)(alpha * 0.42), 162, 190, 207), 1)
                }, new Point(0.1, 0), new Point(0.94, 1));
    }

    public static double FrostDiffusionRadius(AppSettings settings) =>
        6 + Math.Clamp(settings.BlurPercent / 100d, 0, 1) * 34;

    public static Brush GlassReflectionBrush(AppSettings settings, WidgetKind kind)
    {
        var alpha = MaterialAlpha(Profile(kind).Reflection, settings);
        return new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(alpha, 123, 188, 230), 0),
                new(Color.FromArgb((byte)(alpha * 0.38), 149, 211, 218), 0.36),
                new(Color.FromArgb(0, 145, 203, 230), 1)
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
        WidgetKind.Clock => new GlassProfile(0.23, 0.31, 0.065, 0.38, 0.13),
        WidgetKind.Weather => new GlassProfile(0.32, 0.34, 0.085, 0.44, 0.16),
        WidgetKind.Calendar => new GlassProfile(0.30, 0.33, 0.075, 0.42, 0.15),
        _ => new GlassProfile(0.28, 0.32, 0.075, 0.40, 0.15)
    };

    private static byte MaterialAlpha(double alpha, AppSettings settings)
    {
        var opacity = Math.Clamp(settings.OpacityPercent / 100d, 0, 1);
        var blur = Math.Clamp(settings.BlurPercent / 100d, 0, 1);
        var blurDensity = 0.10 + blur * blur * 0.90;
        return (byte)Math.Clamp(Math.Round(255 * alpha * opacity * blurDensity), 0, 255);
    }
}
