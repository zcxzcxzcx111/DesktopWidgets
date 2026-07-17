using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DesktopWidgets;

/// <summary>Two-line calendar header and a month-aware grid that never reserves an unused week.</summary>
public sealed class GlassCalendarWidgetV2 : Grid
{
    private readonly WidgetSize _size;
    private readonly AppState _state;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public GlassCalendarWidgetV2(WidgetSize size, AppState state)
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

        var header = new Grid { Height = 34, Margin = new Thickness(0, 0, 0, 2) };
        var previous = Ui.IconButton("\u2039", _state.Settings, "Previous month");
        previous.Width = previous.Height = 18;
        previous.FontSize = 17;
        previous.HorizontalAlignment = HorizontalAlignment.Left;
        previous.VerticalAlignment = VerticalAlignment.Center;
        previous.Click += (_, _) => { _month = _month.AddMonths(-1); Build(); };
        header.Children.Add(previous);

        var headerText = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var solar = Ui.Text(SolarHeader(), _state.Settings, 12, FontWeights.SemiBold);
        solar.TextWrapping = TextWrapping.NoWrap;
        solar.HorizontalAlignment = HorizontalAlignment.Center;
        headerText.Children.Add(solar);
        var lunar = Ui.Text(LunarHeader(), _state.Settings, 10, FontWeights.SemiBold, WidgetTheme.SecondaryBrush(_state.Settings));
        lunar.TextWrapping = TextWrapping.NoWrap;
        lunar.HorizontalAlignment = HorizontalAlignment.Center;
        headerText.Children.Add(lunar);
        header.Children.Add(headerText);

        var next = Ui.IconButton("\u203a", _state.Settings, "Next month");
        next.Width = next.Height = 18;
        next.FontSize = 17;
        next.HorizontalAlignment = HorizontalAlignment.Right;
        next.VerticalAlignment = VerticalAlignment.Center;
        next.Click += (_, _) => { _month = _month.AddMonths(1); Build(); };
        header.Children.Add(next);
        Children.Add(header);

        var offset = ((int)_month.DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(_month.Year, _month.Month);
        var weekRows = (int)Math.Ceiling((offset + days) / 7d);
        var calendar = new UniformGrid { Columns = 7, Rows = weekRows + 1 };
        foreach (var day in new[] { "\u4e00", "\u4e8c", "\u4e09", "\u56db", "\u4e94", "\u516d", "\u65e5" })
        {
            var label = Ui.Text(day, _state.Settings, 10, FontWeights.SemiBold, WidgetTheme.SecondaryBrush(_state.Settings));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            calendar.Children.Add(label);
        }

        for (var index = 0; index < offset; index++) calendar.Children.Add(new Border());
        for (var day = 1; day <= days; day++)
        {
            var date = new DateTime(_month.Year, _month.Month, day);
            var isToday = date == DateTime.Today;
            var label = Ui.Text(day.ToString(), _state.Settings, _size == WidgetSize.Small ? 10 : 11,
                isToday ? FontWeights.Bold : FontWeights.SemiBold,
                isToday ? new SolidColorBrush(Color.FromRgb(61, 84, 106)) : WidgetTheme.PrimaryBrush(_state.Settings));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            calendar.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(11),
                Margin = new Thickness(1),
                Background = isToday ? WidgetTheme.AccentBrush(_state.Settings) : Brushes.Transparent,
                Child = label
            });
        }

        Grid.SetRow(calendar, 1);
        Children.Add(calendar);
    }

    private string SolarHeader()
    {
        if (_month.Year != DateTime.Today.Year || _month.Month != DateTime.Today.Month)
            return _month.ToString("yyyy\u5e74M\u6708", CultureInfo.GetCultureInfo("zh-CN"));
        var weekdays = new[] { "\u65e5", "\u4e00", "\u4e8c", "\u4e09", "\u56db", "\u4e94", "\u516d" };
        return $"{DateTime.Today:M\u6708d\u65e5} \u5468{weekdays[(int)DateTime.Today.DayOfWeek]}";
    }

    private string LunarHeader()
    {
        try
        {
            var date = _month.Year == DateTime.Today.Year && _month.Month == DateTime.Today.Month
                ? DateTime.Today : _month;
            var lunar = new ChineseLunisolarCalendar();
            var months = new[] { "\u6b63", "\u4e8c", "\u4e09", "\u56db", "\u4e94", "\u516d", "\u4e03", "\u516b", "\u4e5d", "\u5341", "\u51ac", "\u814a" };
            var days = new[] { "\u521d\u4e00", "\u521d\u4e8c", "\u521d\u4e09", "\u521d\u56db", "\u521d\u4e94", "\u521d\u516d", "\u521d\u4e03", "\u521d\u516b", "\u521d\u4e5d", "\u521d\u5341", "\u5341\u4e00", "\u5341\u4e8c", "\u5341\u4e09", "\u5341\u56db", "\u5341\u4e94", "\u5341\u516d", "\u5341\u4e03", "\u5341\u516b", "\u5341\u4e5d", "\u4e8c\u5341", "\u5eff\u4e00", "\u5eff\u4e8c", "\u5eff\u4e09", "\u5eff\u56db", "\u5eff\u4e94", "\u5eff\u516d", "\u5eff\u4e03", "\u5eff\u516b", "\u5eff\u4e5d", "\u4e09\u5341" };
            return $"\u519c\u5386{months[lunar.GetMonth(date) - 1]}\u6708{days[lunar.GetDayOfMonth(date) - 1]}";
        }
        catch { return "\u519c\u5386"; }
    }
}
