using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopWidgets;

public sealed class ClockFaceWidget : FrameworkElement
{
    private readonly AppState _state;
    private readonly DispatcherTimer _timer;
    private DateTime _now = DateTime.Now;

    public ClockFaceWidget(AppState state)
    {
        _state = state;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _now = DateTime.Now;
            InvalidateVisual();
        };
        Loaded += (_, _) =>
        {
            _now = DateTime.Now;
            _timer.Start();
            InvalidateVisual();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var originX = (ActualWidth - size) / 2;
        var originY = (ActualHeight - size) / 2;
        var ink = WidgetTheme.PrimaryBrush(_state.Settings);
        DrawTicks(drawingContext, originX, originY, size, ink);

        var format = _state.Settings.Use24HourClock
            ? (_state.Settings.ShowSeconds ? "HH:mm:ss" : "HH:mm")
            : (_state.Settings.ShowSeconds ? "hh:mm:ss tt" : "hh:mm tt");
        var time = _now.ToString(format, CultureInfo.CurrentCulture);
        var fontSize = size * (_state.Settings.ShowSeconds || !_state.Settings.Use24HourClock ? 0.18 : 0.25);
        var typeface = new Typeface(
            new FontFamily("Bahnschrift SemiCondensed"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);
        var text = new FormattedText(
            time,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            ink,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // Center the visible glyph contours instead of the font line-height box.
        var glyphBounds = text.BuildGeometry(new Point()).Bounds;
        var textOrigin = new Point(
            originX + size / 2 - (glyphBounds.X + glyphBounds.Width / 2),
            originY + size / 2 - (glyphBounds.Y + glyphBounds.Height / 2));
        drawingContext.DrawText(text, textOrigin);
    }

    private static void DrawTicks(DrawingContext drawingContext, double x, double y, double size, Brush brush)
    {
        var inset = size * 0.095;
        var left = x + inset;
        var top = y + inset;
        var width = size - 2 * inset;
        var height = width;
        var radius = size * 0.125;
        var straightX = width - 2 * radius;
        var straightY = height - 2 * radius;
        var arc = Math.PI * radius / 2;
        var perimeter = 2 * straightX + 2 * straightY + 4 * arc;

        for (var i = 0; i < 60; i++)
        {
            var distance = perimeter * i / 60;
            var (point, inward) = PointOnRoundedPerimeter(
                distance, left, top, width, height, radius, straightX, straightY, arc);
            var major = i % 5 == 0;
            var length = size * (major ? 0.062 : 0.042);
            var pen = new Pen(brush, size * (major ? 0.0124 : 0.0079))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            drawingContext.DrawLine(pen, point, point + inward * length);
        }
    }

    private static (Point Point, Vector Inward) PointOnRoundedPerimeter(
        double distance,
        double left,
        double top,
        double width,
        double height,
        double radius,
        double straightX,
        double straightY,
        double arc)
    {
        if (distance < straightX)
            return (new Point(left + radius + distance, top), new Vector(0, 1));
        distance -= straightX;

        if (distance < arc)
            return ArcPoint(left + width - radius, top + radius, radius, -Math.PI / 2 + distance / radius);
        distance -= arc;

        if (distance < straightY)
            return (new Point(left + width, top + radius + distance), new Vector(-1, 0));
        distance -= straightY;

        if (distance < arc)
            return ArcPoint(left + width - radius, top + height - radius, radius, distance / radius);
        distance -= arc;

        if (distance < straightX)
            return (new Point(left + width - radius - distance, top + height), new Vector(0, -1));
        distance -= straightX;

        if (distance < arc)
            return ArcPoint(left + radius, top + height - radius, radius, Math.PI / 2 + distance / radius);
        distance -= arc;

        if (distance < straightY)
            return (new Point(left, top + height - radius - distance), new Vector(1, 0));
        distance -= straightY;

        return ArcPoint(left + radius, top + radius, radius, Math.PI + distance / radius);
    }

    private static (Point Point, Vector Inward) ArcPoint(
        double centerX,
        double centerY,
        double radius,
        double angle)
    {
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        return (
            new Point(centerX + radius * cosine, centerY + radius * sine),
            new Vector(-cosine, -sine));
    }
}
