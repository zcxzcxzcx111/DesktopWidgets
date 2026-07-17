using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DesktopWidgets;

public sealed class SettingsWindow : Window
{
    private readonly AppState _state;
    private readonly DesktopWidgetManager _manager;
    private readonly Dictionary<WidgetKind, CheckBox> _moduleChecks = [];
    private readonly ComboBox _theme;
    private readonly Slider _opacity;
    private readonly Slider _blur;
    private readonly CheckBox _clock24;
    private readonly CheckBox _seconds;
    private readonly TextBox _city;
    private readonly TextBox _apiHost;
    private readonly TextBox _geoHost;
    private readonly PasswordBox _apiKey;
    private readonly CheckBox _startup;

    public SettingsWindow(AppState state, DesktopWidgetManager manager, WidgetKind? focus)
    {
        _state = state;
        _manager = manager;
        Title = focus == null ? "桌面小组件设置" : $"{DisplayName(focus.Value)}设置";
        Width = 640;
        Height = 720;
        MinWidth = 560;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 246));
        FontFamily = WidgetTheme.UiFont;

        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock
        {
            Text = "桌面小组件",
            FontFamily = WidgetTheme.UiFont,
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(27, 46, 48)),
            Margin = new Thickness(0, 0, 0, 18)
        });

        var form = new StackPanel();
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        AddHeading(form, "模块");
        var modules = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 18) };
        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            var check = new CheckBox
            {
                Content = DisplayName(kind),
                IsChecked = state.Settings.EnabledWidgets.GetValueOrDefault(kind, true),
                Margin = new Thickness(0, 5, 12, 5),
                FontSize = 14
            };
            _moduleChecks[kind] = check;
            modules.Children.Add(check);
        }
        form.Children.Add(modules);

        AddHeading(form, "外观");
        _theme = AddCombo(form, "主题", new[] { "跟随系统", "浅色", "深色" }, state.Settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 });
        _opacity = AddSlider(form, "背景透明度", state.Settings.OpacityPercent);
        _blur = AddSlider(form, "毛玻璃模糊强度", state.Settings.BlurPercent);

        AddHeading(form, "时钟");
        _clock24 = AddCheck(form, "使用 24 小时制", state.Settings.Use24HourClock);
        _seconds = AddCheck(form, "显示秒数", state.Settings.ShowSeconds);

        AddHeading(form, "天气");
        _city = AddText(form, "城市", state.Settings.WeatherCity);
        _apiKey = AddPassword(form, "和风天气 API Key", SecretStore.HasWeatherKey ? "已加密保存；留空则保持不变" : "输入 API Key");
        _apiHost = AddText(form, "天气 API Host", state.Settings.WeatherApiHost);
        _geoHost = AddText(form, "城市查询 Host", state.Settings.WeatherGeoHost);
        form.Children.Add(new TextBlock
        {
            Text = "API Key 使用 Windows DPAPI 加密，仅当前 Windows 用户可以解密。",
            FontSize = 12,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 18)
        });

        AddHeading(form, "系统");
        _startup = AddCheck(form, "开机自动启动", state.Settings.StartWithWindows);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = MakeButton("取消", false);
        cancel.Click += (_, _) => Close();
        var edit = MakeButton(manager.IsEditMode ? "退出布局编辑" : "编辑布局", false);
        edit.Click += (_, _) => { manager.SetEditMode(!manager.IsEditMode); Close(); };
        var save = MakeButton("保存", true);
        save.Click += (_, _) => Save();
        buttons.Children.Add(cancel);
        buttons.Children.Add(edit);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        Content = root;
    }

    private void Save()
    {
        foreach (var pair in _moduleChecks) _state.Settings.EnabledWidgets[pair.Key] = pair.Value.IsChecked == true;
        _state.Settings.Theme = _theme.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" };
        _state.Settings.OpacityPercent = _opacity.Value;
        _state.Settings.BlurPercent = _blur.Value;
        _state.Settings.Use24HourClock = _clock24.IsChecked == true;
        _state.Settings.ShowSeconds = _seconds.IsChecked == true;
        _state.Settings.WeatherCity = string.IsNullOrWhiteSpace(_city.Text) ? "上海" : _city.Text.Trim();
        _state.Settings.WeatherApiHost = NormalizeHost(_apiHost.Text, "devapi.qweather.com");
        _state.Settings.WeatherGeoHost = NormalizeHost(_geoHost.Text, "geoapi.qweather.com");
        _state.Settings.StartWithWindows = _startup.IsChecked == true;
        if (!string.IsNullOrWhiteSpace(_apiKey.Password)) SecretStore.SaveWeatherKey(_apiKey.Password);
        _state.SaveSettings();
        Close();
    }

    private static string NormalizeHost(string value, string fallback)
    {
        var host = value.Trim().Replace("https://", "", StringComparison.OrdinalIgnoreCase).TrimEnd('/');
        return string.IsNullOrWhiteSpace(host) ? fallback : host;
    }

    private static string DisplayName(WidgetKind kind) => kind switch
    {
        WidgetKind.Clock => "时钟",
        WidgetKind.Weather => "天气",
        WidgetKind.Calendar => "日历",
        _ => "待办"
    };

    private static void AddHeading(Panel panel, string text) => panel.Children.Add(new TextBlock
    {
        Text = text,
        FontFamily = WidgetTheme.UiFont,
        FontSize = 16,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromRgb(27, 46, 48)),
        Margin = new Thickness(0, 12, 0, 10)
    });

    private static ComboBox AddCombo(Panel panel, string label, IEnumerable<string> items, int selected)
    {
        var row = LabeledRow(label);
        var combo = new ComboBox { MinWidth = 220, Padding = new Thickness(8, 5, 8, 5), ItemsSource = items, SelectedIndex = selected };
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        panel.Children.Add(row);
        return combo;
    }

    private static Slider AddSlider(Panel panel, string label, double value)
    {
        var row = LabeledRow(label);
        var inner = new Grid();
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true };
        var number = new TextBlock { Text = $"{value:0}%", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        slider.ValueChanged += (_, _) => number.Text = $"{slider.Value:0}%";
        inner.Children.Add(slider);
        Grid.SetColumn(number, 1);
        inner.Children.Add(number);
        Grid.SetColumn(inner, 1);
        row.Children.Add(inner);
        panel.Children.Add(row);
        return slider;
    }

    private static CheckBox AddCheck(Panel panel, string label, bool value)
    {
        var check = new CheckBox { Content = label, IsChecked = value, FontSize = 14, Margin = new Thickness(0, 5, 0, 10) };
        panel.Children.Add(check);
        return check;
    }

    private static TextBox AddText(Panel panel, string label, string value)
    {
        var row = LabeledRow(label);
        var input = new TextBox { Text = value, Padding = new Thickness(8, 6, 8, 6), MinWidth = 280 };
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        panel.Children.Add(row);
        return input;
    }

    private static PasswordBox AddPassword(Panel panel, string label, string hint)
    {
        var row = LabeledRow(label);
        var input = new PasswordBox { Padding = new Thickness(8, 6, 8, 6), MinWidth = 280, ToolTip = hint };
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        panel.Children.Add(row);
        return input;
    }

    private static Grid LabeledRow(string label)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
        return row;
    }

    private static Button MakeButton(string text, bool primary) => new()
    {
        Content = text,
        MinWidth = 92,
        Padding = new Thickness(16, 8, 16, 8),
        Margin = new Thickness(8, 0, 0, 0),
        FontFamily = WidgetTheme.UiFont,
        Background = primary ? new SolidColorBrush(Color.FromRgb(24, 116, 108)) : Brushes.White,
        Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(27, 46, 48)),
        BorderThickness = new Thickness(0)
    };
}
