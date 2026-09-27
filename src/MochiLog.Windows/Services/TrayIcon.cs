using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MochiLog_Windows.Services;

// WinUI has no notification-area control. Use the shell API directly so the
// self-contained app does not pull a second UI framework into its build.
public sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8000 + 31;
    private const uint DoubleClick = 0x0203;
    private const uint RightButtonUp = 0x0205;
    private const uint ContextMenu = 0x007B;
    private const uint IconId = 1;
    private readonly IntPtr _window;
    private readonly IntPtr _icon;
    private readonly bool _ownsIcon;
    private readonly Action _open;
    private readonly Action _exit;
    private readonly string _openLabel;
    private readonly string _exitLabel;
    private readonly SubclassProc _procedure;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private bool _disposed;

    public TrayIcon(IntPtr window, string iconPath, string openLabel, string exitLabel,
        Action open, Action exit)
    {
        _window = window;
        _open = open;
        _exit = exit;
        _openLabel = openLabel;
        _exitLabel = exitLabel;
        _icon = LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x10 | 0x40);
        _ownsIcon = _icon != IntPtr.Zero;
        if (_icon == IntPtr.Zero) _icon = LoadIcon(IntPtr.Zero, new IntPtr(32512));
        _procedure = HandleMessage;
        if (!SetWindowSubclass(_window, _procedure, new UIntPtr(IconId), UIntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try { Add(); }
        catch {
            RemoveWindowSubclass(_window, _procedure, new UIntPtr(IconId));
            if (_ownsIcon) DestroyIcon(_icon);
            throw;
        }
    }

    private NotifyIconData Data() => new() {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = _window,
        Id = IconId, Flags = 0x1 | 0x2 | 0x4, Callback = CallbackMessage,
        Icon = _icon, Tip = "MochiLog Windows"
    };

    private void Add()
    {
        var data = Data();
        if (!Shell_NotifyIcon(0, ref data))
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "Could not add the MochiLog notification-area icon.");
    }

    private IntPtr HandleMessage(IntPtr window, uint message, IntPtr wParam,
        IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        if (message == _taskbarCreated) Add();
        if (message == CallbackMessage) {
            switch ((uint)lParam.ToInt64()) {
                case DoubleClick: _open(); break;
                case RightButtonUp:
                case ContextMenu: ShowMenu(); break;
            }
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try {
            AppendMenu(menu, 0, 1, _openLabel);
            AppendMenu(menu, 0x800, 0, null);
            AppendMenu(menu, 0, 2, _exitLabel);
            GetCursorPos(out var point);
            SetForegroundWindow(_window);
            var selected = TrackPopupMenu(menu, 0x100 | 0x2, point.X, point.Y,
                0, _window, IntPtr.Zero);
            if (selected == 1) _open();
            else if (selected == 2) _exit();
        }
        finally { DestroyMenu(menu); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var data = Data();
        Shell_NotifyIcon(2, ref data);
        RemoveWindowSubclass(_window, _procedure, new UIntPtr(IconId));
        if (_ownsIcon) DestroyIcon(_icon);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public uint Flags;
        public uint Callback;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProc(IntPtr window, uint message, IntPtr wParam,
        IntPtr lParam, UIntPtr id, UIntPtr data);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr window, SubclassProc procedure,
        UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc procedure,
        UIntPtr id);
    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr window, uint message,
        IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint action, ref NotifyIconData data);
    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type,
        int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "LoadIconW")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string? label);
    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y,
        int reserved, IntPtr window, IntPtr rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}
