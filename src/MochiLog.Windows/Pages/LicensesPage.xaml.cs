using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Services;

namespace MochiLog_Windows.Pages;

public sealed partial class LicensesPage : Page
{
    private string[] _documents = [];

    public LicensesPage()
    {
        InitializeComponent();
        PageTitle.Text = UiText.Get("win_license");
        var root = Path.Combine(AppContext.BaseDirectory, "Licenses");
        _documents = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => new[] { ".txt", ".md", "" }.Contains(
                    Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).StartsWith("NOTICE", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path).Contains("MochiLog", StringComparison.OrdinalIgnoreCase)
                    ? 0 : 1)
                .ThenBy(path => Path.GetRelativePath(root, path), StringComparer.CurrentCulture)
                .ToArray() : [];
        DocumentList.ItemsSource = _documents.Select(path => Path.GetRelativePath(root, path)).ToArray();
        if (_documents.Length > 0) DocumentList.SelectedIndex = 0;
        else DocumentText.Text = UiText.Get("win_license_missing");
    }

    private async void DocumentChanged(object sender, SelectionChangedEventArgs args)
    {
        var index = DocumentList.SelectedIndex;
        if (index < 0 || index >= _documents.Length) return;
        try { DocumentText.Text = await File.ReadAllTextAsync(_documents[index]); }
        catch (Exception error) { DocumentText.Text = error.Message; }
    }
}
