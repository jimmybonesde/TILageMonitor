using System.Globalization;
using System.IO;
using System.Text;

namespace TILageMonitor;

/// <summary>One row of the exported incident report (local time).</summary>
public sealed record IncidentReportEntry(
    DateTime StartLocal,
    DateTime EndLocal,
    string Service,
    string Status,
    string Description);

/// <summary>
/// Builds the „Störungsbericht“ CSV: UTF-8 with BOM, semicolon separator (German Excel),
/// local times, one row per timeline event, oldest first.
/// </summary>
public static class IncidentReportExporter
{
    public const char Separator = ';';

    public static string DefaultFileName(DateTime today) =>
        $"TILage-Stoerungsbericht-{today:yyyy-MM-dd}.csv";

    public static string BuildCsv(IEnumerable<IncidentReportEntry> entries, bool german)
    {
        var sb = new StringBuilder();
        var header = german
            ? new[] { "Beginn", "Ende", "Dauer (min)", "Dienst", "Status", "Beschreibung" }
            : new[] { "Start", "End", "Duration (min)", "Service", "Status", "Description" };
        AppendLine(sb, header);

        foreach (var entry in entries.OrderBy(e => e.StartLocal).ThenBy(e => e.Service, StringComparer.Ordinal))
        {
            var minutes = Math.Max(0, (int)Math.Round((entry.EndLocal - entry.StartLocal).TotalMinutes, MidpointRounding.AwayFromZero));
            AppendLine(sb,
            [
                entry.StartLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                entry.EndLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                minutes.ToString(CultureInfo.InvariantCulture),
                entry.Service,
                entry.Status,
                entry.Description
            ]);
        }

        return sb.ToString();
    }

    /// <summary>Writes the CSV as UTF-8 with BOM so Excel detects umlauts correctly.</summary>
    public static void WriteCsv(string path, IEnumerable<IncidentReportEntry> entries, bool german) =>
        File.WriteAllText(path, BuildCsv(entries, german), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    public static string EscapeField(string? value)
    {
        value ??= string.Empty;
        // Neutralise spreadsheet formulas from external text (CSV injection).
        if (value.Length > 0 && value[0] is '=' or '+' or '@' or '\t')
            value = "'" + value;
        if (value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static void AppendLine(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(Separator);
            sb.Append(EscapeField(fields[i]));
        }
        sb.Append("\r\n");
    }
}
