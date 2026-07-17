using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DesktopWidgets;

public sealed partial class GlassWeatherWidget
{
    private FrameworkElement BuildMediumLayoutV2(WeatherSnapshot weather)
    {
        var layout = new Grid { VerticalAlignment = VerticalAlignment.Center };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Center the city label over the first hourly forecast column so the
        // two rows share a clear vertical rhythm.
        var today = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0, 0, 0)
        };
        today.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        today.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        today.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        today.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        today.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        today.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

        var city = Ui.Text(_state.Settings.WeatherCity, _state.Settings, 14, FontWeights.SemiBold);
        city.Margin = new Thickness(0, 0, 10, 0);
        city.VerticalAlignment = VerticalAlignment.Center;
        today.Children.Add(city);

        var icon = Ui.Text(WeatherIcon(weather.IconCode), _state.Settings, 25);
        icon.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(icon, 1);
        today.Children.Add(icon);

        var temperature = Ui.Text($"{weather.Temperature}°", _state.Settings, 28, FontWeights.SemiBold);
        temperature.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(temperature, 2);
        today.Children.Add(temperature);

        var condition = Ui.Text(weather.Condition, _state.Settings, 12, FontWeights.SemiBold);
        condition.Margin = new Thickness(4, 0, 0, 0);
        condition.VerticalAlignment = VerticalAlignment.Center;
        condition.TextWrapping = TextWrapping.NoWrap;
        SetColumn(condition, 3);
        today.Children.Add(condition);

        var refresh = Ui.IconButton("\u21bb", _state.Settings, "Refresh weather");
        refresh.Width = refresh.Height = 24;
        refresh.FontSize = 16;
        refresh.Click += async (_, _) => await RefreshAsync();
        SetColumn(refresh, 5);
        today.Children.Add(refresh);
        layout.Children.Add(today);

        var hourly = new UniformGrid
        {
            Columns = Math.Min(4, Math.Max(1, weather.Hourly.Count)),
            Margin = new Thickness(0, 5, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var hour in weather.Hourly.Take(4))
        {
            var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            cell.Children.Add(CenteredText(hour.Time.ToString("HH:mm"), 10, WidgetTheme.SecondaryBrush(_state.Settings)));
            cell.Children.Add(CenteredText(WeatherIcon(hour.IconCode), 15));
            cell.Children.Add(CenteredText($"{hour.Temperature}°", 11, null, FontWeights.SemiBold));
            hourly.Children.Add(cell);
        }
        Grid.SetRow(hourly, 1);
        layout.Children.Add(hourly);
        return layout;
    }
}
