using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCEPTALL1 (D167's 2026-10-15 amendment points 1 to 3): an entry's Accept as decided before git is asked. Its parts land in the
/// order handed, the ledger's; a part sharing an earlier part's tree lands once, with it; only the person's choice leaves a part out,
/// a superseded one included; a merged branch's successor is named for the same first quest; a part is superseded where, replayed
/// after a later part's work, it conflicts or adds nothing. The landing itself is <c>EntryLandingTests</c>'s, with real git.
/// </summary>
public sealed class EntryLandingsTests
{
    private static readonly string Trees = Path.Combine("_fixtures", "entry", "trees", "engine");

    private static EntryLandingPart Part(string session, string tree) => new(session, Path.Combine(Trees, tree));

    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    [Fact]
    public void Parts_land_in_the_order_handed_each_tree_once()
    {
        var steps = EntryLandings.Sequence([Part("s1", "s-1"), Part("s2", "s-2"), Part("s3", "s-1"), Part("s4", "s-4")], None);

        Assert.Equal(["s1", "s2", "s3", "s4"], steps.Select(step => step.Session));
        Assert.Equal([true, true, false, true], steps.Select(step => step.Lands));
        Assert.Equal("s1", steps[2].SameTreeAs);
        Assert.All(steps, step => Assert.False(step.LeftOut));
    }

    [Fact]
    public void Only_the_persons_choice_leaves_a_part_out()
    {
        var steps = EntryLandings.Sequence([Part("s1", "s-1"), Part("s2", "s-2"), Part("s3", "s-3")], new HashSet<string>(["s1"], StringComparer.Ordinal));

        Assert.Equal([false, true, true], steps.Select(step => step.Lands));
        Assert.True(steps[0].LeftOut);
        Assert.Null(steps[0].SameTreeAs);
    }

    /// <summary>A part whose tree is an earlier part's lands with it, whatever was chosen of it: it has nothing of its own to leave out.</summary>
    [Fact]
    public void A_part_sharing_a_tree_is_not_left_out_on_its_own()
    {
        var steps = EntryLandings.Sequence([Part("s1", "s-1"), Part("s2", "s-1")], new HashSet<string>(["s2"], StringComparer.Ordinal));

        Assert.Equal(("s1", false), (steps[1].SameTreeAs, steps[1].LeftOut));
    }

    [Fact]
    public void A_merged_branchs_successor_keeps_the_name_and_counts_on() =>
        Assert.Equal(
            ["feature/0fda18-fix-the-gap", "feature/0fda18-fix-the-gap-2", "feature/0fda18-fix-the-gap-3"],
            Enumerable.Range(1, 3).Select(n => EntryLandings.Candidate("feature/0fda18-fix-the-gap", n)));

    [Fact]
    public void A_part_that_conflicts_after_a_later_part_is_superseded_by_it()
    {
        var superseded = EntryLandings.Verdict("s2", "laterTree", (null, "CONFLICT (content): Merge conflict in app.cs."), ["app.cs"]);

        Assert.Equal(new PartSuperseded("s2", "CONFLICT (content): Merge conflict in app.cs.", Conflicts: true), superseded);
    }

    [Fact]
    public void A_part_that_adds_nothing_after_a_later_part_is_superseded_and_names_its_files()
    {
        var superseded = EntryLandings.Verdict("s2", "laterTree", ("LATERTREE", null), ["a.cs", "b.cs", "c.cs", "d.cs"]);

        Assert.Equal(new PartSuperseded("s2", "a.cs, b.cs, c.cs and 1 more", Conflicts: false), superseded);
    }

    [Fact]
    public void A_part_that_adds_something_or_a_trial_git_could_not_make_is_not_superseded()
    {
        Assert.Null(EntryLandings.Verdict("s2", "laterTree", ("anotherTree", null), ["a.cs"]));
        Assert.Null(EntryLandings.Verdict("s2", "laterTree", (null, null), ["a.cs"]));
        Assert.Null(EntryLandings.Verdict("s2", null, (null, "CONFLICT (content): Merge conflict in a.cs."), ["a.cs"]));
    }

    [Fact]
    public void The_answer_says_what_landed_where_what_was_left_out_and_what_was_not_tried()
    {
        var refused = new TreeLanding(false, "Nothing was landed: it conflicts.");
        var said = EntryLandWords.Said(LandingForm.Branch, "feature/x", [
            new EntryPartLanding("s1", EntryPartOutcome.LeftOut),
            new EntryPartLanding("s2", EntryPartOutcome.Landed, new TreeLanding(true, "put the work on `feature/x`.", "feature/x")),
            new EntryPartLanding("s3", EntryPartOutcome.Refused, refused),
            new EntryPartLanding("s4", EntryPartOutcome.NotTried),
        ]);

        Assert.Equal("Landed 1 part(s) on `feature/x`, in the order they ran. Left out 1, as you chose. `s3` was refused: Nothing was landed: "
            + "it conflicts. 1 after it not tried, since a later part may build on it.", said);
    }

    [Fact]
    public void A_merge_rule_says_the_line_the_parts_merged_into() =>
        Assert.StartsWith("Merged 2 part(s) into `main`",
            EntryLandWords.Said(LandingForm.Merge, "main", [
                new EntryPartLanding("s1", EntryPartOutcome.Landed, new TreeLanding(true, "merged.")),
                new EntryPartLanding("s2", EntryPartOutcome.Landed, new TreeLanding(true, "merged.")),
            ]));

    /// <summary>A conflict with the entry's earlier work offers the later part's session the words to rebase onto the entry's branch.</summary>
    [Fact]
    public void A_conflict_with_the_entrys_earlier_work_is_refused_with_rebase_words_for_its_branch()
    {
        var refused = EntryLandWords.Conflicted("feature/x", "CONFLICT (content): Merge conflict in app.cs.");

        Assert.False(refused.Landed);
        Assert.Equal(AutoLandingCode.Refused, refused.Refusal);
        Assert.Contains("onto `feature/x`, its entry's branch, conflicts with the entry's earlier work there", refused.Message);
        Assert.StartsWith("Rebase your work onto `feature/x`: its entry's earlier work landed there", refused.Conflict!.Words);
        Assert.Equal("`s2`'s work conflicts with what `s1` landed before it. ", EntryLandWords.After("s2", ["s1"]));
    }
}
