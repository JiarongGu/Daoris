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

    /// <summary>
    /// FABRIC-CLOSE1h3: a part whose quest moved just before its push keeps its branch made, but its push was not tried, and the parts
    /// after it are not tried either, since their push would carry its work: the answer says so, and the press did not do what it asked.
    /// </summary>
    [Fact]
    public void A_part_whose_push_was_held_back_stops_the_parts_after_it_and_says_so()
    {
        var held = new TreeLanding(true, "put the work on `feature/x`. its quest was taken back before its plugin was told.", "feature/x")
        {
            Withheld = "its quest was taken back before its plugin was told",
        };
        var outcomes = new[]
        {
            new EntryPartLanding("s1", EntryPartOutcome.Landed, new TreeLanding(true, "put the work on `feature/x`.", "feature/x")),
            new EntryPartLanding("s2", EntryPartOutcome.Landed, held),
            new EntryPartLanding("s3", EntryPartOutcome.NotTried),
        };

        Assert.Equal("Landed 2 part(s) on `feature/x`, in the order they ran. `s2`'s push was held back: its quest moved before its plugin "
            + "was told. 1 after it not tried, since a later part may build on it.", EntryLandWords.Said(LandingForm.Branch, "feature/x", outcomes));
        Assert.False(EntryLandWords.AllLanded(outcomes));
        Assert.True(EntryLandWords.AllLanded(outcomes[..1]));
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

    /// <summary>
    /// ACCEPTALL1c (D167's amendment point 3): a part the person left out is recorded with the landing, a coded note on its conversation
    /// naming the branch its line of work landed on, so its review can say so and lead with Discard.
    /// </summary>
    [Fact]
    public void A_left_out_part_is_kept_as_a_coded_note_naming_the_entrys_branch()
    {
        var note = LandingRules.LeftOutNote("feature/x");

        Assert.Equal(SessionEventKind.Note, note.Kind);
        Assert.Equal("the person left this part out when its line of work landed on `feature/x`: its work did not land there.", note.Text);
        var part = Assert.Single(note.Parts!);
        Assert.Equal((NoteCodes.LandingLeftOut.Code, "feature/x", note.Text), (part.Code, part.Value("branch"), part.Text));
    }

    /// <summary>
    /// Its review reads the newest such note while nothing accepted the part since: a press of its own, or a landing at done, after it
    /// ends what the note said. Where no note says it, nothing is read.
    /// </summary>
    [Fact]
    public void A_parts_review_reads_it_left_out_until_it_is_accepted_after()
    {
        SessionEvent At(SessionEvent note, long seq) => note with { Seq = seq };
        var said = new SessionEvent { Kind = SessionEventKind.Note, Text = "a turn ended." };

        Assert.Equal("feature/x-2", LandingRules.LeftOutOf([At(LandingRules.LeftOutNote("feature/x"), 1), said, At(LandingRules.LeftOutNote("feature/x-2"), 3)]));
        Assert.Null(LandingRules.LeftOutOf([At(LandingRules.LeftOutNote("feature/x"), 1), At(LandingRules.Note(new TreeLanding(true, "put the work on `feature/x`.")), 2)]));
        Assert.Equal("feature/x", LandingRules.LeftOutOf([At(LandingRules.Note(new TreeLanding(true, "put the work on `feature/y`.")), 1), At(LandingRules.LeftOutNote("feature/x"), 2)]));
        Assert.Null(LandingRules.LeftOutOf([said]));
    }
}
