using System.Diagnostics;
using System.Windows;

namespace TILageMonitor;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {MainWindow.GetDisplayVersion()}";
    }

    private void OpenFachportal_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://fachportal.gematik.de/ti-status#TI-Anschluss");

    private void OpenHomepage_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://www.jimmybones.de");

    private void OpenEmail_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("mailto:Webmaster@JimmyBones.de");

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
