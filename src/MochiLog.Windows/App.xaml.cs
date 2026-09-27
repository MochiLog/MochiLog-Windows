using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MochiLog_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MochiLog_Windows;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private static readonly Mutex Instance = new(false,
        @"Local\MochiLog-Windows-9F56A495-2D39-440B-A51B-DC9E26763C35");
    private static readonly EventWaitHandle ShowRequest = new(false, EventResetMode.AutoReset,
        @"Local\MochiLog-Windows-Show-9F56A495-2D39-440B-A51B-DC9E26763C35");
    private Window? _window;
    private bool _ownsInstance;
    public MainWindow? MainWindow => _window as MainWindow;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try { _ownsInstance = Instance.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsInstance = true; }
        if (!_ownsInstance) { ShowRequest.Set(); Exit(); return; }
        CompanionRuntime.Shared.Start();
        _window = new MainWindow();
        _window.Closed += (_, _) => {
            CompanionRuntime.Shared.Dispose();
            if (_ownsInstance) { Instance.ReleaseMutex(); _ownsInstance = false; }
            ShowRequest.Set();
        };
        _window.Activate();
        if (TrayPreferences.Enabled && Environment.GetCommandLineArgs().Contains("--background"))
            _window.AppWindow.Hide();
        _ = Task.Run(() => {
            while (_ownsInstance) {
                ShowRequest.WaitOne();
                if (_ownsInstance)
                    _window.DispatcherQueue.TryEnqueue(() => MainWindow?.Restore());
            }
        });
    }
}
