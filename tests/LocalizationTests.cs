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
    }

    [Fact]
    public void NonGermanCulturesUseEnglishFallback()
    {
        Assert.False(LocalizationService.IsGermanCulture(CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("Refresh", LocalizationService.Translate("↻  Aktualisieren", CultureInfo.GetCultureInfo("en-GB")));
    }
}
