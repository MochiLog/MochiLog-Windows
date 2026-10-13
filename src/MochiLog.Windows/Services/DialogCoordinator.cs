using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;

namespace MochiLog_Windows.Services;

// WinUI permits only one ContentDialog for a XamlRoot at a time. USB setup,
// pairing, and update checks can complete independently on the UI thread.
public static class DialogCoordinator
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static ContentDialog? _active;
    private static int _generation;

    public static void HideActive() { _generation++; _active?.Hide(); }

    public static async Task<ContentDialogResult?> ShowAsync(ContentDialog dialog)
    {
        var generation = _generation;
        await Gate.WaitAsync();
        try {
            if (generation != _generation || dialog.XamlRoot?.IsHostVisible != true) return null;
            _active = dialog;
            return await dialog.ShowAsync();
        }
        catch (COMException) { return null; }
        catch (InvalidOperationException) { return null; }
        finally { if (ReferenceEquals(_active, dialog)) _active = null; Gate.Release(); }
    }
}
