using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC2 (D87's 2026-10-15 amendment point 7): a session going on in an old tree starts from the line, and is told so. The words
/// (<see cref="TreeStartWords"/>) and where an instruction carries them: before the close paragraph, the driver's own words, on a
/// resume, a carry-on and a further part alike. Without them the instruction reads as it did.
/// </summary>
public sealed class StartInSyncHandedTests
{
    private const string Told = "Before this turn Daoris brought this tree up to date with `main`: the line already holds 1 of its commit(s).";

    private static readonly QuestView Question = new("q2", "console-ui", "notes-api", "What does the notes endpoint take?", "We need the contract.", "Done")
    {
        Note = "POST /notes takes { text }.",
    };

    [Fact]
    public void A_further_part_gone_into_its_sessions_tree_is_told_before_how_to_take_the_quest()
    {
        var target = TargetPromptGoldenTests.Part with { InSync = Told };

        var text = TargetPrompt.Compose(target);

        Assert.Contains($"\n{Told}\n", text);
        Assert.True(text.IndexOf(Told, StringComparison.Ordinal) < text.IndexOf("First take the quest", StringComparison.Ordinal));
        var close = Assert.Single(TargetPrompt.Composed(target).Account.Sections, section => section.Name == HandedSections.Close);
        Assert.Equal(HandedSources.DriverWords, close.Source);
        Assert.Equal(text.Length, TargetPrompt.Composed(target).Account.Sections.Sum(section => section.Chars));
    }

    [Fact]
    public void A_resume_and_a_carry_on_are_told_before_how_to_go_on()
    {
        var resumed = TargetPrompt.Compose(TargetPromptGoldenTests.Bare with { Answered = Question, InSync = Told });
        var carried = TargetPrompt.Compose(TargetPromptGoldenTests.Bare with { CutOff = "it ended before closing the quest.", InSync = Told });

        Assert.True(resumed.IndexOf(Told, StringComparison.Ordinal) is var at && at > 0 && at < resumed.IndexOf("Carry on from there", StringComparison.Ordinal));
        Assert.True(carried.IndexOf(Told, StringComparison.Ordinal) is var there && there > 0
                    && there < carried.IndexOf("Finish from there", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_it_the_instruction_reads_as_it_did()
    {
        Assert.Equal(TargetPrompt.Compose(TargetPromptGoldenTests.Part), TargetPrompt.Compose(TargetPromptGoldenTests.Part with { InSync = null }));
        Assert.DoesNotContain("Before this turn", TargetPrompt.Compose(TargetPromptGoldenTests.Part));
    }

    /// <summary>What moved: what the line held was left out, and what of the tree's own was replayed onto which commit of the line.</summary>
    [Fact]
    public void A_tree_brought_up_to_date_says_what_was_left_out_and_what_was_replayed()
    {
        Assert.Equal(
            "Before this turn Daoris brought this tree up to date with `main`: the line already holds 1 of its commit(s), which were left "
            + "out, and its other 2 commit(s) were replayed onto `origin/main`, so its branch holds only what the line lacks. Read the log "
            + "before you go on: the commits you made have new ids.",
            TreeStartWords.Moved("main", "origin/main", 1, 2));
        Assert.Contains("and the tree now stands on `main` with nothing of its own beyond it.", TreeStartWords.Moved("main", "main", 2, 0));
    }

    /// <summary>A conflict, or any reason it was left, is told with the ask to replay its own commits onto the line itself (point 5).</summary>
    [Fact]
    public void A_tree_left_as_it_was_is_asked_to_replay_its_own_commits_onto_the_line()
    {
        var said = TreeStartWords.NotMoved(
            "main", "origin/main", 1,
            "`daoris/s-1a2b` would not replay onto `main`: CONFLICT (content): Merge conflict in CHANGELOG.md. The replay was aborted, and the branch is as it was.");

        Assert.StartsWith("The line `main` already holds 1 of this tree's commit(s), and Daoris could not bring the tree up to date before this turn: ", said);
        Assert.Contains("CONFLICT (content): Merge conflict in CHANGELOG.md", said);
        Assert.Contains("the branch is as it was. Before anything else, replay your own commits onto `origin/main`, keeping only what the line lacks", said);
        Assert.Equal("Before this turn Daoris judged this tree against `main`, and the line holds none of its work.", TreeStartWords.HoldsNone("main"));
    }

    /// <summary>A detached tree's own press says what the line held apart from what became empty (LANDSYNC3).</summary>
    [Fact]
    public void A_detached_trees_replay_says_what_the_line_held_apart_from_what_became_empty()
    {
        Assert.Equal("replayed the tree onto `main`: 2 commit(s) of its own, leaving out 1 the line holds.", TreeUpdateWords.Replayed("main", 2, 2, 1));
        Assert.Equal(
            "moved the tree to `main`: it holds nothing of its own beyond work that reached the line (2 commit(s) the line holds left out).",
            TreeUpdateWords.Replayed("main", 0, 0, 2));
        Assert.Equal("replayed the tree onto `main`: 1 commit(s) of its own (1 became empty, their changes already on the line).",
            TreeUpdateWords.Replayed("main", 1, 2));
    }
}
