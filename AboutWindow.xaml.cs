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

    private void OpenFachportal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://fachportal.gematik.de/ti-status#TI-Anschluss",
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
