using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work as merged onto its line's tip, as a run's check reads it: what git's objects-only merge said (the tree it wrote, or the
/// paths it could not settle), and the words the session is told. Work that holds the line's tip says nothing.
/// </summary>
public sealed class LineMergeTests
{
    [Fact]
    public void A_clean_merge_names_the_tree_it_wrote()
    {
        Assert.Equal(("a1b2", (IReadOnlyList<string>)[]), LineMerge.Written(0, "a1b2\n")!.Value);
    }

    [Fact]
    public void A_conflict_names_each_path_once_and_no_tree()
    {
        var written = LineMerge.Written(1, "a1b2\nsrc/a.txt\nsrc/a.txt\ndocs/b.md\n\n")!.Value;

        Assert.Null(written.Tree);
        Assert.Equal(["src/a.txt", "docs/b.md"], written.Conflicts);
    }

    [Theory]
    [InlineData(128, "fatal: unknown option")]
    [InlineData(0, "")]
    [InlineData(1, "a1b2\n")]
    public void Git_that_did_not_merge_says_nothing(int code, string output)
    {
        Assert.Null(LineMerge.Written(code, output));
    }

    [Fact]
    public void Work_that_holds_the_lines_tip_has_nothing_to_say()
    {
        Assert.Null(new LineMerge("main", "c0ffee1234", "w1", "w1", []).Words());
    }

    [Fact]
    public void Work_git_could_not_merge_is_said_to_be_checked_as_it_stands()
    {
        Assert.Equal("Daoris could not merge this work onto `main`, so it is checked as it stands.", new LineMerge("main", null, "w1", "w1", []).Words());
    }

    [Fact]
    public void A_conflict_in_many_paths_names_ten_and_counts_the_rest()
    {
        var words = new LineMerge("origin/main", "c0ffee1234", "w1", null, [.. Enumerable.Range(1, 12).Select(n => $"p{n}")]).Words()!;

        Assert.Contains("at c0ffee1 conflicts in p1, p2, p3, p4, p5, p6, p7, p8, p9, p10 and 2 more: merge `origin/main` into the branch", words);
        Assert.Contains("resolve those paths", words);
        Assert.DoesNotContain("rebase", words);
    }
}
