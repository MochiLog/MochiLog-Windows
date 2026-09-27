using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;

namespace MochiLog_Windows.Services;

// WinUI permits only one ContentDialog for a XamlRoot at a time. USB setup,
// pairing, and update checks can complete independently on the UI thread.
public static class DialogCoordinator
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<ContentDialogResult?> ShowAsync(ContentDialog dialog)
    {
        await Gate.WaitAsync();
        try { return await dialog.ShowAsync(); }
        catch (COMException) { return null; }
        catch (InvalidOperationException) { return null; }
        finally { Gate.Release(); }
    }
}
