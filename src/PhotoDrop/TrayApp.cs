using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhotoDrop;

/// <summary>
/// The whole visible app: one tray icon driven by a hidden message-only window.
/// Everything with a window in it - the setup screen - is served as a page and opened
/// in the browser, which is why this needs no UI framework at all.
/// </summary>
sealed class TrayApp : IDisposable
{
    const uint IdShow = 1, IdOpenFolder = 2, IdChooseFolder = 3,
               IdStartup = 4, IdSettings = 5, IdExit = 6;

    static readonly IntPtr BalloonTimer = new(1);
    const uint BalloonDelayMs = 1500;

    readonly AppConfig _config;
    readonly Win32.WndProc _proc;      // field, so the GC can't collect the callback
    readonly object _gate = new();

    IntPtr _hwnd;
    IntPtr _icon;
    Win32.NOTIFYICONDATA _tray;
    int _savedInBatch;
    string? _lastFolder;
    bool _disposed;

    public TrayApp(AppConfig config)
    {
        _config = config;
        _proc = WndProc;
    }

    /// <summary>Creates the icon and pumps messages until Exit. Blocks the calling thread.</summary>
    public void Run()
    {
        const string className = "PhotoDropTrayWindow";
        var instance = Win32.GetModuleHandle(null);

        var cls = new Win32.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = instance,
            lpszClassName = className
        };
        Win32.RegisterClassEx(ref cls);

        // HWND_MESSAGE (-3) makes it invisible and keeps it off the taskbar.
        _hwnd = Win32.CreateWindowEx(0, className, null, 0, 0, 0, 0, 0,
                                     new IntPtr(-3), IntPtr.Zero, instance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            Dialogs.Warn("PhotoDrop couldn't create its tray icon.");
            return;
        }

        _icon = LoadAppIcon();
        _tray = new Win32.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = Win32.WM_TRAY,
            hIcon = _icon,
            szTip = Truncate($"{Program.AppName} - {Net.LocalAddresses()[0]}:{_config.Port}", 127),
            szInfo = "",
            szInfoTitle = ""
        };
        Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref _tray);

        // Without this the shell stays at version 0 and never sends NIN_BALLOONUSERCLICK,
        // so clicking the "photos received" balloon does nothing.
        _tray.uVersion = Win32.NOTIFYICON_VERSION;
        Win32.Shell_NotifyIcon(Win32.NIM_SETVERSION, ref _tray);

        Program.FileSaved += OnFileSaved;

        // Only on the very first run - every later launch starts silently.
        if (!_config.Introduced)
        {
            _config.Introduced = true;
            _config.Save();
            ShowSetup();
        }

        while (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }
    }

    IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32.WM_TRAY:
                // The event is the low word; higher bits carry the icon id on newer versions.
                switch ((int)(lParam.ToInt64() & 0xFFFF))
                {
                    case Win32.WM_RBUTTONUP: ShowMenu(); return IntPtr.Zero;
                    case Win32.WM_LBUTTONDBLCLK: ShowSetup(); return IntPtr.Zero;
                    case Win32.NIN_BALLOONUSERCLICK: OpenFolder(); return IntPtr.Zero;
                }
                return IntPtr.Zero;

            case Win32.WM_POST:
                // An upload landed; restart the quiet-period timer so a burst gets one balloon.
                Win32.KillTimer(_hwnd, BalloonTimer);
                Win32.SetTimer(_hwnd, BalloonTimer, BalloonDelayMs, IntPtr.Zero);
                return IntPtr.Zero;

            case Win32.WM_TIMER:
                if (wParam == BalloonTimer)
                {
                    Win32.KillTimer(_hwnd, BalloonTimer);
                    AnnounceBatch();
                }
                return IntPtr.Zero;

            case Win32.WM_DESTROY:
                Win32.PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    void ShowMenu()
    {
        var menu = Win32.CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            Win32.AppendMenu(menu, Win32.MF_STRING | Win32.MF_DEFAULT,
                             new UIntPtr(IdShow), "Show address for phone");
            Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(IdOpenFolder), "Open photo folder");
            Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(IdChooseFolder), "Choose folder...");
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);
            // Read the registry fresh: it can be changed from outside the app.
            Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(IdStartup),
                             AppConfig.RunsAtLogin ? "Disable running at startup"
                                                   : "Enable running at startup");
            Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(IdSettings), "Edit settings file...");
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);
            Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(IdExit), "Exit");

            Win32.GetCursorPos(out var point);
            // Required, or the menu won't dismiss when you click elsewhere.
            Win32.SetForegroundWindow(_hwnd);

            var chosen = Win32.TrackPopupMenuEx(
                menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD,
                point.X, point.Y, _hwnd, IntPtr.Zero);

            switch ((uint)chosen)
            {
                case IdShow: ShowSetup(); break;
                case IdOpenFolder: OpenFolder(); break;
                case IdChooseFolder: ChooseFolder(); break;
                case IdStartup: ToggleStartup(); break;
                case IdSettings: Program.OpenConfigFile(); break;
                case IdExit: Quit(); break;
            }
        }
        finally
        {
            Win32.DestroyMenu(menu);
        }
    }

    void OnFileSaved(string path)
    {
        lock (_gate)
        {
            _savedInBatch++;
            _lastFolder = Path.GetDirectoryName(path);
        }
        // Called from a Kestrel thread; the tray may only be touched on the message-loop thread.
        if (_hwnd != IntPtr.Zero) Win32.PostMessage(_hwnd, Win32.WM_POST, IntPtr.Zero, IntPtr.Zero);
    }

    void AnnounceBatch()
    {
        int count;
        lock (_gate)
        {
            count = _savedInBatch;
            _savedInBatch = 0;
        }
        if (count == 0) return;

        Balloon($"{count} {(count == 1 ? "photo" : "photos")} received",
                "Click here to open the folder.");
    }

    void Balloon(string title, string text)
    {
        _tray.uFlags = Win32.NIF_INFO;
        _tray.szInfoTitle = Truncate(title, 63);
        _tray.szInfo = Truncate(text, 255);
        _tray.dwInfoFlags = Win32.NIIF_INFO;
        Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref _tray);
        _tray.uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP;
    }

    void ShowSetup() => Open($"http://127.0.0.1:{_config.Port}/setup");

    void OpenFolder()
    {
        string target;
        lock (_gate)
        {
            target = _lastFolder is not null && Directory.Exists(_lastFolder)
                ? _lastFolder
                : _config.SaveFolder;
        }

        try
        {
            Directory.CreateDirectory(target);
            Open(target);
        }
        catch (Exception ex)
        {
            Dialogs.Warn($"Couldn't open the folder.\n\n{ex.Message}");
        }
    }

    void ChooseFolder()
    {
        var picked = Dialogs.PickFolder(_config.SaveFolder);
        if (picked is null) return;

        try
        {
            Directory.CreateDirectory(picked);
        }
        catch (Exception ex)
        {
            Dialogs.Warn($"PhotoDrop can't save photos there.\n\n{ex.Message}");
            return;
        }

        // Uploads read the folder from config each time, so this needs no restart.
        _config.SaveFolder = picked;
        _config.Save();
        lock (_gate) _lastFolder = null;

        Balloon("Folder changed", $"Photos will now be saved to {picked}");
    }

    void ToggleStartup()
    {
        var wanted = !AppConfig.RunsAtLogin;
        AppConfig.RunsAtLogin = wanted;

        if (AppConfig.RunsAtLogin != wanted)
        {
            Dialogs.Warn("Windows wouldn't let PhotoDrop change that setting.");
            return;
        }

        Balloon(Program.AppName, wanted
            ? "PhotoDrop will start automatically when you sign in."
            : "PhotoDrop will no longer start automatically.");
    }

    void Quit()
    {
        Dispose();
        Win32.PostQuitMessage(0);
    }

    static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { Dialogs.Warn($"Couldn't open {target}.\n\n{ex.Message}"); }
    }

    static IntPtr LoadAppIcon()
    {
        // The icon is baked into the exe by ApplicationIcon, so pull it back out of there.
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) &&
                Win32.ExtractIconEx(exe, 0, out var large, out var small, 1) > 0)
            {
                if (large != IntPtr.Zero) Win32.DestroyIcon(large);
                if (small != IntPtr.Zero) return small;
            }
        }
        catch { }
        return IntPtr.Zero;   // Windows falls back to a blank slot rather than failing
    }

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Program.FileSaved -= OnFileSaved;

        if (_hwnd != IntPtr.Zero)
        {
            Win32.KillTimer(_hwnd, BalloonTimer);
            Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref _tray);
            Win32.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        if (_icon != IntPtr.Zero)
        {
            Win32.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }
}
