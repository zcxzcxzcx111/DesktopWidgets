using Microsoft.Win32;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopWidgets;

public static class AppPaths
{
    public static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "所有文件", "AI项目", "桌面小组件");
    public static readonly string Config = Path.Combine(Root, "config");
    public static readonly string Data = Path.Combine(Root, "data");
    public static readonly string Cache = Path.Combine(Root, "cache");
    public static readonly string Secrets = Path.Combine(Root, "secrets");
    public static readonly string Logs = Path.Combine(Root, "logs");

    public static void Ensure()
    {
        foreach (var path in new[] { Config, Data, Cache, Secrets, Logs })
            Directory.CreateDirectory(path);
    }
}

public static class AppLog
{
    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            AppPaths.Ensure();
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (ex != null) line += $" | {ex.GetType().Name}: {ex.Message}";
            File.AppendAllText(Path.Combine(AppPaths.Logs, $"app-{DateTime.Now:yyyy-MM-dd}.log"), line + Environment.NewLine);
        }
        catch { }
    }
}

public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Load<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? fallback;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Could not load {Path.GetFileName(path)}", ex);
            try { File.Copy(path, path + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}", true); } catch { }
            return fallback;
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, true);
    }
}

public sealed class AppState
{
    public AppSettings Settings { get; private set; }
    public LayoutState Layout { get; private set; }
    public List<TodoItem> Todos { get; private set; }
    public event Action? SettingsChanged;
    public event Action? TodosChanged;

    public AppState()
    {
        AppPaths.Ensure();
        Settings = JsonStore.Load(Path.Combine(AppPaths.Config, "settings.json"), new AppSettings());
        Layout = JsonStore.Load(Path.Combine(AppPaths.Config, "layout.json"), DefaultLayout());
        Todos = JsonStore.Load(Path.Combine(AppPaths.Data, "todos.json"), new List<TodoItem>());
    }

    private static LayoutState DefaultLayout() => new()
    {
        Widgets =
        [
            new() { Kind = WidgetKind.Clock, Size = WidgetSize.Small, Left = 32, Top = 32 },
            new() { Kind = WidgetKind.Weather, Size = WidgetSize.Medium, Left = 268, Top = 32 },
            new() { Kind = WidgetKind.Calendar, Size = WidgetSize.Medium, Left = 32, Top = 268 },
            new() { Kind = WidgetKind.Todo, Size = WidgetSize.Large, Left = 504, Top = 32 }
        ]
    };

    public WidgetLayout GetLayout(WidgetKind kind)
    {
        var item = Layout.Widgets.FirstOrDefault(x => x.Kind == kind);
        if (item != null) return item;
        item = new WidgetLayout { Kind = kind, Size = WidgetSize.Small, Left = 32, Top = 32 };
        Layout.Widgets.Add(item);
        return item;
    }

    public void SaveSettings()
    {
        JsonStore.Save(Path.Combine(AppPaths.Config, "settings.json"), Settings);
        SettingsChanged?.Invoke();
    }

    public void SaveLayout() => JsonStore.Save(Path.Combine(AppPaths.Config, "layout.json"), Layout);

    public void SaveTodos()
    {
        JsonStore.Save(Path.Combine(AppPaths.Data, "todos.json"), Todos);
        TodosChanged?.Invoke();
    }
}

public static class SecretStore
{
    private static readonly string SecretPath = Path.Combine(AppPaths.Secrets, "weather-credentials.dat");

    public static void SaveWeatherKey(string apiKey)
    {
        AppPaths.Ensure();
        var plain = Encoding.UTF8.GetBytes(apiKey.Trim());
        var encrypted = Dpapi.Protect(plain);
        File.WriteAllBytes(SecretPath, encrypted);
        CryptographicOperations.ZeroMemory(plain);
    }

    public static string? GetWeatherKey()
    {
        try
        {
            if (!File.Exists(SecretPath)) return null;
            var plain = Dpapi.Unprotect(File.ReadAllBytes(SecretPath));
            try { return Encoding.UTF8.GetString(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not decrypt weather credential", ex);
            return null;
        }
    }

    public static bool HasWeatherKey => File.Exists(SecretPath);
}

internal static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int Size; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    public static byte[] Protect(byte[] input) => Transform(input, true);
    public static byte[] Unprotect(byte[] input) => Transform(input, false);

    private static byte[] Transform(byte[] input, bool protect)
    {
        var inputPtr = Marshal.AllocHGlobal(input.Length);
        Marshal.Copy(input, 0, inputPtr, input.Length);
        var inputBlob = new DataBlob { Size = input.Length, Data = inputPtr };
        try
        {
            DataBlob output;
            var ok = protect
                ? CryptProtectData(ref inputBlob, "DesktopWidgets Weather API", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
            if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(inputPtr); }
    }
}

public sealed class WeatherService
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    })
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    public WeatherSnapshot? LoadCache() => JsonStore.Load<WeatherSnapshot?>(
        Path.Combine(AppPaths.Cache, "weather.json"), null);

    public async Task<WeatherSnapshot> FetchAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var key = SecretStore.GetWeatherKey() ?? throw new InvalidOperationException("请先在设置中配置和风天气 API Key。");
        var city = Uri.EscapeDataString(settings.WeatherCity.Trim());
        var geoUrl = $"https://{settings.WeatherGeoHost.Trim()}/geo/v2/city/lookup?location={city}&number=1";
        using var geoDoc = JsonDocument.Parse(await GetStringAsync(geoUrl, key, cancellationToken));
        EnsureSuccess(geoDoc.RootElement);
        var location = geoDoc.RootElement.GetProperty("location")[0];
        var locationId = location.GetProperty("id").GetString()!;
        var cityName = location.GetProperty("name").GetString() ?? settings.WeatherCity;

        var host = settings.WeatherApiHost.Trim();
        var nowTask = GetStringAsync($"https://{host}/v7/weather/now?location={locationId}", key, cancellationToken);
        var dailyTask = GetStringAsync($"https://{host}/v7/weather/3d?location={locationId}", key, cancellationToken);
        var hourlyTask = GetStringAsync($"https://{host}/v7/weather/24h?location={locationId}", key, cancellationToken);
        await Task.WhenAll(nowTask, dailyTask, hourlyTask);

        using var nowDoc = JsonDocument.Parse(await nowTask);
        using var dailyDoc = JsonDocument.Parse(await dailyTask);
        using var hourlyDoc = JsonDocument.Parse(await hourlyTask);
        EnsureSuccess(nowDoc.RootElement);
        EnsureSuccess(dailyDoc.RootElement);
        EnsureSuccess(hourlyDoc.RootElement);

        var now = nowDoc.RootElement.GetProperty("now");
        var today = dailyDoc.RootElement.GetProperty("daily")[0];
        var snapshot = new WeatherSnapshot
        {
            City = cityName,
            Condition = now.GetProperty("text").GetString() ?? "--",
            IconCode = now.GetProperty("icon").GetString() ?? "999",
            Temperature = now.GetProperty("temp").GetString() ?? "--",
            TempMax = today.GetProperty("tempMax").GetString() ?? "--",
            TempMin = today.GetProperty("tempMin").GetString() ?? "--",
            UpdatedAt = DateTime.Now
        };

        foreach (var hour in hourlyDoc.RootElement.GetProperty("hourly").EnumerateArray().Take(5))
        {
            snapshot.Hourly.Add(new HourlyWeather
            {
                Time = DateTimeOffset.Parse(hour.GetProperty("fxTime").GetString()!).LocalDateTime,
                Temperature = hour.GetProperty("temp").GetString() ?? "--",
                IconCode = hour.GetProperty("icon").GetString() ?? "999"
            });
        }

        JsonStore.Save(Path.Combine(AppPaths.Cache, "weather.json"), snapshot);
        return snapshot;
    }

    private async Task<string> GetStringAsync(string url, string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-QW-Api-Key", apiKey);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static void EnsureSuccess(JsonElement root)
    {
        var code = root.TryGetProperty("code", out var value) ? value.GetString() : null;
        if (code != "200") throw new InvalidOperationException($"和风天气返回错误：{code ?? "未知"}");
    }
}

public static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true) ?? Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
            key.SetValue("DesktopWidgets", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("DesktopWidgets", false);
    }
}
