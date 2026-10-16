using System.Globalization;
using System.Windows;
using TILageMonitor.Konnektor;

namespace TILageMonitor;

public sealed record KonnektorRow(string Title, string Detail, string BrushKey)
{
    public System.Windows.Media.Brush Brush =>
        System.Windows.Application.Current?.TryFindResource(BrushKey) as System.Windows.Media.Brush
        ?? System.Windows.Media.Brushes.Gray;
}

/// <summary>Read-only detail view: status, cards (+PIN), terminals, product info, SDS services.</summary>
public partial class KonnektorWindow : Window
{
    private readonly MainWindow _owner;

    public KonnektorWindow(MainWindow owner)
    {
        InitializeComponent();
        LocalizationService.Apply(this);
        _owner = owner;
        Owner = null;
    }

    private static string L(string de, string en) => KonnektorClient.L(de, en);

    public void Render(KonnektorSnapshot? s)
    {
        var culture = LocalizationService.DisplayCulture;
        if (s is null)
        {
            SummaryText.Text = L("Noch keine Abfrage. Konnektor in den Einstellungen konfigurieren.", "No query yet. Configure the connector in Settings.");
            StatusList.ItemsSource = CardsList.ItemsSource = TerminalsList.ItemsSource = ServicesList.ItemsSource = null;
            ProductText.Text = "";
            return;
        }

        SummaryText.Text = $"{KonnektorPresentation.HealthText(s.Health)} · {L("Stand", "As of")} {s.CheckedAt.ToString("g", culture)}";

        var status = new List<KonnektorRow>
        {
            new(L("Erreichbarkeit", "Reachability"),
                s.Reachable ? L("connector.sds abrufbar", "connector.sds reachable") : s.Error ?? "",
                s.Reachable ? "StatusOk" : "StatusOutage"),
            KonnektorPresentation.VpnRow("VPN TI", s.Resource?.VpnTiOnline, s.Resource?.VpnTiTimestamp, s.ResourceError),
            KonnektorPresentation.VpnRow("VPN SIS", s.Resource?.VpnSisOnline, s.Resource?.VpnSisTimestamp, s.ResourceError)
        };
        if (s.ServerCertificate is not null)
            status.Add(new(L("TLS-Zertifikat", "TLS certificate"),
                $"SHA-256 {s.ServerCertificate.Sha256Fingerprint}\n{s.ServerCertificate.Subject} · {L("gültig bis", "valid until")} {s.ServerCertificate.NotAfter.ToString("d", culture)}",
                s.PinMismatch ? "StatusOutage" : "StatusOk"));
        foreach (var err in s.Resource?.ActiveErrors ?? Enumerable.Empty<KonnektorErrorState>())
            status.Add(new(L("Betriebszustand: ", "Operating state: ") + err.Condition,
                $"{err.Severity} · {err.Type} · {err.ValidFrom}",
                string.Equals(err.Severity, "Fatal", StringComparison.OrdinalIgnoreCase) ? "StatusOutage" : "StatusPartial"));
        foreach (var w in s.Warnings)
            status.Add(new(L("Hinweis", "Notice"), w, "StatusPartial"));
        StatusList.ItemsSource = status;

        var cards = s.Cards.Select(c =>
        {
            var pin = c.PinType is null
                ? ""
                : $"\n{c.PinType}: {c.PinStatus ?? c.PinError ?? "–"}" + (c.PinLeftTries is int t ? $" ({L("Restversuche", "tries left")} {t})" : "");
            var expiring = KonnektorAnalysis.IsExpiringSoon(c.CertificateExpirationDate, DateTime.Now, out _);
            var problem = c.PinStatus is not null && KonnektorAnalysis.IsPinProblem(c.PinStatus, c.CardType);
            return new KonnektorRow(
                $"{c.CardType} · {c.CardHolderName ?? c.Kvnr ?? "–"}",
                $"ICCSN {c.Iccsn ?? "–"} · CtId {c.CtId ?? "–"} · Slot {c.SlotId ?? "–"}\n" +
                $"{L("Gesteckt seit", "Inserted")} {c.InsertTime?.ToString("g", culture) ?? "–"} · " +
                $"{L("Zertifikat bis", "Certificate until")} {c.CertificateExpirationDate?.ToString("d", culture) ?? "–"}" + pin,
                problem || expiring ? "StatusPartial" : "StatusOk");
        }).ToList();
        if (s.CardsError is not null)
            cards.Insert(0, new(L("Karten konnten nicht gelesen werden", "Cards could not be read"), s.CardsError, "StatusOutage"));
        else if (cards.Count == 0 && s.Reachable)
            cards.Add(new(L("Keine Karten gesteckt", "No cards inserted"), "", "StatusEmpty"));
        CardsList.ItemsSource = cards;

        var terminals = s.Terminals.Select(t => new KonnektorRow(
            $"{t.Name ?? t.CtId} ({t.CtId})",
            $"IP {t.IpAddress ?? "–"} · {L("Slots", "Slots")} {t.Slots?.ToString(CultureInfo.InvariantCulture) ?? "–"} · " +
            (t.Connected == true ? L("verbunden", "connected") : t.Connected == false ? L("nicht verbunden", "not connected") : "?"),
            t.Connected == false ? "StatusOutage" : "StatusOk")).ToList();
        if (s.TerminalsError is not null)
            terminals.Insert(0, new(L("Terminals konnten nicht gelesen werden", "Terminals could not be read"), s.TerminalsError, "StatusOutage"));
        TerminalsList.ItemsSource = terminals;

        var p = s.Sds?.Product;
        ProductText.Text = p is null
            ? "–"
            : $"{p.VendorName ?? p.VendorId} {p.ProductName ?? p.ProductCode}\n" +
              $"{L("Produkttyp", "Product type")}: {p.ProductType} {p.ProductTypeVersion}\n" +
              $"{L("Firmware", "Firmware")}: {p.FirmwareVersion ?? "–"} · {L("Hardware", "Hardware")}: {p.HardwareVersion ?? "–"}";

        ServicesList.ItemsSource = (s.Sds?.Services ?? new List<KonnektorService>())
            .Select(svc => new KonnektorRow(
                svc.Name,
                string.Join("\n", svc.Versions.Select(v => $"v{v.Version} · {v.PreferredEndpoint ?? "–"}")),
                "StatusEmpty"))
            .ToList();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshKonnektorButton.IsEnabled = false;
        try
        {
            await _owner.RefreshKonnektorAsync(userInitiated: true);
        }
        catch (Exception ex)
        {
            App.LogUnhandled("KonnektorWindowRefresh", ex);
        }
        finally
        {
            RefreshKonnektorButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>Shared texts for main card + detail window.</summary>
public static class KonnektorPresentation
{
    private static string L(string de, string en) => KonnektorClient.L(de, en);

    public static string HealthText(KonnektorHealth health) => health switch
    {
        KonnektorHealth.Ok => L("Konnektor in Ordnung", "Connector OK"),
        KonnektorHealth.Warning => L("Konnektor mit Hinweisen", "Connector with warnings"),
        KonnektorHealth.Down => L("Lokal gestört (Konnektor)", "Local fault (connector)"),
        _ => L("Konnektor nicht eingerichtet", "Connector not configured")
    };

    public static KonnektorRow VpnRow(string name, bool? online, string? timestamp, string? error) => online switch
    {
        true => new(name, L("online", "online") + (timestamp is null ? "" : $" · {timestamp}"), "StatusOk"),
        false => new(name, L("offline", "offline") + (timestamp is null ? "" : $" · {timestamp}"), "StatusOutage"),
        _ => new(name, error ?? L("unbekannt", "unknown"), "StatusEmpty")
    };

    /// <summary>
    /// Classifies where a problem lies: gematik central TI vs. local connector.
    /// gematikKind: "ok" | "partial" | "maintenance" | other (outage).
    /// </summary>
    public static string Classification(string? gematikKind, KonnektorHealth health)
    {
        var tiDisturbed = gematikKind is not null && gematikKind != "ok";
        var localDown = health == KonnektorHealth.Down;
        return (tiDisturbed, localDown) switch
        {
            (true, true) => L("TI gestört (gematik) und lokal gestört (Konnektor)", "TI disrupted (gematik) and local fault (connector)"),
            (true, false) => L("TI gestört (gematik) – lokaler Konnektor in Ordnung", "TI disrupted (gematik) – local connector OK"),
            (false, true) => L("Lokal gestört (Konnektor) – gematik meldet keine Störung", "Local fault (connector) – gematik reports no disruption"),
            _ => health == KonnektorHealth.Warning
                ? L("Konnektor mit Hinweisen – gematik ohne Störung", "Connector with warnings – gematik without disruption")
                : L("TI und Konnektor in Ordnung", "TI and connector OK")
        };
    }
}
