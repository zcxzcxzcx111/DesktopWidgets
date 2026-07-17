using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopWidgets;

internal static class NativeDesktop
{
    private const uint WmSpawnWorker = 0x052C;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtNone = 1;
    private const int DwmsbtTransientWindow = 3;

    public static void ConfigureWidgetWindow(System.Windows.Window window, AppSettings settings, bool attachToDesktop = true)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style | WsExToolWindow | WsExNoActivate));
        ApplyBlur(window, settings);
        if (attachToDesktop) PlaceInDesktopLayer(hwnd);
    }

    public static void ApplyBlur(Window window, AppSettings settings)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (Environment.OSVersion.Version.Build >= 22621)
        {
            var margins = new Margins(-1, -1, -1, -1);
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
            var backdrop = settings.BlurPercent > 0 ? DwmsbtTransientWindow : DwmsbtNone;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }

        ApplyRoundedRegion(window);
    }

    public static void EnsureInDesktopLayer(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero) PlaceInDesktopLayer(hwnd);
    }

    public static void ApplyRoundedRegion(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dpi = NativeDpi.GetDpi(hwnd) / 96d;
        var radius = window is DesktopWidgetWindow widget
            ? WidgetTheme.RadiusValue(widget.Size)
            : 20d;
        var width = Math.Max(1, (int)Math.Round(window.ActualWidth * dpi));
        var height = Math.Max(1, (int)Math.Round(window.ActualHeight * dpi));
        var diameter = Math.Max(2, (int)Math.Round(radius * 2 * dpi));
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
        if (region != IntPtr.Zero) SetWindowRgn(hwnd, region, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Margins
    {
        public readonly int Left;
        public readonly int Right;
        public readonly int Top;
        public readonly int Bottom;

        public Margins(int left, int right, int top, int bottom) =>
            (Left, Right, Top, Bottom) = (left, right, top, bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private static IntPtr FindDesktopHost()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
            SendMessageTimeout(progman, WmSpawnWorker, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        IntPtr desktopHost = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            var shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
                desktopHost = GetParent(shellView);
            return desktopHost == IntPtr.Zero;
        }, IntPtr.Zero);

        return desktopHost != IntPtr.Zero ? desktopHost : progman;
    }

    private static void PlaceInDesktopLayer(IntPtr hwnd)
    {
        // Reparenting a transparent WPF window to WorkerW can make it invisible.
        // Keeping it top-level but directly above the desktop host preserves the
        // glass surface while allowing every normal app window to cover it.
        var desktopHost = FindDesktopHost();
        var insertAfter = desktopHost != IntPtr.Zero ? desktopHost : HwndBottom;
        SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow);
    }

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndBottom = new(1);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? title);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
}
