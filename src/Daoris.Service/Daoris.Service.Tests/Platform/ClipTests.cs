using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>DEDUP9c: the one way the service core clips text for a refusal or a log line.</summary>
public sealed class ClipTests
{
    [Theory]
    [InlineData(120)]
    [InlineData(200)]
    public void Text_at_or_under_the_limit_is_unchanged(int limit)
    {
        Assert.Equal("", Clipped.Clip("", limit));
        Assert.Equal(new string('a', limit), Clipped.Clip(new string('a', limit), limit));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(200)]
    public void Text_over_the_limit_is_cut_to_the_limit_then_an_ellipsis(int limit)
    {
        Assert.Equal(new string('a', limit) + "…", Clipped.Clip(new string('a', limit + 1), limit));
    }

    [Fact]
    public void The_default_limit_is_120()
    {
        Assert.Equal(new string('b', 120) + "…", Clipped.Clip(new string('b', 500)));
    }

    [Theory]
    [InlineData("0123456789abcdef", 7, "0123456")]
    [InlineData("0123456789abcdef", 8, "01234567")]
    [InlineData("01234567", 8, "01234567")]
    [InlineData("abc", 7, "abc")]
    [InlineData("", 7, "")]
    public void ShortCommit_keeps_the_head_and_leaves_a_short_id_alone(string id, int length, string expected)
    {
        Assert.Equal(expected, Clipped.ShortCommit(id, length));
    }

    [Fact]
    public void OneLine_turns_line_ends_to_spaces_and_trims()
    {
        Assert.Equal("a  b c", Clipped.OneLine("\n a\r\nb\nc \r"));
    }
}
