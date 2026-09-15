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

    [Fact]
    public void UrsacheKey_HasNoTrailingSpace_AndComposesWithSeparator()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        // Catalog key is "Ursache ·" (no trailing space); call sites append " " + focus.
        Assert.Equal("Cause ·", LocalizationService.Translate("Ursache ·", en));
        Assert.Equal("Ursache · ", LocalizationService.Translate("Ursache · ", en)); // must NOT match
        Assert.Equal("Cause · ePA", LocalizationService.Translate("Ursache ·", en) + " " + "ePA");
    }

    [Fact]
    public void TranslateAppDetail_TranslatesStatusPrefix_KeepsAffectedSuffixRaw()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("Full outage", LocalizationService.TranslateAppDetail("Komplettausfall", en));
        Assert.Equal("Partial outage", LocalizationService.TranslateAppDetail("Teilausfall", en));
        Assert.Equal(
            "Maintenance / restriction",
            LocalizationService.TranslateAppDetail("Wartung / Einschränkung", en));
        Assert.Equal("Available", LocalizationService.TranslateAppDetail("Verfügbar", en));

        // AffectedFunctions append " – …" — only the status label must translate.
        Assert.Equal(
            "Full outage – VSD: eingeschränkt",
            LocalizationService.TranslateAppDetail("Komplettausfall – VSD: eingeschränkt", en));
        Assert.Equal(
            "Partial outage – ePA: limited",
            LocalizationService.TranslateAppDetail("Teilausfall – ePA: limited", en));
    }

    [Fact]
    public void EnglishComposedFragments_MatchExactCatalogParts()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("Cause ·", LocalizationService.Translate("Ursache ·", en));
        Assert.Equal("Outage", LocalizationService.Translate("Störung", en));
        Assert.Equal("Restriction", LocalizationService.Translate("Einschränkung", en));
        Assert.Equal(
            "Automatically detected restriction · ",
            LocalizationService.Translate("Automatisch erkannte Einschränkung · ", en));
        Assert.Equal(
            "hour tiles with data",
            LocalizationService.Translate("Stundenkacheln mit Daten", en));
        Assert.Equal(
            "Compact rows per service. A service chip above focuses the daily tiles; events are available via the segment switcher.",
            LocalizationService.Translate(
                "Kompakte Zeilen je Dienst. Ein Dienst-Chip oben fokussiert die Tageskacheln; Ereignisse über den Segment-Umschalter.",
                en));
        Assert.Equal(
            "Over the last 14 days, {0} had no restriction or outage — a quiet picture.",
            LocalizationService.Translate(
                "In den letzten 14 Tagen blieb {0} ohne Einschränkung oder Störung — ein ruhiges Bild.",
                en));
        Assert.Equal(
            "14-day API history · {0} / {1} hours recorded locally",
            LocalizationService.Translate(
                "14 Tage API-Verlauf · {0} / {1} Stunden zusätzlich lokal erfasst",
                en));
        Assert.Equal(
            "Click to open the hourly view.",
            LocalizationService.Translate("Klick öffnet die Stundenansicht.", en));
        Assert.Equal(
            "Author: Randy Carter / R.C.  ·  © 2026",
            LocalizationService.Translate("Autor: Randy Carter / R.C.  ·  © 2026", en));
        Assert.Equal(
            "Notifications enabled",
            LocalizationService.Translate("Benachrichtigungen aktiviert", en));
        Assert.Equal(
            "is available. Currently installed:",
            LocalizationService.Translate("ist verfügbar. Aktuell installiert:", en));
        Assert.Equal("Update", LocalizationService.Translate("Update", en));
        Assert.Equal("services changed", LocalizationService.Translate("Dienste geändert", en));
        Assert.Equal("new messages", LocalizationService.Translate("neue Meldungen", en));
    }

    [Fact]
    public void DeadPlaceholderUpdateVersionKey_IsRemoved()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        // Callers compose from translated parts — dead placeholder must not match.
        var dead = "Version {result.LatestVersion} ist verfügbar. Aktuell installiert: {result.CurrentVersion}.";
        Assert.Equal(dead, LocalizationService.Translate(dead, en));
    }
}
