using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DesktopWidgets;

public sealed class DesktopWidgetWindow : Window
{
    private readonly AppState _state;
    private readonly DesktopWidgetManager _manager;
    private readonly Border _card;
    private readonly Border _contentSurface;
    private readonly Border _glassDiffusion;
    private readonly Border _glassHighlight;
    private readonly Border _glassReflection;
    private readonly Border _editOutline;
    private bool _dragArmed;
    private bool _dragging;
    private Point _pointerStart;
    private Point _windowStart;
    private Point _lastLegalPosition;
    private double? _snapX;
    private double? _snapY;
    public WidgetKind Kind { get; }
    public WidgetSize Size => _state.GetLayout(Kind).Size;

    public DesktopWidgetWindow(WidgetKind kind, AppState state, DesktopWidgetManager manager)
    {
        Kind = kind;
        _state = state;
        _manager = manager;
        Title = Kind.ToString();
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        // DWM system backdrops require a non-layered HWND. Rounded transparency
        // is provided by the native window region instead.
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        ShowInTaskbar = App.IsPreviewMode;
        ShowActivated = false;
        Topmost = false;
        SnapsToDevicePixels = true;

        _contentSurface = new Border
        {
            // The clock draws its own proportional inner margin. Giving it the
            // full card canvas enlarges the face without changing the glass card.
            Padding = Kind == WidgetKind.Clock ? new Thickness(0) : new Thickness(16)
        };
        _glassDiffusion = new Border { IsHitTestVisible = false };
        _glassHighlight = new Border { IsHitTestVisible = false };
        _glassReflection = new Border { IsHitTestVisible = false };
        var layers = new Grid();
        layers.Children.Add(_glassDiffusion);
        layers.Children.Add(_glassHighlight);
        layers.Children.Add(_glassReflection);
        layers.Children.Add(_contentSurface);
        _card = new Border { Child = layers };
        _editOutline = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            CornerRadius = new CornerRadius(20),
            Child = _card
        };
        Content = _editOutline;
        ApplyAppearance();
        RebuildContent();

        SourceInitialized += (_, _) =>
        {
            if (!App.IsPreviewMode) NativeDesktop.ConfigureWidgetWindow(this, _state.Settings, true);
            else NativeDesktop.ApplyBlur(this, _state.Settings);
        };
        Loaded += (_, _) => _manager.EnsureWidgetsStayOnDesktop();
        Deactivated += (_, _) => _manager.EnsureWidgetsStayOnDesktop();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        LostMouseCapture += (_, _) => CancelDrag();
        MouseEnter += (_, _) => SetGlassInteraction(true);
        MouseLeave += (_, _) => SetGlassInteraction(false);
        ContextMenu = BuildContextMenu();
    }

    public void ApplySize(WidgetSize size)
    {
        var dimensions = DesktopWidgetManager.SizeFor(Kind, size);
        Width = dimensions.Width;
        Height = dimensions.Height;
        ApplyAppearance();
        Dispatcher.BeginInvoke(() => NativeDesktop.ApplyRoundedRegion(this));
    }

    public void ApplyAppearance()
    {
        _card.Background = WidgetTheme.CardBrush(_state.Settings, Kind);
        _card.BorderBrush = WidgetTheme.GlassEdgeBrush(_state.Settings, Kind);
        _card.BorderThickness = new Thickness(1);
        _card.CornerRadius = WidgetTheme.Radius(Size);
        _glassDiffusion.Background = WidgetTheme.FrostDiffusionBrush(_state.Settings, Kind);
        _glassDiffusion.CornerRadius = WidgetTheme.Radius(Size);
        _glassDiffusion.Effect = new BlurEffect
        {
            Radius = WidgetTheme.FrostDiffusionRadius(_state.Settings),
            RenderingBias = RenderingBias.Quality
        };
        _glassHighlight.Background = WidgetTheme.GlassHighlightBrush(_state.Settings, Kind);
        _glassHighlight.CornerRadius = WidgetTheme.Radius(Size);
        _glassReflection.Background = WidgetTheme.GlassReflectionBrush(_state.Settings, Kind);
        _glassReflection.CornerRadius = WidgetTheme.Radius(Size);
        _editOutline.CornerRadius = WidgetTheme.Radius(Size);
        Opacity = 1;
        SetGlassInteraction(IsMouseOver);
        NativeDesktop.ApplyBlur(this, _state.Settings);
        RebuildContent();
    }

    public void RebuildContent()
    {
        _contentSurface.Child = WidgetFactory.Create(Kind, Size, _state, _manager);
    }

    private void SetGlassInteraction(bool active)
    {
        _card.RenderTransform = new TranslateTransform(0, active ? -1 : 0);
        _card.Effect = new DropShadowEffect
        {
            Color = WidgetTheme.ShadowColor(_state.Settings),
            BlurRadius = active ? 26 : 21,
            ShadowDepth = active ? 8 : 6,
            Opacity = WidgetTheme.ShadowOpacity(_state.Settings, Kind, active),
            Direction = 270
        };
        _glassHighlight.Opacity = active ? 1 : 0.86;
        _glassReflection.Opacity = active ? 0.96 : 0.78;
        _glassDiffusion.Opacity = active ? 1 : 0.92;
    }

    public void SetEditMode(bool enabled)
    {
        _editOutline.BorderBrush = enabled ? WidgetTheme.AccentBrush(_state.Settings) : Brushes.Transparent;
        Cursor = enabled ? Cursors.SizeAll : Cursors.Arrow;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (!_manager.IsEditMode && !Keyboard.IsKeyDown(Key.LeftAlt) && !Keyboard.IsKeyDown(Key.RightAlt)) return;
        if (e.OriginalSource is Button or TextBox or CheckBox or DatePicker) return;
        _dragArmed = true;
        _dragging = false;
        _pointerStart = e.GetPosition(this);
        _windowStart = new Point(Left, Top);
        _lastLegalPosition = _windowStart;
        _snapX = _snapY = null;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed) return;
        var pointer = e.GetPosition(this);
        var delta = pointer - _pointerStart;
        if (!_dragging && delta.Length < 4) return;
        _dragging = true;
        Cursor = Cursors.SizeAll;
        var result = _manager.UpdateDragPosition(this,
            new Point(_windowStart.X + delta.X, _windowStart.Y + delta.Y), _snapX, _snapY, _lastLegalPosition);
        _snapX = result.SnapX;
        _snapY = result.SnapY;
        _lastLegalPosition = new Point(result.Left, result.Top);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragArmed) return;
        var dragged = _dragging;
        _dragArmed = false;
        _dragging = false;
        _snapX = _snapY = null;
        ReleaseMouseCapture();
        if (dragged) _manager.SnapAndSave(this);
        _manager.EnsureWidgetsStayOnDesktop();
    }

    private void CancelDrag()
    {
        if (!_dragArmed) return;
        if (_dragging) { Left = _lastLegalPosition.X; Top = _lastLegalPosition.Y; }
        _dragArmed = false;
        _dragging = false;
        _snapX = _snapY = null;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu { FontFamily = WidgetTheme.UiFont };
        foreach (var size in Enum.GetValues<WidgetSize>())
        {
            var item = new MenuItem { Header = size switch { WidgetSize.Small => "小", WidgetSize.Medium => "中", _ => "大" } };
            item.Click += (_, _) => _manager.SetWidgetSize(Kind, size);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var settings = new MenuItem { Header = "组件设置" };
        settings.Click += (_, _) => _manager.OpenSettings(Kind);
        menu.Items.Add(settings);
        var edit = new MenuItem { Header = "编辑布局" };
        edit.Click += (_, _) => _manager.SetEditMode(true);
        menu.Items.Add(edit);
        var hide = new MenuItem { Header = "隐藏组件" };
        hide.Click += (_, _) => _manager.SetWidgetEnabled(Kind, false);
        menu.Items.Add(hide);
        return menu;
    }
}
