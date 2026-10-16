using Xunit;

namespace TILageMonitor.Tests;

public sealed class MessageTextTests
{
    [Theory]
    [InlineData("Siehe &quot;Wartungen &amp; Sonstige Informationen&quot;", "Siehe \"Wartungen & Sonstige Informationen\"")]
    [InlineData("&amp;quot;doppelt&amp;quot;", "\"doppelt\"")]
    [InlineData("A&nbsp;B &lt;C&gt; &#228;&#x00FC; &auml;", "A B <C> äü ä")]
    [InlineData("   führende   und  doppelte\t Leerzeichen  ", "führende und doppelte Leerzeichen")]
    [InlineData("<p>Zeile 1</p><p>  Zeile   2</p>", "Zeile 1\nZeile 2")]
    [InlineData("a<br/>b<br>c", "a\nb\nc")]
    [InlineData("x\r\n\r\n\r\n\r\ny", "x\n\ny")]
    [InlineData(null, "")]
    public void Normalize_decodes_entities_and_whitespace(string? input, string expected) =>
        Assert.Equal(expected, MessageText.Normalize(input));

    [Fact]
    public void FirstLine_skips_empty_lines() =>
        Assert.Equal("Titel", MessageText.FirstLine("\n  \nTitel\nRest"));
}
