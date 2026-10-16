using System.Net;
using System.Text.RegularExpressions;

namespace TILageMonitor;

/// <summary>Cleans gematik message prose: HTML tags/entities, NBSP, leading/duplicate whitespace.</summary>
public static partial class MessageText
{
    [GeneratedRegex(@"<\s*(br|/p|/div|/li)\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTags();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewlines();

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var s = text.Replace("\r\n", "\n").Replace('\r', '\n');
        s = LineBreakTags().Replace(s, "\n");
        s = AnyTag().Replace(s, " ");

        // Decode entities; a second pass handles double-encoded API text (&amp;quot;).
        for (var i = 0; i < 2; i++)
        {
            var decoded = WebUtility.HtmlDecode(s);
            if (decoded == s) break;
            s = decoded;
        }

        s = s.Replace('\u00A0', ' ').Replace("\u200B", "");
        var lines = s.Split('\n').Select(line => HorizontalWhitespace().Replace(line, " ").Trim());
        s = string.Join("\n", lines);
        s = ManyNewlines().Replace(s, "\n\n");
        return s.Trim();
    }

    /// <summary>First non-empty line (used for the collapsed card preview).</summary>
    public static string FirstLine(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
    }
}
