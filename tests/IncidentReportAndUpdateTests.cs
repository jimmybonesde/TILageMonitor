using System.Threading;
using Xunit;

namespace TILageMonitor.Tests;

public sealed class IncidentReportAndUpdateTests
{
    [Theory]
    [InlineData("KIM", "KIM")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("", "")]
    public void EscapeField_quotes_separator_quotes_and_newlines(string input, string expected) =>
        Assert.Equal(expected, IncidentReportExporter.EscapeField(input));

    [Fact]
    public void BuildCsv_german_header_rows_sorted_and_duration()
    {
        var entries = new[]
        {
            new IncidentReportEntry(new DateTime(2026, 10, 2, 8, 0, 0), new DateTime(2026, 10, 2, 9, 30, 0), "eRezept", "Störung", "Ausfall; total"),
            new IncidentReportEntry(new DateTime(2026, 10, 1, 7, 0, 0), new DateTime(2026, 10, 1, 7, 45, 0), "KIM", "Wartung", "Plan \"A\"")
        };

        var csv = IncidentReportExporter.BuildCsv(entries, german: true);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Beginn;Ende;Dauer (min);Dienst;Status;Beschreibung", lines[0]);
        Assert.Equal("2026-10-01 07:00;2026-10-01 07:45;45;KIM;Wartung;\"Plan \"\"A\"\"\"", lines[1]);
        Assert.Equal("2026-10-02 08:00;2026-10-02 09:30;90;eRezept;Störung;\"Ausfall; total\"", lines[2]);
    }

    [Fact]
    public void BuildCsv_english_header()
    {
        var csv = IncidentReportExporter.BuildCsv(Array.Empty<IncidentReportEntry>(), german: false);
        Assert.Equal("Start;End;Duration (min);Service;Status;Description\r\n", csv);
    }

    [Fact]
    public void DefaultFileName_uses_iso_date() =>
        Assert.Equal("TILage-Stoerungsbericht-2026-10-16.csv",
            IncidentReportExporter.DefaultFileName(new DateTime(2026, 10, 16)));

    [Fact]
    public void WriteCsv_emits_utf8_bom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tilage-test-{Guid.NewGuid():N}.csv");
        try
        {
            IncidentReportExporter.WriteCsv(path, Array.Empty<IncidentReportEntry>(), german: true);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HttpClient_timeout_is_classified_as_timeout()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException while the caller token is not cancelled.
        Assert.True(UpdateService.IsTimeout(new TaskCanceledException(), CancellationToken.None));
    }

    [Fact]
    public void Caller_cancellation_is_not_a_timeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.False(UpdateService.IsTimeout(new OperationCanceledException(cts.Token), cts.Token));
    }

    [Fact]
    public void Other_exceptions_are_not_timeouts() =>
        Assert.False(UpdateService.IsTimeout(new HttpRequestException("x"), CancellationToken.None));

    [Fact]
    public void Timeout_messages_are_localized_to_english()
    {
        var en = new System.Globalization.CultureInfo("en-US");
        Assert.StartsWith("The update check timed out",
            LocalizationService.TranslateMessage(UpdateService.CheckTimeoutMessage, en));
        Assert.StartsWith("Downloading the update timed out",
            LocalizationService.TranslateMessage(UpdateService.DownloadTimeoutMessage, en));
    }
}
