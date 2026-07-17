using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace DesktopWidgets;

public sealed class DesktopWidgetManager : IDisposable
{
    private const double Gap = WidgetPlacement.WidgetGap;
    private readonly AppState _state = new();
    private readonly Dictionary<WidgetKind, DesktopWidgetWindow> _windows = [];
    private NotifyIcon? _tray;
    private SettingsWindow? _settingsWindow;
    public bool IsEditMode { get; private set; }

    public void Start()
    {
        CreateTray();
        StartupService.SetEnabled(_state.Settings.StartWithWindows);
        ApplyEnabledWidgets();
        _state.SettingsChanged += OnSettingsChanged;
    }

    private void CreateTray()
    {
        var applicationIcon = Environment.ProcessPath is { } processPath
            ? Icon.ExtractAssociatedIcon(processPath)
            : null;
        var menu = new ContextMenuStrip();
        menu.Items.Add("设置", null, (_, _) => OpenSettings());
        menu.Items.Add("编辑布局", null, (_, _) => SetEditMode(!IsEditMode));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _tray = new NotifyIcon
        {
            Text = "桌面小组件",
            Icon = applicationIcon ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => OpenSettings();
    }

    public void OpenSettings(WidgetKind? focus = null)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_state, this, focus);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    public void SetEditMode(bool enabled)
    {
        IsEditMode = enabled;
        foreach (var window in _windows.Values) window.SetEditMode(enabled);
    }

    public void SetWidgetEnabled(WidgetKind kind, bool enabled)
    {
        _state.Settings.EnabledWidgets[kind] = enabled;
        _state.SaveSettings();
    }

    public void SetWidgetSize(WidgetKind kind, WidgetSize size)
    {
        var layout = _state.GetLayout(kind);
        layout.Size = size;
        if (_windows.TryGetValue(kind, out var window))
        {
            window.ApplySize(size);
            SnapAndSave(window);
            window.RebuildContent();
        }
        _state.SaveLayout();
    }

    private void OnSettingsChanged()
    {
        StartupService.SetEnabled(_state.Settings.StartWithWindows);
        ApplyEnabledWidgets();
        foreach (var window in _windows.Values) window.ApplyAppearance();
    }

    private void ApplyEnabledWidgets()
    {
        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            var enabled = _state.Settings.EnabledWidgets.GetValueOrDefault(kind, true);
            if (enabled && !_windows.ContainsKey(kind)) CreateWidget(kind);
            if (!enabled && _windows.Remove(kind, out var old)) old.Close();
        }
    }

    private void CreateWidget(WidgetKind kind)
    {
        var layout = _state.GetLayout(kind);
        var window = new DesktopWidgetWindow(kind, _state, this);
        _windows[kind] = window;
        window.Left = layout.Left;
        window.Top = layout.Top;
        window.ApplySize(layout.Size);
        window.Show();
        window.SetEditMode(IsEditMode);
        window.Dispatcher.BeginInvoke(() => SnapAndSave(window));
    }

    public void SnapAndSave(DesktopWidgetWindow moving)
    {
        var bounds = GetWorkArea(moving);
        var others = _windows.Values.Where(window => window != moving && window.IsVisible)
            .Select(window => new Rect(window.Left, window.Top, window.Width, window.Height)).ToArray();
        var result = WidgetPlacement.Calculate(bounds, new System.Windows.Size(moving.Width, moving.Height),
            new System.Windows.Point(moving.Left, moving.Top), others,
            fallback: new System.Windows.Point(moving.Left, moving.Top));
        moving.Left = result.Left;
        moving.Top = result.Top;
        var layout = _state.GetLayout(moving.Kind);
        layout.Left = moving.Left;
        layout.Top = moving.Top;
        layout.Size = moving.Size;
        layout.MonitorId = GetMonitorId(moving);
        _state.SaveLayout();
    }

    public void EnsureWidgetsStayOnDesktop()
    {
        if (App.IsPreviewMode) return;
        foreach (var window in _windows.Values.Where(window => window.IsVisible))
            NativeDesktop.EnsureInDesktopLayer(window);
    }

    internal WidgetPlacement.Result UpdateDragPosition(DesktopWidgetWindow moving, System.Windows.Point raw,
        double? heldX, double? heldY, System.Windows.Point fallback)
    {
        var bounds = GetWorkArea(moving);
        var others = _windows.Values.Where(window => window != moving && window.IsVisible)
            .Select(window => new Rect(window.Left, window.Top, window.Width, window.Height)).ToArray();
        var result = WidgetPlacement.Calculate(bounds, new System.Windows.Size(moving.Width, moving.Height), raw,
            others, heldX, heldY, fallback);
        moving.Left = result.Left;
        moving.Top = result.Top;
        return result;
    }

    private IEnumerable<PointF> CandidatePositions(double left, double top, Rect bounds, double width, double height)
    {
        yield return new PointF((float)left, (float)top);
        const int step = 8;
        for (var radius = step; radius <= 800; radius += step)
        {
            for (var x = -radius; x <= radius; x += step)
            {
                foreach (var y in new[] { -radius, radius })
                {
                    var px = Math.Clamp(left + x, bounds.Left, bounds.Right - width);
                    var py = Math.Clamp(top + y, bounds.Top, bounds.Bottom - height);
                    yield return new PointF((float)Snap(px), (float)Snap(py));
                }
            }
            for (var y = -radius + step; y < radius; y += step)
            {
                foreach (var x in new[] { -radius, radius })
                {
                    var px = Math.Clamp(left + x, bounds.Left, bounds.Right - width);
                    var py = Math.Clamp(top + y, bounds.Top, bounds.Bottom - height);
                    yield return new PointF((float)Snap(px), (float)Snap(py));
                }
            }
        }
    }

    private (double Left, double Top) AlignToWidgets(DesktopWidgetWindow moving, double left, double top)
    {
        var others = _windows.Values.Where(window => window != moving && window.IsVisible).ToArray();
        var directLeft = others.SelectMany(other => new[]
        {
            other.Left,
            other.Left + other.Width - moving.Width,
            other.Left + other.Width / 2 - moving.Width / 2
        }).ToArray();
        var directTop = others.SelectMany(other => new[]
        {
            other.Top,
            other.Top + other.Height - moving.Height,
            other.Top + other.Height / 2 - moving.Height / 2
        }).ToArray();
        var gapLeft = others.SelectMany(other => new[]
        {
            other.Left - moving.Width - Gap,
            other.Left + other.Width + Gap
        }).ToArray();
        var gapTop = others.SelectMany(other => new[]
        {
            other.Top - moving.Height - Gap,
            other.Top + other.Height + Gap
        }).ToArray();

        left = HasNearbyTarget(left, directLeft) ? SnapIfNear(left, directLeft) : SnapIfNear(left, gapLeft);
        top = HasNearbyTarget(top, directTop) ? SnapIfNear(top, directTop) : SnapIfNear(top, gapTop);

        return (left, top);
    }

    private static bool HasNearbyTarget(double value, IEnumerable<double> targets) =>
        targets.Any(target => Math.Abs(target - value) <= WidgetPlacement.SnapThreshold);

    private static double SnapIfNear(double value, IEnumerable<double> targets)
    {
        var nearest = targets.OrderBy(target => Math.Abs(target - value)).FirstOrDefault();
        return Math.Abs(nearest - value) <= WidgetPlacement.SnapThreshold ? nearest : value;
    }

    private bool IsFree(DesktopWidgetWindow moving, double left, double top)
    {
        var candidate = new Rect(left - Gap, top - Gap, moving.Width + Gap * 2, moving.Height + Gap * 2);
        return _windows.Values.Where(w => w != moving && w.IsVisible)
            .All(w => !candidate.IntersectsWith(new Rect(w.Left, w.Top, w.Width, w.Height)));
    }

    private static double Snap(double value) => Math.Round(value / 8d) * 8d;

    public static (double Width, double Height) SizeFor(WidgetSize size) => size switch
    {
        WidgetSize.Small => (220, 220),
        WidgetSize.Medium => (456, 220),
        _ => (456, 456)
    };

    public static (double Width, double Height) SizeFor(WidgetKind kind, WidgetSize size)
    {
        if (kind == WidgetKind.Weather)
        {
            return size switch
            {
                WidgetSize.Small => (160, 160),
                WidgetSize.Medium => (336, 160),
                _ => (400, 260)
            };
        }

        if (kind is WidgetKind.Clock or WidgetKind.Calendar)
        {
            var edge = size switch
            {
                WidgetSize.Small => 160d,
                WidgetSize.Medium => 280d,
                _ => 380d
            };
            return (edge, edge);
        }

        return SizeFor(size);
    }

    private static Rect GetWorkArea(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var screen = Screen.FromHandle(hwnd);
        var dpi = NativeDpi.GetDpi(hwnd) / 96d;
        var area = screen.WorkingArea;
        return new Rect(area.Left / dpi, area.Top / dpi, area.Width / dpi, area.Height / dpi);
    }

    private static string GetMonitorId(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return Screen.FromHandle(hwnd).DeviceName;
    }

    public void Dispose()
    {
        _state.SettingsChanged -= OnSettingsChanged;
        foreach (var window in _windows.Values.ToArray()) window.Close();
        _windows.Clear();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
    }
}

internal static class NativeDpi
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
    public static uint GetDpi(IntPtr hwnd) => hwnd == IntPtr.Zero ? 96u : GetDpiForWindow(hwnd);
}
