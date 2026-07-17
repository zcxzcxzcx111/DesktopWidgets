using System.Windows;

namespace DesktopWidgets;

internal static class WidgetPlacement
{
    public const double SnapThreshold = 12;
    public const double ReleaseThreshold = 18;
    public const double GridSize = 16;
    public const double WidgetGap = 16;
    public const double SafeMargin = 16;
    public const int DropDurationMs = 180;

    internal readonly record struct Result(double Left, double Top, double? SnapX, double? SnapY);
    private readonly record struct Candidate(double Value, int Priority);

    public static Result Calculate(Rect bounds, Size size, Point raw, IReadOnlyList<Rect> others,
        double? heldX = null, double? heldY = null, Point? fallback = null)
    {
        var clamped = Clamp(bounds, size, raw);
        var x = AxisCandidates(clamped.X, bounds.Left + SafeMargin, bounds.Right - size.Width - SafeMargin,
            size.Width, others, true, heldX);
        var y = AxisCandidates(clamped.Y, bounds.Top + SafeMargin, bounds.Bottom - size.Height - SafeMargin,
            size.Height, others, false, heldY);

        foreach (var horizontal in x.OrderBy(candidate => Math.Abs(candidate.Value - clamped.X)).ThenBy(candidate => candidate.Priority))
        foreach (var vertical in y.OrderBy(candidate => Math.Abs(candidate.Value - clamped.Y)).ThenBy(candidate => candidate.Priority))
        {
            var point = Clamp(bounds, size, new Point(horizontal.Value, vertical.Value));
            if (!Collides(point, size, others))
                return new Result(point.X, point.Y,
                    horizontal.Priority < 5 ? horizontal.Value : null,
                    vertical.Priority < 5 ? vertical.Value : null);
        }

        var safeFallback = Clamp(bounds, size, fallback ?? clamped);
        return new Result(safeFallback.X, safeFallback.Y, null, null);
    }

    private static List<Candidate> AxisCandidates(double raw, double min, double max, double span,
        IReadOnlyList<Rect> others, bool horizontal, double? held)
    {
        max = Math.Max(min, max);
        var candidates = new List<Candidate>();
        void Add(double value, int priority, double threshold = SnapThreshold)
        {
            value = Math.Clamp(value, min, max);
            if (Math.Abs(raw - value) <= threshold) candidates.Add(new Candidate(value, priority));
        }

        if (held.HasValue && Math.Abs(raw - held.Value) <= ReleaseThreshold)
            candidates.Add(new Candidate(held.Value, 0));

        foreach (var other in others)
        {
            var start = horizontal ? other.Left : other.Top;
            var length = horizontal ? other.Width : other.Height;
            Add(start - span - WidgetGap, 1);
            Add(start + length + WidgetGap, 1);
            Add(start, 2);
            Add(start + length - span, 2);
            Add(start + (length - span) / 2, 2);
        }

        Add(min, 3); Add(max, 3);
        Add(min + (max - min) / 2, 4);
        Add(Math.Round(raw / GridSize) * GridSize, 5);
        if (candidates.Count == 0) candidates.Add(new Candidate(raw, 6));
        return candidates.DistinctBy(candidate => Math.Round(candidate.Value, 3)).ToList();
    }

    public static Point Clamp(Rect bounds, Size size, Point value)
    {
        var minX = bounds.Left + SafeMargin;
        var minY = bounds.Top + SafeMargin;
        return new Point(
            Math.Clamp(value.X, minX, Math.Max(minX, bounds.Right - size.Width - SafeMargin)),
            Math.Clamp(value.Y, minY, Math.Max(minY, bounds.Bottom - size.Height - SafeMargin)));
    }

    private static bool Collides(Point point, Size size, IReadOnlyList<Rect> others)
    {
        var candidate = new Rect(point.X - WidgetGap, point.Y - WidgetGap,
            size.Width + WidgetGap * 2, size.Height + WidgetGap * 2);
        return others.Any(candidate.IntersectsWith);
    }
}
