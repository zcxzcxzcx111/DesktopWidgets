using System.Text.Json.Serialization;

namespace DesktopWidgets;

public enum WidgetKind { Clock, Weather, Calendar, Todo }
public enum WidgetSize { Small, Medium, Large }

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public double OpacityPercent { get; set; } = 82;
    public double BlurPercent { get; set; } = 80;
    public bool Use24HourClock { get; set; } = true;
    public bool ShowSeconds { get; set; }
    public string WeatherCity { get; set; } = "上海";
    public string WeatherApiHost { get; set; } = "devapi.qweather.com";
    public string WeatherGeoHost { get; set; } = "geoapi.qweather.com";
    public bool StartWithWindows { get; set; } = true;
    public Dictionary<WidgetKind, bool> EnabledWidgets { get; set; } = new()
    {
        [WidgetKind.Clock] = true,
        [WidgetKind.Weather] = true,
        [WidgetKind.Calendar] = true,
        [WidgetKind.Todo] = true
    };
}

public sealed class WidgetLayout
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WidgetKind Kind { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WidgetSize Size { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public string MonitorId { get; set; } = "Primary";
}

public sealed class LayoutState
{
    public List<WidgetLayout> Widgets { get; set; } = [];
}

public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
}

public sealed class WeatherSnapshot
{
    public string City { get; set; } = "";
    public string Condition { get; set; } = "--";
    public string IconCode { get; set; } = "999";
    public string Temperature { get; set; } = "--";
    public string TempMax { get; set; } = "--";
    public string TempMin { get; set; } = "--";
    public DateTime UpdatedAt { get; set; }
    public List<HourlyWeather> Hourly { get; set; } = [];
}

public sealed class HourlyWeather
{
    public DateTime Time { get; set; }
    public string Temperature { get; set; } = "--";
    public string IconCode { get; set; } = "999";
}
