using System.IO;
using System.Globalization;
using System.Windows;
using TILageMonitor.Konnektor;

namespace TILageMonitor;

/// <summary>Settings section "Konnektor": configuration, P12 import (DPAPI), trust-on-first-use, connection test.</summary>
public partial class SettingsWindow
{
    private byte[]? _pendingP12;
    private string? _pendingP12Name;

    private static string KL(string de, string en) => KonnektorClient.L(de, en);

    private void LoadKonnektorFields()
    {
        var k = _settings.Konnektor ??= new KonnektorSettings();
        KonnektorEnabledToggle.IsChecked = k.Enabled;
        KonnektorHostBox.Text = k.Host;
        KonnektorPortBox.Text = k.Port.ToString(CultureInfo.InvariantCulture);
        KonnektorMandantBox.Text = k.MandantId;
        KonnektorClientSystemBox.Text = k.ClientSystemId;
        KonnektorWorkplaceBox.Text = k.WorkplaceId;
        KonnektorUserBox.Text = k.UserId ?? "";
        KonnektorIntervalBox.Text = KonnektorSettings.ClampRefreshMinutes(k.RefreshMinutes).ToString(CultureInfo.InvariantCulture);
        UpdateKonnektorInfoTexts();
    }

    private void UpdateKonnektorInfoTexts()
    {
        var store = KonnektorSecretStore.CreateDefault();
        KonnektorP12StatusText.Text = _pendingP12Name is not null
            ? KL($"Ausgewählt: {_pendingP12Name} (wird beim Speichern verschlüsselt)", $"Selected: {_pendingP12Name} (encrypted on save)")
            : store.HasClientCertificate
                ? KL("Zertifikat verschlüsselt hinterlegt", "Certificate stored encrypted")
                : KL("Kein Zertifikat hinterlegt", "No certificate stored");
        var pin = _settings.Konnektor?.PinnedFingerprint;
        KonnektorPinText.Text = string.IsNullOrWhiteSpace(pin)
            ? KL("Konnektor-Zertifikat: noch nicht vertraut", "Connector certificate: not trusted yet")
            : KL("Vertrauter Fingerprint (SHA-256): ", "Trusted fingerprint (SHA-256): ") + pin;
    }

    private void KonnektorImportP12_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = KL("Clientsystem-Zertifikat auswählen", "Select client system certificate"),
            Filter = "PKCS#12 (*.p12;*.pfx)|*.p12;*.pfx|*.*|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > 1024 * 1024)
                throw new InvalidDataException(KL("Datei ist zu groß für ein P12.", "File is too large for a P12."));
            _pendingP12 = File.ReadAllBytes(dialog.FileName);
            _pendingP12Name = info.Name;
            KonnektorStatusText.Text = KL("Bitte Passwort eingeben und „Speichern“ oder „Verbindung testen“ klicken.",
                "Enter the password and click “Save” or “Test connection”.");
        }
        catch (Exception ex)
        {
            KonnektorStatusText.Text = ex.Message;
        }

        UpdateKonnektorInfoTexts();
    }

    /// <summary>Validates and persists fields + secrets. Returns false with a message on invalid input.</summary>
    private bool SaveKonnektorSettings()
    {
        var k = _settings.Konnektor ??= new KonnektorSettings();

        var host = KonnektorHostBox.Text.Trim();
        if (!int.TryParse(KonnektorPortBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            KonnektorStatusText.Text = KL("Ungültiger Port (1–65535).", "Invalid port (1–65535).");
            return false;
        }

        if (!int.TryParse(KonnektorIntervalBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
            minutes = 5;

        try
        {
            if (host.Length > 0)
                _ = KonnektorSettings.BuildBaseUri(host, port);
        }
        catch (UriFormatException)
        {
            KonnektorStatusText.Text = KL("Ungültige IP-Adresse oder Hostname.", "Invalid IP address or host name.");
            return false;
        }

        var store = KonnektorSecretStore.CreateDefault();
        var password = KonnektorPasswordBox.Password;
        try
        {
            if (_pendingP12 is not null)
            {
                using (KonnektorClient.LoadClientCertificate(_pendingP12, password)) { }
                store.SaveClientCertificate(_pendingP12, password);
                _pendingP12 = null;
                _pendingP12Name = null;
                KonnektorPasswordBox.Clear();
            }
            else if (password.Length > 0 && store.LoadClientCertificate() is { } existing)
            {
                using (KonnektorClient.LoadClientCertificate(existing.P12, password)) { }
                store.SaveClientCertificate(existing.P12, password);
                KonnektorPasswordBox.Clear();
            }
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            KonnektorStatusText.Text = KL("P12 konnte nicht geöffnet werden (Passwort falsch?): ", "P12 could not be opened (wrong password?): ") + ex.Message;
            return false;
        }

        var hostChanged = !string.Equals(k.Host, host, StringComparison.OrdinalIgnoreCase) || k.Port != port;
        k.Host = host;
        k.Port = port;
        k.MandantId = KonnektorMandantBox.Text.Trim();
        k.ClientSystemId = KonnektorClientSystemBox.Text.Trim();
        k.WorkplaceId = KonnektorWorkplaceBox.Text.Trim();
        k.UserId = string.IsNullOrWhiteSpace(KonnektorUserBox.Text) ? null : KonnektorUserBox.Text.Trim();
        k.RefreshMinutes = KonnektorSettings.ClampRefreshMinutes(minutes);
        k.Enabled = KonnektorEnabledToggle.IsChecked == true;
        if (hostChanged)
            k.PinnedFingerprint = null; // new target → trust must be confirmed again

        SettingsStore.Save(_settings);
        _ownerMain.ApplySettings(_settings);
        KonnektorIntervalBox.Text = k.RefreshMinutes.ToString(CultureInfo.InvariantCulture);
        UpdateKonnektorInfoTexts();
        return true;
    }

    private void KonnektorSave_Click(object sender, RoutedEventArgs e)
    {
        if (SaveKonnektorSettings())
            KonnektorStatusText.Text = KL("Gespeichert.", "Saved.");
    }

    private async void KonnektorTest_Click(object sender, RoutedEventArgs e)
    {
        KonnektorTestButton.IsEnabled = false;
        try
        {
            if (!SaveKonnektorSettings())
                return;

            var k = _settings.Konnektor;
            if (!k.IsConfigured)
            {
                KonnektorStatusText.Text = KL("Bitte IP/Host, MandantId, ClientSystemId und WorkplaceId ausfüllen.",
                    "Please fill in IP/host, MandantId, ClientSystemId and WorkplaceId.");
                return;
            }

            KonnektorStatusText.Text = KL("Verbinde mit Konnektor …", "Connecting to connector …");

            var probe = await Task.Run(async () =>
            {
                var secret = KonnektorSecretStore.CreateDefault().LoadClientCertificate();
                using var cert = secret is { } s ? KonnektorClient.LoadClientCertificate(s.P12, s.Password) : null;
                using var client = new KonnektorClient(k, cert, TimeSpan.FromSeconds(15));
                return await client.ProbeCertificateAsync(CancellationToken.None);
            });

            if (probe.Certificate is null)
            {
                KonnektorStatusText.Text = KL("Keine TLS-Verbindung: ", "No TLS connection: ") + (probe.Error ?? "?");
                return;
            }

            if (probe.Pin != PinEvaluation.Match)
            {
                var dialog = new TrustCertificateWindow(probe.Certificate, probe.Pin == PinEvaluation.Mismatch, k.PinnedFingerprint)
                {
                    Owner = this
                };
                if (dialog.ShowDialog() != true)
                {
                    KonnektorStatusText.Text = KL("Zertifikat nicht vertraut – keine Abfrage durchgeführt.", "Certificate not trusted – no query performed.");
                    return;
                }

                k.PinnedFingerprint = probe.Certificate.Sha256Fingerprint;
                SettingsStore.Save(_settings);
                _ownerMain.ApplySettings(_settings);
                UpdateKonnektorInfoTexts();
            }

            KonnektorStatusText.Text = KL("Frage Konnektor ab …", "Querying connector …");
            var snapshot = await _ownerMain.RefreshKonnektorAsync(userInitiated: true);
            KonnektorStatusText.Text = snapshot is null
                ? KL("Keine Antwort.", "No response.")
                : Summarize(snapshot);
        }
        catch (Exception ex)
        {
            App.LogUnhandled("KonnektorTest", ex);
            KonnektorStatusText.Text = KL("Test fehlgeschlagen: ", "Test failed: ") + ex.Message;
        }
        finally
        {
            KonnektorTestButton.IsEnabled = true;
        }
    }

    private static string Summarize(KonnektorSnapshot s)
    {
        if (!s.Reachable)
            return s.Error ?? KL("Nicht erreichbar.", "Unreachable.");
        var p = s.Sds?.Product;
        var lines = new List<string>
        {
            KL("✓ Verbunden: ", "✓ Connected: ") + $"{p?.VendorName ?? p?.VendorId} {p?.ProductName ?? p?.ProductCode} (FW {p?.FirmwareVersion ?? "–"})",
            $"VPN TI: {Online(s.Resource?.VpnTiOnline)} · VPN SIS: {Online(s.Resource?.VpnSisOnline)} · " +
            $"{KL("Karten", "Cards")}: {s.Cards.Count} · Terminals: {s.Terminals.Count}"
        };
        foreach (var err in new[] { s.ResourceError, s.CardsError, s.TerminalsError })
            if (err is not null) lines.Add(err);
        return string.Join("\n", lines);
    }

    private static string Online(bool? value) => value switch
    {
        true => "online",
        false => "offline",
        _ => "?"
    };

    private void KonnektorResetTrust_Click(object sender, RoutedEventArgs e)
    {
        _settings.Konnektor.PinnedFingerprint = null;
        SettingsStore.Save(_settings);
        _ownerMain.ApplySettings(_settings);
        UpdateKonnektorInfoTexts();
        KonnektorStatusText.Text = KL("Vertrauen zurückgesetzt. Beim nächsten Test wird das Zertifikat erneut angezeigt.",
            "Trust reset. The certificate will be shown again on the next test.");
    }

    private void KonnektorDelete_Click(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(this,
            KL("Konnektor-Konfiguration, Zertifikat und Passwort wirklich löschen?", "Really delete connector configuration, certificate and password?"),
            KL("Konnektor", "Connector"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        KonnektorSecretStore.CreateDefault().Delete();
        _settings.Konnektor = new KonnektorSettings();
        _pendingP12 = null;
        _pendingP12Name = null;
        KonnektorPasswordBox.Clear();
        SettingsStore.Save(_settings);
        _ownerMain.ApplySettings(_settings);
        _initializing = true;
        LoadKonnektorFields();
        _initializing = false;
        KonnektorStatusText.Text = KL("Gelöscht.", "Deleted.");
    }
}
