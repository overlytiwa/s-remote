using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace SamsungMiniRemote;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private Icon? _appIcon;
    private MainWindow? _remote;

    internal bool IsExiting { get; private set; }

    // ── Win32 APIs to force the restored window into the foreground ──
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keeping a window hidden must NOT automatically terminate the app.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        CreateTrayIcon();

        _remote = new MainWindow();
        MainWindow = _remote;
        _remote.Show();
    }

    private void CreateTrayIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "remote.ico");
        _appIcon = File.Exists(path) ? new Icon(path) : (Icon)SystemIcons.Application.Clone();

        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add("Open S-Remote", null, (_, _) => Dispatcher.Invoke(ShowRemote));
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = "S-Remote",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };

        // Single click AND double click both restore the window.
        // MouseClick fires on every click; we use it instead of DoubleClick
        // to avoid the delay inherent in waiting for a double-click.
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
                Dispatcher.Invoke(ShowRemote);
        };
    }

    private void ShowRemote()
    {
        if (_remote is null) return;

        // 1. Ensure the window is shown and not collapsed/hidden.
        _remote.Show();

        // 2. Ensure ShowInTaskbar is true.
        _remote.ShowInTaskbar = true;

        // 3. If minimized, restore to normal state.
        if (_remote.WindowState == WindowState.Minimized)
            _remote.WindowState = WindowState.Normal;

        // 4. WPF Activate alone is unreliable; use Win32 to force foreground.
        _remote.Activate();

        // 5. Get the Win32 handle and force-show + foreground.
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_remote).Handle;
        if (hwnd != IntPtr.Zero)
        {
            if (IsIconic(hwnd))
                ShowWindow(hwnd, SW_RESTORE);

            SetForegroundWindow(hwnd);
        }

        // 6. Give keyboard focus.
        _remote.Focus();
    }

    private async void ExitApplication()
    {
        IsExiting = true;

        // Clean up WebSocket connections and background tasks.
        if (_remote != null)
        {
            try { await _remote.CleanupAsync(); }
            catch { /* Best effort */ }
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _trayMenu?.Dispose();
        _appIcon?.Dispose();
        base.OnExit(e);
    }
}
