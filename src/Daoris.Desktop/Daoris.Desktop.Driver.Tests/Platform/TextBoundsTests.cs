using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one way the driver clips, folds and quotes text (DEDUP7). Each caller keeps its own limit; these pin the rules the
/// copies agreed on and the ones they differed in, so a caller's bytes do not move.
/// </summary>
public sealed class TextBoundsTests
{
    [Fact]
    public void ClipKeepsTextWithinTheLimitAndMarksWhatItCuts()
    {
        Assert.Equal("abcde", TextBounds.Clip("abcde", 5));
        Assert.Equal("abcde…", TextBounds.Clip("abcdef", 5));
    }

    [Fact]
    public void ClipCanKeepFewerCharactersThanItsLimitSoTheMarkFits()
    {
        Assert.Equal("abcdef", TextBounds.Clip("abcdef", 6, keep: 5));
        Assert.Equal("abcde…", TextBounds.Clip("abcdefg", 6, keep: 5));
    }

    [Fact]
    public void ClipCanTrimTheEndBeforeTheMarkAndKeepSurrogatePairsWhole()
    {
        Assert.Equal("ab…", TextBounds.Clip("ab   cd", 4, trimEnd: true));
        Assert.Equal("ab\ud83d…", TextBounds.Clip("ab😀cd", 4, keep: 3));
        Assert.Equal("ab…", TextBounds.Clip("ab😀cd", 4, keep: 3, wholePairs: true));
    }

    [Fact]
    public void ClipStartKeepsTheEndAndDropsTheSpaceAfterTheMark()
    {
        Assert.Equal("abc", TextBounds.ClipStart("abc", 3));
        Assert.Equal("…cd", TextBounds.ClipStart("ab cd", 3));
        Assert.Equal("…d", TextBounds.ClipStart("ab  d", 3));
    }

    [Fact]
    public void ClipCountedSaysHowLongTheTextWasAndKeepsNull()
    {
        Assert.Null(TextBounds.ClipCounted(null, 3));
        Assert.Equal("abc", TextBounds.ClipCounted("abc", 3));
        Assert.Equal("abc… (5 chars)", TextBounds.ClipCounted("abcde", 3));
    }

    [Fact]
    public void OneLineFoldsLineBreaksAndTrimsOrNot()
    {
        Assert.Equal("a b c", TextBounds.OneLine("  a\r\nb\nc \n"));
        Assert.Equal(" a b c ", TextBounds.OneLine(" a\nb\r\nc\n", trim: false));
    }

    [Fact]
    public void FoldTurnsEveryRunOfWhiteSpaceIntoOneSpace()
    {
        Assert.Equal("a b c", TextBounds.Fold("  a \t\n b   c "));
        Assert.Equal("", TextBounds.Fold(null));
    }

    [Fact]
    public void QuoteMarksEachLineAndLeavesABlankOneBareOrNot()
    {
        Assert.Equal("> a\n>\n> b", TextBounds.Quote("a\r\n\nb"));
        Assert.Equal("  > a\n  >\n  > b", TextBounds.Quote("a\n\nb", indent: "  "));
        Assert.Equal("> a\n> \n> b", TextBounds.Quote("a\n\nb", bareBlank: false));
        Assert.Equal("> ", TextBounds.Quote("", bareBlank: false));
    }

    [Fact]
    public void QuoteCanDropTheTrailingBlankLines()
    {
        Assert.Equal("> a\n> b", TextBounds.Quote("a\nb\n\n", trimEnd: true));
    }

    [Theory]
    [InlineData("0123456789abcdef", 7, "0123456")]
    [InlineData("0123456789abcdef", 8, "01234567")]
    [InlineData("0123456789abcdef", 12, "0123456789ab")]
    [InlineData("01234567", 8, "01234567")]
    [InlineData("abc", 8, "abc")]
    [InlineData("", 7, "")]
    public void ShortCommitKeepsTheHeadAndLeavesAShortIdAlone(string id, int length, string expected)
    {
        Assert.Equal(expected, TextBounds.ShortCommit(id, length));
    }
}
