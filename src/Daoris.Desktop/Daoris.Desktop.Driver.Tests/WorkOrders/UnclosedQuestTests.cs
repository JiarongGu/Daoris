using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A taken quest whose last session here ended <c>completed</c> without its done, which nothing here carries on, waits on the
/// person: a park the person or the steward finished at a checkpoint, leaving the quest as it stood. Nothing resumes such a session
/// and the planner planned nothing for it, so its quest stayed taken in silence, with its tree kept for a line that never went on.
/// </summary>
public sealed class UnclosedQuestTests
{
    private static readonly DriverConfig Config = DriverConfig.Empty with { Drivable = ["Game"], Cap = 2 };

    private static QuestView Taken(string id = "q1") => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", "Taken");

    private static Snapshot Ran(QuestView quest, PriorSession last) =>
        new([quest], [new RepoView("Game", true, "D:/fam/Game")], [])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { [quest.Id] = last },
        };

    private static PriorSession Ended(Noted noted) =>
        new("s1", "D:/trees/s1", "completed", noted.Note, "Game") { NoteParts = noted.Parts };

    /// <summary>The finish as the record keeps it where the person wrote their own words: no code on the note at all.</summary>
    private static readonly PriorSession FinishedWithWords = Ended(Noted.Said("Its three fixes are merged into main.", NoteBy.Person));

    /// <summary>🔴 The take left taken by its finished session is before the person, saying the doors that move it.</summary>
    [Fact]
    public void A_take_its_finished_session_left_taken_waits_on_the_person()
    {
        var only = Assert.Single(Planner.Plan(Ran(Taken(), FinishedWithWords), Config));

        Assert.Equal(StartVerdict.Unclosed, only.Verdict);
        Assert.Equal((SittingFact.Unclosed, "s1"), (only.Said?.Code, only.Said?.Session));
        Assert.Equal(FinishedWithWords, only.HeldBy);
        Assert.Null(only.Resumes);
        Assert.Equal(
            "session `s1` ended and left `#q1` taken, and nothing here carries it on: your done closes it — `daoris quest done q1` — "
            + "or a word to `s1` goes on with it.",
            only.Reason);
    }

    /// <summary>
    /// The person's finish with no words and the steward's alike, and a go-on from either that ended with the quest as it stood: the
    /// rule reads the end, never the note's words.
    /// </summary>
    [Theory]
    [InlineData("person")]
    [InlineData("steward")]
    [InlineData("went-on")]
    public void A_finish_by_the_person_or_the_steward_leaves_it_waiting_alike(string by)
    {
        var finished = Ended(by switch
        {
            "steward" => SessionMoves.ByTheStewardNoted("completed"),
            "went-on" => Observation.WentOn(0, "completed").AsNoted(),
            _ => SessionMoves.ByThePersonNoted("completed"),
        });

        Assert.Equal(StartVerdict.Unclosed, Assert.Single(Planner.Plan(Ran(Taken(), finished), Config)).Verdict);
    }

    /// <summary>
    /// A completed end that something else carries on keeps its own reading: one that asked another repository waits on that
    /// answer, one that waits on its own helper goes on with the answer, one that said done or ready follows its quest, one submitted
    /// closes at its workflow's end, and a teammate's is theirs. None is said unclosed.
    /// </summary>
    [Fact]
    public void A_completed_end_something_else_carries_on_is_never_said_unclosed()
    {
        var asked = Planner.Plan(Ran(Taken() with { Awaits = "q9" }, FinishedWithWords), Config);
        Assert.DoesNotContain(asked, each => each.Verdict == StartVerdict.Unclosed);

        var helper = Ended(Noted.Of(EndNoteCodes.EndedAwaitsHelper, "It waits on its helper."));
        Assert.Empty(Planner.Plan(Ran(Taken(), helper), Config));
        Assert.Empty(Planner.Plan(Ran(Taken(), Ended(Noted.Of(EndNoteCodes.EndedDone, "the quest reached done."))), Config));

        var submitted = Taken() with { Closure = "workflow", Submission = new QuestSubmissionView("desk", 41, null, null) };
        Assert.Empty(Planner.Plan(Ran(submitted, FinishedWithWords), Config));

        Assert.Empty(Planner.Plan(Ran(Taken(), FinishedWithWords with { Session = "desk/s1" }), Config));
    }

    /// <summary>Nothing goes back into its tree but the person's word, so the work the session left there stays to review, as a stop's does.</summary>
    [Fact]
    public void The_work_its_session_left_in_its_tree_stays_to_review()
    {
        const string tree = "X:/daoris/trees/aurora/engine/s-1a2b3c4d";
        var quest = new QuestView("q1", "game", "engine", "The work", "A body.", "Taken");
        var record = new JsonObject
        {
            ["id"] = "s1", ["repository"] = "engine", ["adapter"] = "claude-code", ["state"] = "completed", ["kind"] = "driven",
            ["quest"] = "q1", ["tree"] = tree, ["said"] = new JsonArray(),
            ["created"] = "2026-11-08T04:54:00.0000000+00:00", ["updated"] = "2026-11-08T08:26:00.0000000+00:00",
        };
        var look = SessionLook.From(new JsonArray(record).ToJsonString(), [quest], [new(quest, StartVerdict.Unclosed, "the planner's sentence")], _ => 0) with
        {
            Trees = new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase) { [SessionGroups.Normal(tree)] = new TreeWork(2, 0) },
        };

        Assert.Equal(SessionGroup.NeedsYou, Assert.Single(SessionGroups.Read(look)).Group);
    }
}
