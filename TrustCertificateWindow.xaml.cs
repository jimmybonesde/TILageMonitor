using System.Windows;
using TILageMonitor.Konnektor;

namespace TILageMonitor;

/// <summary>Trust-on-first-use dialog for the connector TLS certificate.</summary>
public partial class TrustCertificateWindow : Window
{
    public TrustCertificateWindow(KonnektorServerCertificate certificate, bool isMismatch, string? previousFingerprint)
    {
        InitializeComponent();
        LocalizationService.Apply(this);

        FingerprintBox.Text = certificate.Sha256Fingerprint;
        SubjectText.Text = certificate.Subject;
        IssuerText.Text = certificate.Issuer;
        var culture = LocalizationService.DisplayCulture;
        ValidityText.Text = $"{certificate.NotBefore.ToString("g", culture)} – {certificate.NotAfter.ToString("g", culture)}";

        if (isMismatch)
        {
            HeadlineText.Text = KonnektorClient.L("⚠ Zertifikat des Konnektors hat sich geändert", "⚠ The connector certificate has changed");
            HeadlineText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "StatusOutage");
            IntroText.Text = KonnektorClient.L(
                $"Der Konnektor meldet ein anderes Zertifikat als das bisher vertraute ({previousFingerprint}). Das kann ein Zertifikatstausch, aber auch ein Angriff sein. Nur vertrauen, wenn der neue Fingerprint in der Konnektor-Administration bestätigt wurde.",
                $"The connector presents a different certificate than the trusted one ({previousFingerprint}). This can be a certificate renewal, but also an attack. Only trust it after confirming the new fingerprint in the connector administration.");
        }
    }

    private void Trust_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
