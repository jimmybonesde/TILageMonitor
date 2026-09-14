using System.Globalization;
using Xunit;

namespace TILageMonitor.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void GermanCultureKeepsGermanText()
    {
        Assert.Equal("Einstellungen", LocalizationService.Translate("Einstellungen", CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public void EnglishCultureTranslatesExactUiKeys()
    {
        Assert.Equal("Settings", LocalizationService.Translate("Einstellungen", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("Outage", LocalizationService.Translate("Störung", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("no data point", LocalizationService.Translate("kein Datenpunkt", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("Services", LocalizationService.Translate("Dienste", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("of", LocalizationService.Translate("von", CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void NonGermanCulturesUseEnglishFallback()
    {
        Assert.False(LocalizationService.IsGermanCulture(CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("↻  Refresh", LocalizationService.Translate("↻  Aktualisieren", CultureInfo.GetCultureInfo("en-GB")));
    }

    [Fact]
    public void EnglishCultureTranslatesExactIncidentAndToastPhrases()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("API reachable again", LocalizationService.Translate("API wieder erreichbar", en));
        Assert.Equal("TI status: outage", LocalizationService.Translate("TI-Status: Störung", en));
        Assert.Equal("Checking GitHub release …", LocalizationService.Translate("Prüfe GitHub-Release …", en));
        Assert.Equal("gematik reports an outage", LocalizationService.Translate("gematik meldet Ausfall", en));
        Assert.Equal("TI component", LocalizationService.Translate("TI-Komponente", en));
    }

    [Fact]
    public void EnglishExactMatchOnly_DoesNotCorruptApiLikeSubstrings()
    {
        var en = CultureInfo.GetCultureInfo("en-US");

        // Substring corruption must not return: davon→daof, Diensten→Servicesn, etc.
        Assert.Equal("davon", LocalizationService.Translate("davon", en));
        Assert.Equal("Diensten", LocalizationService.Translate("Diensten", en));
        Assert.Equal(
            "Auswirkung auf davon betroffene Dienste und Funktionen",
            LocalizationService.Translate("Auswirkung auf davon betroffene Dienste und Funktionen", en));
        Assert.Equal(
            "Störung · ePA",
            LocalizationService.Translate("Störung · ePA", en));
        Assert.Equal(
            "3 Dienste geändert · ePA: Störung",
            LocalizationService.Translate("3 Dienste geändert · ePA: Störung", en));

        // Exact keys still translate.
        Assert.Equal("Outage", LocalizationService.Translate("Störung", en));
        Assert.Equal("Services", LocalizationService.Translate("Dienste", en));
        Assert.Equal("of", LocalizationService.Translate("von", en));
        Assert.Equal("services changed", LocalizationService.Translate("Dienste geändert", en));
    }

    [Fact]
    public void TranslateMessage_TranslatesKnownUpdatePrefixOnly()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal(
            "Update check failed: boom",
            LocalizationService.TranslateMessage("Update-Prüfung fehlgeschlagen: boom", en));
        Assert.Equal(
            "davon bleibt unverändert",
            LocalizationService.TranslateMessage("davon bleibt unverändert", en));
    }
}
