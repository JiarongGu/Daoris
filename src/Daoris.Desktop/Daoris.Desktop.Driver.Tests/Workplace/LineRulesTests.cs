using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1d (D215 point 6, the integration design §4.1's <c>merge</c>): a declared line rule's merge by the base. Where both sides
/// changed only lines its pattern matches, the line's side is taken there for the rule's writer to recompute, and every other change
/// of either side is kept as the merge by the base keeps it; where both changed a line the pattern does not match, it settles nothing.
/// The landing's real-git rows are <c>LandLineRuleLandingTests</c>'.
/// </summary>
public sealed class LineRulesTests
{
    private const string Base = "# Status\n\nStatus: a\n\nNotes.\n";

    [Fact]
    public void Both_sides_changing_only_the_declared_line_take_the_lines_side_there_and_keep_every_other_change()
    {
        var merged = LineRules.ByLine(Base, "# Status\n\nStatus: a, line\n\nNotes.\n", "# Status\n\nStatus: a, work\n\nNotes.\nThe work's note.\n", LineRules.Pattern("^Status: ")!);

        Assert.Equal("# Status\n\nStatus: a, line\n\nNotes.\nThe work's note.\n", merged);
    }

    [Fact]
    public void Changes_that_do_not_meet_are_kept_from_both_sides_as_the_merge_by_the_base_keeps_them()
    {
        var ours = "# Status of the line\n\nStatus: a\n\nNotes.\n";
        var theirs = "# Status\n\nStatus: a\n\nNotes.\nThe work's note.\n";

        Assert.Equal(BacklogMerge.ByBase(Base, ours, theirs).Text, LineRules.ByLine(Base, ours, theirs, LineRules.Pattern("^Status: ")!));
    }

    [Theory]
    [InlineData("# Status\n\nStatus: a, line\n\nNotes of the line.\n", "# Status\n\nStatus: a, work\n\nNotes of the work.\n")]
    [InlineData("# Status\n\nStatus: a, line\nA line's line.\n\nNotes.\n", "# Status\n\nA work's line.\nStatus: a, work\n\nNotes.\n")]
    public void Both_sides_changing_a_line_the_pattern_does_not_match_settle_nothing(string ours, string theirs) =>
        Assert.Null(LineRules.ByLine(Base, ours, theirs, LineRules.Pattern("^Status: ")!));

    [Theory]
    [InlineData("^Status: ", true)]
    [InlineData("(unclosed", false)]
    [InlineData("", false)]
    public void A_pattern_that_does_not_read_as_one_settles_nothing(string pattern, bool reads) =>
        Assert.Equal(reads, LineRules.Pattern(pattern) is not null);
}
