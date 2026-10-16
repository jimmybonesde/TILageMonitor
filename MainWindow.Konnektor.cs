using System.Windows;
using TILageMonitor.Konnektor;

namespace TILageMonitor;

/// <summary>Local TI-Konnektor monitoring (read-only): own timer, card, toasts, detail window.</summary>
public partial class MainWindow
{
    private System.Windows.Threading.DispatcherTimer? _konnektorTimer;
    private readonly SemaphoreSlim _konnektorGate = new(1, 1);
    private CancellationTokenSource? _konnektorCts;
    private KonnektorSnapshot? _lastKonnektor;
    private KonnektorWindow? _konnektorWindow;
    private string? _lastOverallKind;
    private bool? _lastVpnTiOnline;
    private bool? _lastKonnektorReachable;
    private readonly HashSet<string> _notifiedKonnektorWarnings = new(StringComparer.Ordinal);

    private static string KL(string de, string en) => KonnektorClient.L(de, en);

    public KonnektorSnapshot? LastKonnektorSnapshot => _lastKonnektor;

    /// <summary>(Re)starts or stops the Konnektor timer according to the current settings.</summary>
    public void ConfigureKonnektorMonitoring()
    {
        var cfg = _settings.Konnektor ??= new KonnektorSettings();
        var active = cfg.Enabled && cfg.IsConfigured && !string.IsNullOrWhiteSpace(cfg.PinnedFingerprint);

        KonnektorCard.Visibility = cfg.IsConfigured ? Visibility.Visible : Visibility.Collapsed;

        if (!active)
        {
            _konnektorTimer?.Stop();
            if (cfg.IsConfigured && _lastKonnektor is null)
            {
                KonnektorClassificationText.Text = string.IsNullOrWhiteSpace(cfg.PinnedFingerprint)
                    ? KL("Zertifikat noch nicht bestätigt – in den Einstellungen „Verbindung testen“.", "Certificate not confirmed yet – use “Test connection” in Settings.")
                    : KL("Regelmäßige Prüfung ist ausgeschaltet.", "Periodic check is turned off.");
            }
            return;
        }

        var interval = TimeSpan.FromMinutes(KonnektorSettings.ClampRefreshMinutes(cfg.RefreshMinutes));
        if (_konnektorTimer is null)
        {
            _konnektorTimer = new System.Windows.Threading.DispatcherTimer();
            _konnektorTimer.Tick += async (_, _) =>
            {
                try { await RefreshKonnektorAsync(userInitiated: false); }
                catch (Exception ex) { App.LogUnhandled("KonnektorTimer", ex); }
            };
        }

        var wasRunning = _konnektorTimer.IsEnabled;
        _konnektorTimer.Interval = interval;
        _konnektorTimer.Start();
        if (!wasRunning)
            _ = RefreshKonnektorSafeAsync();
    }

    private async Task RefreshKonnektorSafeAsync()
    {
        try { await RefreshKonnektorAsync(userInitiated: false); }
        catch (Exception ex) { App.LogUnhandled("KonnektorRefresh", ex); }
    }

    private void StopKonnektorMonitoring()
    {
        try { _konnektorTimer?.Stop(); } catch { /* ignore */ }
        try { _konnektorCts?.Cancel(); } catch { /* ignore */ }
    }

    /// <summary>Queries the connector once and updates card, detail window and toasts.</summary>
    public async Task<KonnektorSnapshot?> RefreshKonnektorAsync(bool userInitiated)
    {
        var cfg = _settings.Konnektor;
        if (cfg is null || !cfg.IsConfigured || _disposed)
            return null;

        if (!await _konnektorGate.WaitAsync(0))
            return _lastKonnektor;

        try
        {
            KonnektorRefreshButton.IsEnabled = false;
            _konnektorCts?.Dispose();
            _konnektorCts = new CancellationTokenSource();

            var snapshot = await Task.Run(async () =>
            {
                var store = KonnektorSecretStore.CreateDefault();
                System.Security.Cryptography.X509Certificates.X509Certificate2? clientCert = null;
                try
                {
                    var secret = store.LoadClientCertificate();
                    if (secret is { } s)
                        clientCert = KonnektorClient.LoadClientCertificate(s.P12, s.Password);
                }
                catch (Exception ex)
                {
                    return new KonnektorSnapshot
                    {
                        Error = KL("Clientzertifikat konnte nicht geladen werden: ", "Client certificate could not be loaded: ") + ex.Message
                    };
                }

                using (clientCert)
                using (var client = new KonnektorClient(cfg, clientCert))
                {
                    return await client.CollectAsync(_konnektorCts.Token);
                }
            });

            if (_disposed)
                return snapshot;

            var previous = _lastKonnektor;
            _lastKonnektor = snapshot;
            RenderKonnektor(snapshot);
            _konnektorWindow?.Render(snapshot);
            NotifyKonnektorTransitions(previous, snapshot);
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            return _lastKonnektor;
        }
        finally
        {
            if (!_disposed)
                KonnektorRefreshButton.IsEnabled = true;
            _konnektorGate.Release();
        }
    }

    private void RenderKonnektor(KonnektorSnapshot s)
    {
        SetChip(KonnektorReachChip, s.Reachable ? "StatusOk" : "StatusOutage");
        KonnektorReachText.Text = s.Reachable ? KL("Erreichbar", "Reachable") : KL("Nicht erreichbar", "Unreachable");
        SetChip(KonnektorVpnTiChip, ChipKey(s.Resource?.VpnTiOnline));
        SetChip(KonnektorVpnSisChip, ChipKey(s.Resource?.VpnSisOnline));

        UpdateKonnektorClassification();

        var details = new List<string>();
        if (s.Error is not null) details.Add(s.Error);
        if (s.ResourceError is not null) details.Add(s.ResourceError);
        details.AddRange(s.Warnings.Take(3));
        if (s.Warnings.Count > 3)
            details.Add(KL($"… und {s.Warnings.Count - 3} weitere Hinweise", $"… and {s.Warnings.Count - 3} more notices"));
        var product = s.Sds?.Product;
        var productLine = product is null ? "" : $"{product.VendorName ?? product.VendorId} {product.ProductName ?? product.ProductCode} · FW {product.FirmwareVersion} · ";
        details.Add($"{productLine}{KL("Karten", "Cards")}: {s.Cards.Count} · {KL("Terminals", "Terminals")}: {s.Terminals.Count} · {KL("Stand", "As of")} {s.CheckedAt:HH:mm}");
        KonnektorDetailText.Text = string.Join("\n", details);
    }

    private void UpdateKonnektorClassification()
    {
        if (_lastKonnektor is null || KonnektorClassificationText is null)
            return;
        KonnektorClassificationText.Text = KonnektorPresentation.Classification(_lastOverallKind, _lastKonnektor.Health);
        KonnektorClassificationText.SetResourceReference(
            System.Windows.Controls.TextBlock.ForegroundProperty,
            _lastKonnektor.Health switch
            {
                KonnektorHealth.Down => "StatusOutage",
                KonnektorHealth.Warning => "StatusPartial",
                _ => "TextMain"
            });
    }

    private static string ChipKey(bool? online) => online switch
    {
        true => "StatusOk",
        false => "StatusOutage",
        _ => "StatusEmpty"
    };

    private static void SetChip(System.Windows.Controls.Border chip, string key) =>
        chip.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, key);

    private void NotifyKonnektorTransitions(KonnektorSnapshot? previous, KonnektorSnapshot current)
    {
        // Reachability
        if (_lastKonnektorReachable == true && !current.Reachable)
            ToastService.Show("TI-Lage Monitor",
                KL("Konnektor nicht erreichbar – lokal gestört.", "Connector unreachable – local fault."), ToastUrgency.Error);
        else if (_lastKonnektorReachable == false && current.Reachable)
            ToastService.Show("TI-Lage Monitor", KL("Konnektor wieder erreichbar.", "Connector reachable again."), ToastUrgency.Info);
        _lastKonnektorReachable = current.Reachable;

        // VPN TI
        var vpn = current.Resource?.VpnTiOnline;
        if (vpn is not null)
        {
            if (_lastVpnTiOnline == true && vpn == false)
                ToastService.Show("TI-Lage Monitor",
                    KL("VPN TI des Konnektors ist offline.", "Connector VPN TI is offline.") + " " +
                    KonnektorPresentation.Classification(_lastOverallKind, current.Health),
                    ToastUrgency.Error);
            else if (_lastVpnTiOnline == false && vpn == true)
                ToastService.Show("TI-Lage Monitor", KL("VPN TI wieder online.", "VPN TI back online."), ToastUrgency.Info);
            _lastVpnTiOnline = vpn;
        }

        // Expiry / PIN warnings: once per distinct text per app session.
        foreach (var warning in current.Warnings)
        {
            if (_notifiedKonnektorWarnings.Add(warning))
                ToastService.Show("TI-Lage Monitor", warning, ToastUrgency.Warning);
        }

        if (current.PinMismatch && previous?.PinMismatch != true)
            ToastService.Show("TI-Lage Monitor",
                KL("Konnektor-Zertifikat hat sich geändert – Verbindung blockiert. Bitte in den Einstellungen prüfen.",
                   "Connector certificate changed – connection blocked. Please verify in Settings."),
                ToastUrgency.Error);
    }

    private async void KonnektorRefresh_Click(object sender, RoutedEventArgs e)
    {
        try { await RefreshKonnektorAsync(userInitiated: true); }
        catch (Exception ex) { App.LogUnhandled("KonnektorRefreshClick", ex); }
    }

    private void KonnektorDetails_Click(object sender, RoutedEventArgs e) => ShowKonnektorWindow();

    public void ShowKonnektorWindow()
    {
        if (_konnektorWindow is null)
        {
            _konnektorWindow = new KonnektorWindow(this);
            _konnektorWindow.Closed += (_, _) => _konnektorWindow = null;
        }

        _konnektorWindow.Render(_lastKonnektor);
        _konnektorWindow.Show();
        _konnektorWindow.Activate();
    }
}
