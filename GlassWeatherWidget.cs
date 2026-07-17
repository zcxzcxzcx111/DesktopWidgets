using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopWidgets;

/// <summary>Weather layout designed specifically for the compact horizontal medium card.</summary>
public sealed partial class GlassWeatherWidget : Grid
{
    private readonly WidgetSize _size;
    private readonly AppState _state;
    private readonly WeatherService _service = new();
    private readonly StackPanel _content = new();
    private readonly TextBlock _status;
    private readonly DispatcherTimer _timer;
    private bool _loading;

    public GlassWeatherWidget(WidgetSize size, AppState state)
    {
        _size = size;
        _state = state;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (_size != WidgetSize.Medium)
        {
        var header = new DockPanel();
        var city = Ui.Text(state.Settings.WeatherCity, state.Settings, 14, FontWeights.SemiBold);
        var refresh = Ui.IconButton("↻", state.Settings, "Refresh weather");
        refresh.Width = refresh.Height = 24;
        refresh.FontSize = 17;
        refresh.Click += async (_, _) => await RefreshAsync();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(city);
        Children.Add(header);
        }

        _content.VerticalAlignment = VerticalAlignment.Center;
        _content.Margin = new Thickness(0, 2, 0, 2);
        SetRow(_content, 1);
        Children.Add(_content);

        _status = Ui.Text("", state.Settings, 10, null, WidgetTheme.SecondaryBrush(state.Settings));
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
        _status.Text = "Updating";
        try { Render(await _service.FetchAsync(_state.Settings)); }
        catch (Exception ex)
        {
            Render(_service.LoadCache());
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
            _content.Children.Add(Ui.Text("Weather data unavailable", _state.Settings, 12));
            _status.Text = "";
            return;
        }

        _content.Children.Add(_size == WidgetSize.Medium ? BuildMediumLayoutV2(weather) : BuildSmallLayout(weather));
        _status.Text = $"Updated {weather.UpdatedAt:HH:mm}";
    }

    private FrameworkElement BuildMediumLayout(WeatherSnapshot weather)
    {
        var layout = new Grid { VerticalAlignment = VerticalAlignment.Center };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(122) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var current = new Grid { VerticalAlignment = VerticalAlignment.Center };
        current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.Text(WeatherIcon(weather.IconCode), _state.Settings, 28);
        icon.VerticalAlignment = VerticalAlignment.Center;
        current.Children.Add(icon);
        var temperature = Ui.Text($"{weather.Temperature}°", _state.Settings, 32, FontWeights.SemiBold);
        temperature.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(temperature, 1);
        current.Children.Add(temperature);
        var condition = Ui.Text(weather.Condition, _state.Settings, 12, FontWeights.SemiBold);
        condition.VerticalAlignment = VerticalAlignment.Center;
        condition.TextWrapping = TextWrapping.NoWrap;
        SetColumn(condition, 2);
        current.Children.Add(condition);
        layout.Children.Add(current);

        var hourly = new UniformGrid { Columns = Math.Min(4, Math.Max(1, weather.Hourly.Count)) };
        foreach (var hour in weather.Hourly.Take(4))
        {
            var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            cell.Children.Add(CenteredText(hour.Time.ToString("HH:mm"), 10, WidgetTheme.SecondaryBrush(_state.Settings)));
            cell.Children.Add(CenteredText(WeatherIcon(hour.IconCode), 15));
            cell.Children.Add(CenteredText($"{hour.Temperature}°", 11, null, FontWeights.SemiBold));
            hourly.Children.Add(cell);
        }
        Grid.SetColumn(hourly, 1);
        layout.Children.Add(hourly);
        return layout;
    }

    private FrameworkElement BuildSmallLayout(WeatherSnapshot weather)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Ui.Text(WeatherIcon(weather.IconCode), _state.Settings, 32));
        var temperature = Ui.Text($"{weather.Temperature}°", _state.Settings, 34, FontWeights.SemiBold);
        temperature.Margin = new Thickness(8, 0, 7, 0);
        panel.Children.Add(temperature);
        panel.Children.Add(Ui.Text(weather.Condition, _state.Settings, 13, FontWeights.SemiBold));
        return panel;
    }

    private TextBlock CenteredText(string value, double size, Brush? color = null, FontWeight? weight = null)
    {
        var text = Ui.Text(value, _state.Settings, size, weight, color);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        return text;
    }

    private static string WeatherIcon(string code) => code.Length > 0 ? code[0] switch
    {
        '1' => "☀",
        '2' => "⛅",
        '3' => "☁",
        '4' => "☁",
        '5' => "☔",
        _ => "☁"
    } : "☁";
}
