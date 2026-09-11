using System.Windows;

namespace TILageMonitor;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _ownerMain;
    private bool _initializing;
    private AppSettings _settings;

    private static readonly Dictionary<string, string> ServiceNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["erezept"] = "eRezept",
            ["epa"] = "ePA",
            ["kim"] = "KIM",
            ["wanda"] = "WANDA",
            ["ogd"] = "OGD",
            ["vsdm"] = "VSDM",
            ["tianschluss"] = "TI-Anschluss"
        };

    public SettingsWindow(MainWindow owner)
    {
        InitializeComponent();
        Owner = owner;
        _ownerMain = owner;
        _settings = owner.GetSettingsSnapshot();

        _initializing = true;
        DarkModeToggle.IsChecked = _settings.DarkMode;
        AutoStartToggle.IsChecked = _settings.AutoStart;
        BuildNotifyCheckboxes();
        _initializing = false;
    }

    /// <summary>Refresh toggles if MainWindow changed settings externally (e.g. tray Autostart).</summary>
    public void SyncFrom(AppSettings settings)
    {
        _settings = settings;
        _initializing = true;
        DarkModeToggle.IsChecked = settings.DarkMode;
        AutoStartToggle.IsChecked = settings.AutoStart;
        BuildNotifyCheckboxes();
        _initializing = false;
    }

    private void BuildNotifyCheckboxes()
    {
        NotifyServicesPanel.Children.Clear();

        foreach (var key in AppSettings.ServiceKeys)
        {
            var name = ServiceNames.TryGetValue(key, out var n) ? n : key;
            var enabled = _settings.IsNotifyEnabled(key);

            var cb = new System.Windows.Controls.CheckBox
            {
                Content = name,
                IsChecked = enabled,
                Margin = new Thickness(0, 4, 18, 4),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                Tag = key,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            cb.Checked += NotifyService_Changed;
            cb.Unchecked += NotifyService_Changed;
            NotifyServicesPanel.Children.Add(cb);
        }
    }

    private void NotifyService_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
            return;

        if (sender is not System.Windows.Controls.CheckBox cb || cb.Tag is not string key)
            return;

        _settings.NotifyServices[key] = cb.IsChecked == true;
        SettingsStore.Save(_settings);
        _ownerMain.ApplySettings(_settings);
    }

    private void DarkModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
            return;

        var dark = DarkModeToggle.IsChecked == true;
        ThemeService.ApplyTheme(dark);
        _settings.DarkMode = dark;
        SettingsStore.Save(_settings);
        _ownerMain.ApplySettings(_settings, themeChanged: true);

        // Rebuild checkboxes so TextMain brush updates
        _initializing = true;
        BuildNotifyCheckboxes();
        _initializing = false;
    }

    private void AutoStartToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
            return;

        var enabled = AutoStartToggle.IsChecked == true;
        _settings.AutoStart = enabled;
        SettingsStore.Save(_settings);
        AutostartService.SetEnabled(enabled);
        _ownerMain.ApplySettings(_settings);
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Prüfe GitHub-Release …";

        try
        {
            var result = await _ownerMain.CheckForUpdatesAsync(userInitiated: true);
            UpdateStatusText.Text = result.IsSuccess
                ? result.IsUpdateAvailable
                    ? $"Update {result.LatestVersion} verfügbar."
                    : $"Aktuell: Version {result.CurrentVersion}."
                : result.ErrorMessage ?? "Update-Prüfung fehlgeschlagen.";
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
