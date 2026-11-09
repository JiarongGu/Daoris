using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A heading union kept twice in a replay is kept once, and nothing else is: only a heading both sides added since the base, held more
/// often than either side held it. The replay itself is <c>LandBacklogLandingTests</c>' (Process). Reads text only.
/// </summary>
public sealed class UnionOnceTests
{
    private const string Base = "# Archive\n\n## OLD - old\nOld.\n";

    [Fact]
    public void A_heading_both_sides_added_that_union_kept_twice_is_kept_once_its_later_copy_cut()
    {
        var ours = Base.Replace("\n## OLD", "\n## A - a\nBy the line.\n\n## OLD");
        var theirs = Base.Replace("\n## OLD", "\n## B - b\nB.\n\n## A - a\nBy the work.\n\n## OLD");
        var made = "# Archive\n\n## A - a\nBy the line.\n## B - b\nB.\n\n## A - a\nBy the work.\n\n## OLD - old\nOld.\n";

        var (text, lines) = UnionOnce.KeepOnce(made, Base, ours, theirs);

        Assert.Equal("# Archive\n\n## A - a\nBy the line.\n## B - b\nB.\n\nBy the work.\n\n## OLD - old\nOld.\n", text);
        Assert.Equal(["## A - a"], lines);
    }

    [Fact]
    public void A_line_that_is_no_heading_stands_as_made_even_twice_since_two_entries_may_each_carry_it()
    {
        var ours = Base + "\n---\n- tested.\n";
        var theirs = Base.Replace("\n## OLD", "\n---\n- tested.\n\n## OLD");
        var made = Base.Replace("\n## OLD", "\n---\n- tested.\n\n## OLD") + "\n---\n- tested.\n";

        var (text, lines) = UnionOnce.KeepOnce(made, Base, ours, theirs);

        Assert.Equal(made, text);
        Assert.Empty(lines);
    }

    [Fact]
    public void A_heading_only_one_side_added_or_one_a_side_held_twice_itself_stands_as_made()
    {
        var twice = Base + "\n## A - a\nOne.\n\n## A - a\nTwo.\n";

        Assert.Empty(UnionOnce.KeepOnce(twice, Base, twice, Base).Lines);
        Assert.Empty(UnionOnce.KeepOnce(twice, Base, twice, Base + "\n## A - a\nOne.\n").Lines);
        Assert.Equal("", UnionOnce.Words([]));
        Assert.Equal(
            " Kept once in a commit on top, where union had kept it twice: `docs/task-archive.md`: “## A - a”.",
            UnionOnce.Words([new UnionOnce.Kept("docs/task-archive.md", "## A - a\r")]));
    }
}
