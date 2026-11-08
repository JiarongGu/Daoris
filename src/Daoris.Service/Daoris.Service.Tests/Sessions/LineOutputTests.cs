using System.Globalization;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A tool call's output as the line reader shows it: the driver's own cut is read for what it says, never cut again.</summary>
public sealed class LineOutputTests
{
    private static string DriverCut(string text) => $"{text[..LineEvents.OutputLimit]}… ({text.Length} chars)";

    /// <summary>An output the driver cut carries its marker past the limit; the real length is the marker's, not the cut text's.</summary>
    [Fact]
    public void An_output_the_driver_cut_says_the_length_it_had()
    {
        var shown = LineOutput.Shown(DriverCut(new string('x', 123_456)));

        Assert.Equal($"{new string('x', LineEvents.OutputLimit)}… (123,456 characters in all)", shown);
    }

    /// <summary>A short output, or a long one no driver cut, is as it is or cut once at the limit with its own length.</summary>
    [Fact]
    public void An_output_no_driver_cut_is_cut_once_at_the_limit()
    {
        Assert.Equal("done", LineOutput.Shown("done"));
        Assert.Equal(
            $"{new string('y', LineEvents.OutputLimit)}… ({(LineEvents.OutputLimit + 50).ToString("N0", CultureInfo.InvariantCulture)} characters in all)",
            LineOutput.Shown(new string('y', LineEvents.OutputLimit + 50)));
    }

    /// <summary>A cut never leaves half of a surrogate pair, which would print as one replacement character.</summary>
    [Fact]
    public void A_cut_keeps_a_surrogate_pair_whole()
    {
        var text = new string('x', LineEvents.OutputLimit - 1) + "😀" + "tail";

        var shown = LineOutput.Shown(text);

        Assert.DoesNotContain(shown, char.IsLowSurrogate);
        Assert.StartsWith(new string('x', LineEvents.OutputLimit - 1) + "…", shown);
    }

    /// <summary>The driver's cut, which keeps a pair whole, is one character short of the limit and is still read as its cut.</summary>
    [Fact]
    public void An_output_the_driver_cut_before_a_pair_is_still_read_as_its_cut()
    {
        var text = new string('x', LineEvents.OutputLimit - 1) + "😀" + new string('z', 500);
        var cut = $"{new string('x', LineEvents.OutputLimit - 1)}… ({text.Length} chars)";

        Assert.Equal($"{new string('x', LineEvents.OutputLimit - 1)}… ({text.Length.ToString("N0", CultureInfo.InvariantCulture)} characters in all)", LineOutput.Shown(cut));
    }
}
