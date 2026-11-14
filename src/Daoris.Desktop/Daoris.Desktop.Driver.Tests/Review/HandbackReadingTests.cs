using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The hand-back as the check step reads it, from git and never from the session's note: the base against the line, how far the line
/// moved past it, the paths touched, and the commits without a maker's trailer. Any of the last two held against it is a fault.
/// </summary>
public sealed class HandbackReadingTests
{
    [Fact]
    public void A_commit_without_its_makers_trailer_is_read_and_said_as_the_merge_check_refuses_it()
    {
        const string log =
            "a1\u001fp0\u001fsigned by Claude\u001fClaude Sonnet 5.5 <noreply@anthropic.com>\n\u001e\n"
            + "a2\u001fa1\u001fsigned by nobody\u001f\u001e\n"
            + "a3\u001fa2\u001fsigned by a person\u001fA Person <person@example.invalid>\n\u001e\n"
            + "a4\u001fa3 b9\u001fMerge branch 'line' into work\u001f\u001e\n";

        var seen = new HandbackReading("a4", "p0", 0, [], CommitAttribution.Unsigned(log));

        Assert.Equal(["signed by nobody", "signed by a person"], seen.Unsigned.Select(commit => commit.Subject));
        Assert.True(seen.Faulty);
        Assert.Contains("signed by nobody", seen.Words());
        Assert.Contains("Co-Authored-By", seen.Words());
        Assert.False(new HandbackReading("a1", "p0", 0, ["x"], []).Faulty);
    }

    [Fact]
    public void A_line_that_moved_past_the_work_is_a_fault_said_with_the_way_to_put_it_right()
    {
        var seen = new HandbackReading("t1", "b0", 1, ["a"], []) { Line = "main" };

        Assert.True(seen.Faulty);
        Assert.Contains("1 commit behind `main`", seen.Words());
        Assert.Contains("rebase it onto `main`", seen.Words());
    }

    [Fact]
    public void The_reading_says_its_base_and_how_many_paths_it_touched_even_where_it_holds_nothing_against_the_work()
    {
        var words = new HandbackReading("t1", "b0", 0, ["a", "b"], []).Words();

        Assert.Contains("based on b0, up to date with the line; 2 paths touched", words);
    }
}
