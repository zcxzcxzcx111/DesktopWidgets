using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopWidgets;

public static class WidgetFactory
{
    public static FrameworkElement Create(WidgetKind kind, WidgetSize size, AppState state, DesktopWidgetManager manager) => kind switch
    {
        WidgetKind.Clock => new ClockFaceWidget(state),
        WidgetKind.Weather => new GlassWeatherWidget(size, state),
        WidgetKind.Calendar => new GlassCalendarWidgetV2(size, state),
        _ => new TodoWidget(size, state)
    };
}

internal static class Ui
{
    public static TextBlock Text(string text, AppSettings settings, double size = 13, FontWeight? weight = null,
        Brush? color = null) => new()
    {
        Text = text,
        FontFamily = WidgetTheme.UiFont,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = color ?? WidgetTheme.PrimaryBrush(settings),
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.Wrap
    };

    public static Button IconButton(string text, AppSettings settings, string tooltip)
    {
        var button = new Button
        {
            Content = text,
            Width = 30,
            Height = 30,
            FontFamily = WidgetTheme.UiFont,
            FontSize = 16,
            Foreground = WidgetTheme.PrimaryBrush(settings),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Padding = new Thickness(0)
        };
        button.Template = RoundedIconButtonTemplate(settings);
        return button;
    }

    private static ControlTemplate RoundedIconButtonTemplate(AppSettings settings)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(99));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(52, 255, 255, 255))));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Control.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(78, 255, 255, 255))));
        template.Triggers.Add(pressed);
        return template;
    }
}

public sealed class ClockWidget : Grid
{
    private readonly WidgetSize _size;
    private readonly AppState _state;
    private readonly TextBlock _time;
    private readonly TextBlock _date;
    private readonly DispatcherTimer _timer;

    public ClockWidget(WidgetSize size, AppState state)
    {
        _size = size;
        _state = state;
        VerticalAlignment = VerticalAlignment.Stretch;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _time = Ui.Text("", state.Settings, size == WidgetSize.Small ? 48 : 60, FontWeights.SemiBold);
        _time.HorizontalAlignment = HorizontalAlignment.Center;
        _time.VerticalAlignment = VerticalAlignment.Center;
        _time.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI Variable");
        _date = Ui.Text("", state.Settings, 13, FontWeights.SemiBold, WidgetTheme.SecondaryBrush(state.Settings));
        _date.HorizontalAlignment = HorizontalAlignment.Center;
        _date.Margin = new Thickness(0, 8, 0, 4);
        Children.Add(_time);
        SetRow(_date, 1);
        Children.Add(_date);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateTime();
        Loaded += (_, _) => { UpdateTime(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        var format = _state.Settings.Use24HourClock
            ? (_state.Settings.ShowSeconds ? "HH:mm:ss" : "HH:mm")
            : (_state.Settings.ShowSeconds ? "hh:mm:ss tt" : "hh:mm tt");
        _time.Text = now.ToString(format, CultureInfo.CurrentCulture);
        _date.Text = now.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN"));
    }
}

public sealed class WeatherWidget : Grid
{
    private readonly WidgetSize _size;
    private readonly AppState _state;
    private readonly WeatherService _service = new();
    private readonly DispatcherTimer _timer;
    private readonly StackPanel _content = new();
    private readonly TextBlock _status;
    private bool _loading;

    public WeatherWidget(WidgetSize size, AppState state)
    {
        _size = size;
        _state = state;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new DockPanel();
        var city = Ui.Text(state.Settings.WeatherCity, state.Settings, 14, FontWeights.SemiBold);
        city.SetValue(DockPanel.DockProperty, Dock.Left);
        var refresh = Ui.IconButton("↻", state.Settings, "刷新天气");
        refresh.SetValue(DockPanel.DockProperty, Dock.Right);
        refresh.Click += async (_, _) => await RefreshAsync();
        header.Children.Add(refresh);
        header.Children.Add(city);
        Children.Add(header);

        _content.VerticalAlignment = VerticalAlignment.Center;
        _content.Margin = new Thickness(0, 8, 0, 8);
        SetRow(_content, 1);
        Children.Add(_content);

        _status = Ui.Text("", state.Settings, 11, null, WidgetTheme.SecondaryBrush(state.Settings));
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        SetRow(_status, 2);
        Children.Add(_status);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            Render(_service.LoadCache());
            _timer.Start();
            if (SecretStore.HasWeatherKey) await RefreshAsync();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        _status.Text = "更新中…";
        try { Render(await _service.FetchAsync(_state.Settings)); }
        catch (Exception ex)
        {
            var cache = _service.LoadCache();
            if (cache != null) Render(cache);
            _status.Text = ex.Message;
            _status.ToolTip = ex.Message;
            AppLog.Write("Weather refresh failed", ex);
        }
        finally { _loading = false; }
    }

    private void Render(WeatherSnapshot? weather)
    {
        _content.Children.Clear();
        if (weather == null)
        {
            _content.Children.Add(Ui.Text(SecretStore.HasWeatherKey ? "暂无天气数据" : "请在设置中配置天气 API", _state.Settings, 13));
            _status.Text = "";
            return;
        }

        var top = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        top.Children.Add(Ui.Text(WeatherIcon(weather.IconCode), _state.Settings, 38));
        var temp = Ui.Text($"{weather.Temperature}°", _state.Settings, 42, FontWeights.SemiBold);
        temp.Margin = new Thickness(12, 0, 12, 0);
        top.Children.Add(temp);
        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(Ui.Text(weather.Condition, _state.Settings, 14, FontWeights.SemiBold));
        if (_size != WidgetSize.Small)
            details.Children.Add(Ui.Text($"最高 {weather.TempMax}°  最低 {weather.TempMin}°", _state.Settings, 12, null, WidgetTheme.SecondaryBrush(_state.Settings)));
        top.Children.Add(details);
        _content.Children.Add(top);

        if (_size != WidgetSize.Small && weather.Hourly.Count > 0)
        {
            var hours = new UniformGrid { Columns = Math.Min(5, weather.Hourly.Count), Margin = new Thickness(0, 14, 0, 0) };
            foreach (var hour in weather.Hourly.Take(5))
            {
                var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                cell.Children.Add(Ui.Text(hour.Time.ToString("HH时"), _state.Settings, 11, null, WidgetTheme.SecondaryBrush(_state.Settings)));
                cell.Children.Add(Ui.Text(WeatherIcon(hour.IconCode), _state.Settings, 18));
                cell.Children.Add(Ui.Text($"{hour.Temperature}°", _state.Settings, 12, FontWeights.SemiBold));
                hours.Children.Add(cell);
            }
            _content.Children.Add(hours);
        }
        _status.Text = $"更新于 {weather.UpdatedAt:HH:mm}";
    }

    private static string WeatherIcon(string code) => code.Length > 0 ? code[0] switch
    {
        '1' => "☀",
        '2' => "☁",
        '3' => "☂",
        '4' => "❄",
        '5' => "≋",
        _ => "◌"
    } : "◌";
}

public sealed class CalendarWidget : Grid
{
    private readonly WidgetSize _size;
    private readonly AppState _state;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public CalendarWidget(WidgetSize size, AppState state)
    {
        _size = size;
        _state = state;
        Build();
    }

    private void Build()
    {
        Children.Clear();
        RowDefinitions.Clear();
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var previous = Ui.IconButton("‹", _state.Settings, "上个月");
        previous.Click += (_, _) => { _month = _month.AddMonths(-1); Build(); };
        DockPanel.SetDock(previous, Dock.Left);
        var next = Ui.IconButton("›", _state.Settings, "下个月");
        next.Click += (_, _) => { _month = _month.AddMonths(1); Build(); };
        DockPanel.SetDock(next, Dock.Right);
        var title = Ui.Text(_month.ToString("yyyy年 M月"), _state.Settings, 15, FontWeights.SemiBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        header.Children.Add(previous);
        header.Children.Add(next);
        header.Children.Add(title);
        Children.Add(header);

        var calendar = new UniformGrid { Columns = 7, Rows = 7 };
        foreach (var day in new[] { "一", "二", "三", "四", "五", "六", "日" })
        {
            var label = Ui.Text(day, _state.Settings, 11, FontWeights.SemiBold, WidgetTheme.SecondaryBrush(_state.Settings));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            calendar.Children.Add(label);
        }

        var offset = ((int)_month.DayOfWeek + 6) % 7;
        for (var i = 0; i < offset; i++) calendar.Children.Add(new Border());
        for (var day = 1; day <= DateTime.DaysInMonth(_month.Year, _month.Month); day++)
        {
            var date = new DateTime(_month.Year, _month.Month, day);
            var today = date == DateTime.Today;
            var text = Ui.Text(day.ToString(), _state.Settings, _size == WidgetSize.Small ? 11 : 12,
                today ? FontWeights.Bold : FontWeights.Normal,
                today ? Brushes.White : WidgetTheme.PrimaryBrush(_state.Settings));
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            var cell = new Border
            {
                CornerRadius = new CornerRadius(13),
                Margin = new Thickness(2),
                Background = today ? WidgetTheme.AccentBrush(_state.Settings) : Brushes.Transparent,
                Child = text
            };
            calendar.Children.Add(cell);
        }
        SetRow(calendar, 1);
        Children.Add(calendar);
    }
}

public sealed class TodoWidget : Grid
{
    private readonly WidgetSize _size;
    private readonly AppState _state;
    private readonly StackPanel _list = new();
    private readonly TextBox _newTitle;
    private readonly DatePicker _dueDate;
    private readonly Border _undoBar;
    private TodoItem? _lastDeleted;
    private DispatcherTimer? _undoTimer;

    public TodoWidget(WidgetSize size, AppState state)
    {
        _size = size;
        _state = state;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = Ui.Text("待办", state.Settings, 18, FontWeights.SemiBold);
        heading.Margin = new Thickness(0, 0, 0, 10);
        Children.Add(heading);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list };
        SetRow(scroll, 1);
        Children.Add(scroll);

        var input = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        input.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (_size != WidgetSize.Small) input.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        input.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        _newTitle = new TextBox
        {
            FontFamily = WidgetTheme.UiFont,
            FontSize = 13,
            Padding = new Thickness(9, 6, 9, 6),
            Background = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            Foreground = WidgetTheme.PrimaryBrush(state.Settings),
            BorderThickness = new Thickness(0),
            ToolTip = "待办内容"
        };
        _newTitle.KeyDown += (_, e) => { if (e.Key == Key.Enter) AddTodo(); };
        input.Children.Add(_newTitle);
        _dueDate = new DatePicker
        {
            FontFamily = WidgetTheme.UiFont,
            Margin = new Thickness(8, 0, 0, 0),
            SelectedDateFormat = DatePickerFormat.Short,
            ToolTip = "截止日期"
        };
        if (_size != WidgetSize.Small) { SetColumn(_dueDate, 1); input.Children.Add(_dueDate); }
        var add = Ui.IconButton("＋", state.Settings, "新增待办");
        add.Click += (_, _) => AddTodo();
        SetColumn(add, _size == WidgetSize.Small ? 1 : 2);
        input.Children.Add(add);
        SetRow(input, 2);
        Children.Add(input);

        _undoBar = new Border { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
        SetRow(_undoBar, 3);
        Children.Add(_undoBar);

        _state.TodosChanged += Render;
        Unloaded += (_, _) => _state.TodosChanged -= Render;
        Render();
    }

    private void AddTodo()
    {
        var title = _newTitle.Text.Trim();
        if (title.Length == 0) return;
        _state.Todos.Add(new TodoItem { Title = title, DueDate = _dueDate.SelectedDate });
        _newTitle.Clear();
        _dueDate.SelectedDate = null;
        _state.SaveTodos();
    }

    private void Render()
    {
        _list.Children.Clear();
        var ordered = _state.Todos
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.CreatedAt)
            .ToList();

        if (ordered.Count == 0)
        {
            var empty = Ui.Text("暂无待办", _state.Settings, 13, null, WidgetTheme.SecondaryBrush(_state.Settings));
            empty.Margin = new Thickness(0, 16, 0, 0);
            _list.Children.Add(empty);
            return;
        }

        foreach (var item in ordered)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var check = new CheckBox { IsChecked = item.IsCompleted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) };
            check.Checked += (_, _) => Complete(item, true);
            check.Unchecked += (_, _) => Complete(item, false);
            row.Children.Add(check);
            var textStack = new StackPanel();
            var title = Ui.Text(item.Title, _state.Settings, 13, item.IsCompleted ? FontWeights.Normal : FontWeights.SemiBold);
            if (item.IsCompleted) title.TextDecorations = TextDecorations.Strikethrough;
            textStack.Children.Add(title);
            if (item.DueDate != null && _size != WidgetSize.Small)
            {
                var overdue = !item.IsCompleted && item.DueDate.Value.Date < DateTime.Today;
                textStack.Children.Add(Ui.Text($"截止 {item.DueDate:MM月dd日}", _state.Settings, 11, null,
                    overdue ? Brushes.IndianRed : WidgetTheme.SecondaryBrush(_state.Settings)));
            }
            SetColumn(textStack, 1);
            row.Children.Add(textStack);
            var delete = Ui.IconButton("×", _state.Settings, "删除待办");
            delete.Click += (_, _) => Delete(item);
            SetColumn(delete, 2);
            row.Children.Add(delete);
            _list.Children.Add(row);
        }
    }

    private void Complete(TodoItem item, bool completed)
    {
        item.IsCompleted = completed;
        item.CompletedAt = completed ? DateTime.Now : null;
        _state.SaveTodos();
    }

    private void Delete(TodoItem item)
    {
        _state.Todos.Remove(item);
        _lastDeleted = item;
        _state.SaveTodos();
        ShowUndo();
    }

    private void ShowUndo()
    {
        var panel = new DockPanel();
        panel.Children.Add(Ui.Text("已删除", _state.Settings, 12, null, WidgetTheme.SecondaryBrush(_state.Settings)));
        var undo = new Button
        {
            Content = "撤销",
            FontFamily = WidgetTheme.UiFont,
            Foreground = WidgetTheme.AccentBrush(_state.Settings),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        DockPanel.SetDock(undo, Dock.Right);
        undo.Click += (_, _) =>
        {
            if (_lastDeleted != null) { _state.Todos.Add(_lastDeleted); _lastDeleted = null; _state.SaveTodos(); }
            _undoBar.Visibility = Visibility.Collapsed;
        };
        panel.Children.Insert(0, undo);
        _undoBar.Child = panel;
        _undoBar.Visibility = Visibility.Visible;
        _undoTimer?.Stop();
        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _undoTimer.Tick += (_, _) => { _undoTimer.Stop(); _lastDeleted = null; _undoBar.Visibility = Visibility.Collapsed; };
        _undoTimer.Start();
    }
}
