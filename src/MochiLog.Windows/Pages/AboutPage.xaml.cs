using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Services;

namespace MochiLog_Windows.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = UiText.Format("win_version",
            typeof(AboutPage).Assembly.GetName().Version?.ToString() ?? "0.1.0");
        AboutText.Text = UiText.Get("win_note");
        RepositoryButton.Content = UiText.Get("win_repo");
        LicenseButton.Content = UiText.Get("win_license");
    }

    private void RepositoryClicked(object sender, RoutedEventArgs args) =>
        Process.Start(new ProcessStartInfo("https://github.com/MochiLog/MochiLog-Windows") {
            UseShellExecute = true
        });

    private void LicenseClicked(object sender, RoutedEventArgs args)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Licenses");
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") {
                UseShellExecute = true
            });
    }
}
