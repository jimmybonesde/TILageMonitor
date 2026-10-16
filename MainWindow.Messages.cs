using System.Windows;

namespace TILageMonitor;

/// <summary>Collapsible "Aktuelle Meldungen" cards.</summary>
public partial class MainWindow
{
    private readonly HashSet<string> _expandedMessageIds = new(StringComparer.Ordinal);

    /// <summary>ToggleButton (click / Enter / Space) flips IsExpanded via TwoWay binding; remember the state.</summary>
    private void MessageToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MessageRow row })
            return;
        if (row.IsExpanded)
            _expandedMessageIds.Add(row.Id);
        else
            _expandedMessageIds.Remove(row.Id);
    }
}
