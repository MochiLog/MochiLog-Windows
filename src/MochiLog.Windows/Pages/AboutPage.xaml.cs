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
        PrivacyLink.Content = UiText.Get("mt_l_07");
        TermsLink.Content = UiText.Get("mt_l_08");
    }

    private void RepositoryClicked(object sender, RoutedEventArgs args) =>
        Process.Start(new ProcessStartInfo("https://github.com/MochiLog/MochiLog-Windows") {
            UseShellExecute = true
        });

    private void LicenseClicked(object sender, RoutedEventArgs args) =>
        Frame.Navigate(typeof(LicensesPage));
}
