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
    public void EnglishCultureTranslatesUiText()
    {
        Assert.Equal("Settings", LocalizationService.Translate("Einstellungen", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("Outage · ePA", LocalizationService.Translate("Störung · ePA", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("no data point", LocalizationService.Translate("kein Datenpunkt", CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void NonGermanCulturesUseEnglishFallback()
    {
        Assert.False(LocalizationService.IsGermanCulture(CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("↻  Refresh", LocalizationService.Translate("↻  Aktualisieren", CultureInfo.GetCultureInfo("en-GB")));
    }

    [Fact]
    public void EnglishCultureTranslatesIncidentAndApiToastPhrases()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("API reachable again", LocalizationService.Translate("API wieder erreichbar", en));
        Assert.Equal("TI status: outage", LocalizationService.Translate("TI-Status: Störung", en));
        Assert.Equal("Checking GitHub release …", LocalizationService.Translate("Prüfe GitHub-Release …", en));
        Assert.Equal("gematik reports an outage", LocalizationService.Translate("gematik meldet Ausfall", en));
        Assert.Equal("TI component", LocalizationService.Translate("TI-Komponente", en));
        Assert.Equal("3 services changed · ePA: Outage", LocalizationService.Translate("3 Dienste geändert · ePA: Störung", en));
        Assert.Equal("2 new messages · a; b", LocalizationService.Translate("2 neue Meldungen · a; b", en));
    }
}
