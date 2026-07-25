using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using MyFanControl.Modern.ViewModels;

namespace MyFanControl.Modern;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly NotifyIcon _trayIcon;
    private bool _allowClose;
    private bool _shutdownCompleted;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _trayIcon = new NotifyIcon
        {
            Text = "MyFanControl Modern",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();

        Loaded += (_, _) =>
        {
            _viewModel.Start();
            if (Environment.GetCommandLineArgs().Contains("--minimized"))
                HideToTray();
        };
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => RestoreWindow());
        menu.Items.Add("强制冷却", null, (_, _) => _viewModel.ForceCoolingCommand.Execute(null));
        menu.Items.Add("恢复原厂自动控制", null, (_, _) => _viewModel.RestoreAutomaticCommand.Execute(null));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            _allowClose = true;
            Close();
        });
        return menu;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        _trayIcon.ShowBalloonTip(
            1500,
            "MyFanControl Modern",
            "程序仍在后台运行；退出时会恢复原厂风扇控制。",
            ToolTipIcon.Info);
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        if (_shutdownCompleted)
            return;

        e.Cancel = true;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        await _viewModel.DisposeAsync();
        _shutdownCompleted = true;
        Close();
    }
}
