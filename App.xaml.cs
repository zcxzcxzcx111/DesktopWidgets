using System.Threading;
using System.Windows;

namespace DesktopWidgets;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private DesktopWidgetManager? _manager;
    private EventWaitHandle? _openSettingsEvent;
    private CancellationTokenSource? _eventCancellation;
    public static bool IsPreviewMode { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        IsPreviewMode = e.Args.Contains("--preview", StringComparer.OrdinalIgnoreCase);
        _singleInstance = new Mutex(true, "DesktopWidgets.SingleInstance", out var created);
        if (!created)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting("DesktopWidgets.OpenSettings");
                signal.Set();
            }
            catch { }
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write("Unhandled UI error", args.Exception);
            MessageBox.Show("桌面小组件遇到错误，详细信息已写入日志。", "桌面小组件",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _manager = new DesktopWidgetManager();
        _manager.Start();
        Deactivated += (_, _) => _manager?.EnsureWidgetsStayOnDesktop();
        _openSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "DesktopWidgets.OpenSettings");
        _eventCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ListenForSettingsSignal(_eventCancellation.Token));
    }

    private void ListenForSettingsSignal(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_openSettingsEvent?.WaitOne(500) == true)
                Dispatcher.BeginInvoke(() => _manager?.OpenSettings());
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _manager?.Dispose();
        _eventCancellation?.Cancel();
        _openSettingsEvent?.Dispose();
        _eventCancellation?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
