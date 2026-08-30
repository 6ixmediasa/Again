using System.Diagnostics;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Again.App;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    public bool IsExiting { get; private set; }
    public MainWindow? MainWindowInstance { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindowInstance = new MainWindow();
        MainWindow = MainWindowInstance;
        MainWindowInstance.Show();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show AGAIN", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Pause monitoring", null, (_, _) => MainWindowInstance?.PauseMonitoringFromTray());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "AGAIN — Do it once. Never do it twice.",
            ContextMenuStrip = menu,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule!.FileName!) ?? System.Drawing.SystemIcons.Application
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (MainWindowInstance is null) return;
        MainWindowInstance.Show();
        MainWindowInstance.WindowState = WindowState.Normal;
        MainWindowInstance.Activate();
    }

    public void ExitApplication()
    {
        IsExiting = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        MainWindowInstance?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
