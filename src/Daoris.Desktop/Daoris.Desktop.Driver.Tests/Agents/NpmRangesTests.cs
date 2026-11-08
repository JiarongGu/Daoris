using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>An npm range read as npm reads it, for the installed versions a server's declared range can be answered by.</summary>
public sealed class NpmRangesTests
{
    [Theory]
    [InlineData("^1", "1.9.0", true)]
    [InlineData("^1", "2.0.0", false)]
    [InlineData("^1.2.3", "1.2.2", false)]
    [InlineData("^1.2.3", "1.9.0", true)]
    [InlineData("^0.2.3", "0.2.9", true)]
    [InlineData("^0.2.3", "0.3.0", false)]
    [InlineData("^0.0.3", "0.0.4", false)]
    [InlineData("~1.2.3", "1.2.9", true)]
    [InlineData("~1.2.3", "1.3.0", false)]
    [InlineData("~1", "1.9.0", true)]
    [InlineData("1.x", "1.4.0", true)]
    [InlineData("1.x", "2.0.0", false)]
    [InlineData("*", "7.0.0", true)]
    [InlineData(">=1.2 <2", "1.9.0", true)]
    [InlineData(">=1.2 <2", "2.0.0", false)]
    [InlineData(">1.2.3", "1.2.3", false)]
    [InlineData("<=1.2.3", "1.2.3", true)]
    [InlineData("^1 || ^3", "3.1.0", true)]
    [InlineData("^1 || ^3", "2.0.0", false)]
    [InlineData("1.2.3 - 2.0.0", "1.5.0", true)]
    [InlineData("1.2.3 - 2.0.0", "2.0.1", false)]
    public void A_range_is_satisfied_as_npm_reads_it(string range, string version, bool satisfied)
    {
        Assert.True(NpmRanges.Reads(range));
        Assert.Equal(satisfied, NpmRanges.Satisfies(range, version));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("next")]
    [InlineData("^1-beta")]
    [InlineData("^")]
    [InlineData("")]
    public void A_tag_or_a_word_this_cannot_read_is_no_range(string written)
    {
        Assert.False(NpmRanges.Reads(written));
    }
}
