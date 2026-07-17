using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DesktopWidgets;

/// <summary>Compact, reference-image calendar with a current-month default and month navigation.</summary>
public sealed class GlassCalendarWidget : Grid
{
    private readonly WidgetSize _size;
    private readonly AppState _state;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public GlassCalendarWidget(WidgetSize size, AppState state)
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

        var header = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

        var previous = Ui.IconButton("‹", _state.Settings, "Previous month");
        previous.Width = previous.Height = 24;
        previous.FontSize = 20;
        previous.Click += (_, _) => { _month = _month.AddMonths(-1); Build(); };
        header.Children.Add(previous);

        var titlePanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var title = Ui.Text($"{_month.Month}\u6708  {LunarSummary(_month)}", _state.Settings, 14, FontWeights.SemiBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        titlePanel.Children.Add(title);
        if (!IsCurrentMonth)
        {
            var goToday = Ui.IconButton("\u4eca\u5929", _state.Settings, "Return to current month");
            goToday.Width = 38;
            goToday.Height = 16;
            goToday.FontSize = 10;
            goToday.HorizontalAlignment = HorizontalAlignment.Center;
            goToday.Click += (_, _) => { _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); Build(); };
            titlePanel.Children.Add(goToday);
        }
        Grid.SetColumn(titlePanel, 1);
        header.Children.Add(titlePanel);

        var next = Ui.IconButton("›", _state.Settings, "Next month");
        next.Width = next.Height = 24;
        next.FontSize = 20;
        next.Click += (_, _) => { _month = _month.AddMonths(1); Build(); };
        Grid.SetColumn(next, 2);
        header.Children.Add(next);
        Children.Add(header);

        var calendar = new UniformGrid { Columns = 7, Rows = 7 };
        foreach (var day in new[] { "\u4e00", "\u4e8c", "\u4e09", "\u56db", "\u4e94", "\u516d", "\u65e5" })
        {
            var label = Ui.Text(day, _state.Settings, 11, FontWeights.SemiBold, WidgetTheme.SecondaryBrush(_state.Settings));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            calendar.Children.Add(label);
        }

        var offset = ((int)_month.DayOfWeek + 6) % 7;
        for (var index = 0; index < offset; index++) calendar.Children.Add(new Border());
        for (var day = 1; day <= DateTime.DaysInMonth(_month.Year, _month.Month); day++)
        {
            var date = new DateTime(_month.Year, _month.Month, day);
            var isToday = date == DateTime.Today;
            var label = Ui.Text(day.ToString(), _state.Settings, _size == WidgetSize.Small ? 11 : 12,
                isToday ? FontWeights.Bold : FontWeights.SemiBold,
                isToday ? new SolidColorBrush(Color.FromRgb(61, 84, 106)) : WidgetTheme.PrimaryBrush(_state.Settings));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            calendar.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(2),
                Background = isToday ? WidgetTheme.AccentBrush(_state.Settings) : Brushes.Transparent,
                Child = label
            });
        }

        Grid.SetRow(calendar, 1);
        Children.Add(calendar);
    }

    private bool IsCurrentMonth => _month.Year == DateTime.Today.Year && _month.Month == DateTime.Today.Month;

    private static string LunarSummary(DateTime month)
    {
        if (month.Year != DateTime.Today.Year || month.Month != DateTime.Today.Month)
            return $"{month:yyyy}\u5e74";
        try
        {
            var lunar = new ChineseLunisolarCalendar();
            return $"\u519c\u5386{lunar.GetMonth(DateTime.Today)}\u6708{lunar.GetDayOfMonth(DateTime.Today)}";
        }
        catch { return $"{month:yyyy}\u5e74"; }
    }
}
