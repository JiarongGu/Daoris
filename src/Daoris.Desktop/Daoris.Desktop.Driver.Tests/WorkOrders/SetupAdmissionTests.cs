using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A set-up pressed where its repository is not driven is admitted alone, by its
/// quest's id in <c>driver.json</c>'s <c>admitted</c>. Its session starts and runs while every other quest of that repository,
/// queued or newly arriving, stays held as not driven, through a restart; the admission ends with the quest; and driving chosen
/// releases everything.
/// </summary>
public sealed class SetupAdmissionTests
{
    private static QuestView Quest(string id, string status = "Open", string title = "Fix the map") =>
        new(id, "ask #a1", "atlas", title, "Here is why.", status);

    private static readonly QuestView SetUp = Quest("qs", title: "Set up this repository for every agent (2026-11-01)");

    private static readonly RepoView Atlas = new("atlas", false, "D:/fam/atlas", "work");

    /// <summary>atlas not driven here, its sessions in trees of their own, its set-up quest admitted alone.</summary>
    private static readonly DriverConfig Admitted =
        (DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp"), Cap = 3 }).WithAdmitted("qs", "atlas");

    private static IReadOnlyList<Consideration> Plan(Snapshot snapshot, DriverConfig config) =>
        Planner.Plan(snapshot.Apart("atlas"), config, true);

    private static StartVerdict VerdictOf(IReadOnlyList<Consideration> plan, string quest) =>
        Assert.Single(plan, each => each.Quest.Id == quest).Verdict;

    /// <summary>🔴 The set-up quest starts by its own id; the queued quest older than it and one that arrived since stay held.</summary>
    [Fact]
    public void The_set_up_quest_admitted_alone_starts_and_nothing_else_of_its_repository_does()
    {
        var plan = Plan(new Snapshot([Quest("q1"), SetUp, Quest("q2", title: "Draw the coast")], [Atlas], []), Admitted);

        Assert.Equal(StartVerdict.Start, VerdictOf(plan, "qs"));
        Assert.Equal(StartVerdict.NotDrivable, VerdictOf(plan, "q1"));
        Assert.Equal(StartVerdict.NotDrivable, VerdictOf(plan, "q2"));
        Assert.Contains("opted into driving", Assert.Single(plan, each => each.Quest.Id == "q1").Reason);
    }

    /// <summary>
    /// 🔴 While its session runs, and after the loop restarts from the file, the rest stays held, not merely busy, and the set-up quest
    /// still goes on: a question it asked answered resumes it.
    /// </summary>
    [Fact]
    public void Through_its_session_and_a_restart_the_rest_stays_held_and_the_set_up_goes_on()
    {
        var running = new Snapshot(
            [Quest("q1"), SetUp with { Status = "Taken" }, Quest("q3", title: "Name the rivers")], [Atlas],
            [new SessionView("s1", "atlas", "working") { Quest = "qs" }]);
        var restarted = DriverConfig.Parse(Admitted.ToJson());
        var answered = new Snapshot([Quest("q1"), SetUp with { Status = "Taken", Awaits = "q9" }, Quest("q3")], [Atlas], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["qs"] = new("s1", "D:/trees/s1") },
        };

        foreach (var config in new[] { Admitted, restarted })
        {
            var plan = Plan(running, config);
            Assert.Equal([("q1", StartVerdict.NotDrivable), ("q3", StartVerdict.NotDrivable)], plan.Select(each => (each.Quest.Id, each.Verdict)));
            var resumed = Plan(answered, config);
            Assert.Equal(StartVerdict.Start, VerdictOf(resumed, "qs"));
            Assert.Equal("s1", Assert.Single(resumed, each => each.Quest.Id == "qs").Resumes?.Session);
            Assert.Equal(StartVerdict.NotDrivable, VerdictOf(resumed, "q3"));
        }
    }

    /// <summary>
    /// 🔴 The admission ends with the quest: closed, words written to its last session no longer carry it on where it is not driven,
    /// as they do where it is; and an abandon's tidy, which drops a closed quest's entries, drops it.
    /// </summary>
    [Fact]
    public void The_admission_ends_with_its_quest()
    {
        var written = new PriorSession("s1", "D:/trees/s1", "completed", "reached done.")
        {
            Said = [new SaidWordView("w1", "One more thing.", DateTimeOffset.UnixEpoch, [], false)],
        };

        foreach (var closed in new[] { "Done", "Declined" })
        {
            var ended = SetUp with { Status = closed };
            var snapshot = new Snapshot([Quest("q1")], [Atlas], [])
            {
                Closed = [ended],
                LastRun = new Dictionary<string, PriorSession> { ["qs"] = written },
            };

            Assert.False(Admitted.Admits(ended));
            Assert.Equal(StartVerdict.NotDrivable, VerdictOf(Plan(snapshot, Admitted), "qs"));
            Assert.Equal(StartVerdict.NotDrivable, VerdictOf(Plan(snapshot, Admitted), "q1"));
            Assert.True(Assert.Single(Plan(snapshot, Admitted.WithDrivable("atlas", true)), each => each.Quest.Id == "qs").GoesOn);
        }

        Assert.Empty(Admitted.WithoutQuest("qs").Admitted);
    }

    /// <summary>With general driving chosen, everything is released as today, the admission or none.</summary>
    [Fact]
    public void With_general_driving_chosen_everything_is_released_as_today()
    {
        var snapshot = new Snapshot([Quest("q1"), SetUp, Quest("q2")], [Atlas], []);

        foreach (var config in new[] { Admitted.WithDrivable("atlas", true), Admitted.WithDrivable("atlas", true) with { Admitted = new Dictionary<string, string>() } })
        {
            Assert.All(Plan(snapshot, config), each => Assert.Equal(StartVerdict.Start, each.Verdict));
        }
    }

    /// <summary>The person's hold still holds it, and a quest of another repository, or another quest, is never admitted by it.</summary>
    [Fact]
    public void An_admission_is_its_own_quest_in_its_own_repository_and_a_hold_still_holds_it()
    {
        var elsewhere = SetUp with { To = "harbour" };

        Assert.Equal(StartVerdict.Held, VerdictOf(Plan(new Snapshot([SetUp], [Atlas], []), Admitted with { Holds = ["atlas"] }), "qs"));
        Assert.False(Admitted.Admits(elsewhere));
        Assert.False(Admitted.Admits(Quest("q1")));
        Assert.True(Admitted.Admits(SetUp with { Id = "QS" }));
    }

    /// <summary>A stored shape: absent is none, a quest against its repository is one, anything else is not read; written only when set.</summary>
    [Theory]
    [InlineData("absent is no admission", "{}", false)]
    [InlineData("the quest against its repository is one", """{"admitted":{"qs":"atlas"}}""", true)]
    [InlineData("a quest is matched in any case", """{"admitted":{"QS":"atlas"}}""", true)]
    [InlineData("a repository is matched in any case and without its spaces", """{"admitted":{"qs":" Atlas "}}""", true)]
    [InlineData("another repository's is not this one's", """{"admitted":{"qs":"harbour"}}""", false)]
    [InlineData("a blank repository admits nothing", """{"admitted":{"qs":" "}}""", false)]
    [InlineData("a repository that is not text is not read", """{"admitted":{"qs":7}}""", false)]
    [InlineData("a list is not a map", """{"admitted":["qs"]}""", false)]
    public void Admitted_reads_as_its_file_holds_it(string name, string file, bool admits)
    {
        Assert.True(admits == DriverConfig.Parse(file).Admits(SetUp), name);
    }

    [Fact]
    public void An_admission_survives_the_file_and_is_written_only_when_set()
    {
        Assert.Contains("\"admitted\": {", Admitted.ToJson(), StringComparison.Ordinal);
        Assert.True(DriverConfig.Parse(Admitted.ToJson()).Admits(SetUp));
        Assert.DoesNotContain("admitted", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithAdmitted(" ", "atlas"));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithAdmitted("qs", ""));
        Assert.Equal(["#qs"], DriverConfig.Empty.WithAdmitted("#qs", "atlas").WithAdmitted("QS", "atlas").Admitted.Keys.Select(key => $"#{key}"));
    }
}
