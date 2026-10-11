using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>pause</c> proposal (PAUSE1f-b, D132 §7.4, design §2): an ask's work or one quest's paused on this machine,
/// or resumed, judged by what a pause would do with each piece of it (<see cref="WorkPausing.Plan"/>) and the pauses
/// <c>driver.json</c> keeps, and applied through the pause and the resume the pages press. Nothing is paused until the
/// person's press (D89), and abandon is never proposed: its reason is the person's answer (D37).
/// </summary>
public sealed class HelpPauseProposalsTests : HelpProposalsFixture
{
    private const string AskId = "a1paus00";

    private static readonly DateTimeOffset At = new(2026, 10, 2, 14, 2, 11, TimeSpan.Zero);

    private static HelpProposal Pause(string door, string? target) => Of("pause", door, target);

    private static QuestView Quest(string id, string status) =>
        new(id, AskWork.SenderOf(AskId), "engine", "a quest", "", status) { Workspace = "work" };

    private static readonly QuestView Open = Quest("q1open00", "Open");
    private static readonly QuestView Taken = Quest("q2taken0", "Taken");
    private static readonly QuestView Waiting = Quest("q3parked", "Taken");
    private static readonly QuestView Elsewhere = Quest("q4else00", "Taken");
    private static readonly QuestView Done = Quest("q5done00", "Done");

    private static readonly SessionRecord Live = new("s3live00", "engine", "working") { Quest = "q2taken0" };
    private static readonly SessionRecord Parked = new("s4parked", "engine", "awaiting-person") { Quest = "q3parked" };
    private static readonly SessionRecord Teammate = new("north/s7team0", "engine", "working") { Quest = "q4else00" };
    private static readonly SessionRecord Intake = new("i1intake", $"ask #{AskId}", "working") { Ask = AskId };
    private static readonly SessionRecord Ended = new("s1ended0", "engine", "completed") { Quest = "q5done00" };

    private static readonly IReadOnlyDictionary<string, PausedBy> NonePaused = new Dictionary<string, PausedBy>();

    /// <summary>The ask's work as <see cref="WorkPausing.PlanAsync"/> reads it: every piece §2.1 names, once.</summary>
    private static WorkPlan AskPlan(IReadOnlyDictionary<string, PausedBy>? paused = null, params SessionRecord[] more) => WorkPausing.Plan(
        new WorkPieces(
            WorkScope.Ask, AskId,
            [.. new[] { Open, Taken, Waiting, Elsewhere, Done }.Select(quest => new WorkQuest(quest, WorkJoin.Asked))],
            [new WorkSession(Live), new WorkSession(Parked), new WorkSession(Teammate), new WorkSession(Intake) { Intake = true },
                new WorkSession(Ended), .. more.Select(record => new WorkSession(record))],
            [], []),
        DriverConfig.Empty, paused ?? NonePaused, new AskView(AskId, "work", "fix the stall", "Published", "full"));

    private static WorkPlan QuestPlan(QuestView quest, IReadOnlyDictionary<string, PausedBy>? paused = null, params SessionRecord[] sessions) =>
        WorkPausing.Plan(
            new WorkPieces(WorkScope.Quest, quest.Id, [new WorkQuest(quest, WorkJoin.Named)], [.. sessions.Select(record => new WorkSession(record))], [], []),
            DriverConfig.Empty, paused ?? NonePaused, null);

    /// <summary>This machine's works as the modules read them for a pause proposal, and the workspaces it syncs with a remote.</summary>
    private static readonly HelpMachineFacts Works = Facts with
    {
        Work = [AskPlan(), QuestPlan(Taken, sessions: Live), QuestPlan(Open), QuestPlan(Done, sessions: Ended)],
        Wired = ["work"],
    };

    private static DriverConfig PausedAsk(IReadOnlyDictionary<string, string>? stopped = null) =>
        DriverConfig.Empty.WithPausedAsk(AskId, new WorkPause(At, stopped ?? new Dictionary<string, string>()));

    /// <summary>
    /// §2.6: the card says what the pause's own ask says, then where they apply the two sentences it adds, then what §2.1
    /// leaves as it is: a session waiting on the person, a teammate's, and a take no session here worked.
    /// </summary>
    [Fact]
    public void Pausing_an_ask_says_what_it_stops_what_it_adds_and_what_it_leaves()
    {
        var plan = HelpProposals.Plan(Pause("pause", AskId), DriverConfig.Empty, Works);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Stops 1 running session now and starts nothing of ask `#a1paus00` on this machine until you resume it. Its tree "
            + "keeps what it wrote, and a quest it took stays taken here. Another machine may still take `#q1open00`: a pause "
            + "holds this machine only. Its intake keeps reading; what it publishes waits too. `s4parked` is left waiting on "
            + "you: your answer is kept, and what it starts waits for Resume. `north/s7team0` runs on `north`, a teammate's "
            + "machine, and nothing this machine sends reaches it. `#q4else00` is taken with no session of this machine's on "
            + "it, so a pause here does not reach it.",
            plan.Describe);
        Assert.Equal("daoris-driver ask --pause a1paus00", plan.Terminal);
        Assert.Equal(new PausedBy(WorkScope.Ask, AskId), plan.Pause);
    }

    /// <summary>§5.1: another machine may take an open quest only where this machine syncs its workspace with a remote.</summary>
    [Fact]
    public void Another_machine_is_named_only_where_the_quests_workspace_syncs_with_a_remote()
    {
        var plan = HelpProposals.Plan(Pause("pause", AskId), DriverConfig.Empty, Works with { Wired = [] });

        Assert.Null(plan.Refusal);
        Assert.DoesNotContain("Another machine", plan.Describe);
    }

    [Fact]
    public void Several_sessions_stopped_are_said_as_theirs()
    {
        var two = new SessionRecord("s5live00", "game", "starting") { Quest = "q2taken0" };
        var plan = HelpProposals.Plan(Pause("pause", AskId), DriverConfig.Empty, Works with { Work = [AskPlan(null, two)], Wired = [] });

        Assert.StartsWith(
            "Stops 2 running sessions now and starts nothing of ask `#a1paus00` on this machine until you resume it. Their trees "
            + "keep what they wrote, and the quests they took stay taken here. Its intake keeps reading;",
            plan.Describe);
    }

    /// <summary>A quest's work: by the quest's id, its door the terminal's <c>quest pause</c>, and no intake to name.</summary>
    [Fact]
    public void Pausing_one_quest_takes_its_own_door()
    {
        var plan = HelpProposals.Plan(Pause("pause", "q2taken0"), DriverConfig.Empty, Works);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Stops 1 running session now and starts nothing of quest `#q2taken0` on this machine until you resume it. Its tree "
            + "keeps what it wrote, and a quest it took stays taken here.",
            plan.Describe);
        Assert.Equal("daoris-driver quest pause q2taken0", plan.Terminal);
        Assert.Equal(new PausedBy(WorkScope.Quest, "q2taken0"), plan.Pause);
    }

    /// <summary>§2.6: a pause that stops nothing loses nothing, and the card says so.</summary>
    [Fact]
    public void A_pause_that_stops_nothing_says_nothing_runs()
    {
        var plan = HelpProposals.Plan(Pause("pause", "#Q1OPEN00"), DriverConfig.Empty, Works);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Starts nothing of quest `#q1open00` on this machine until you resume it; nothing of it runs now, so nothing is "
            + "stopped. Another machine may still take `#q1open00`: a pause holds this machine only.",
            plan.Describe);
        Assert.Equal("daoris-driver quest pause q1open00", plan.Terminal);
        Assert.Equal(new PausedBy(WorkScope.Quest, "q1open00"), plan.Pause);
    }

    /// <summary>
    /// D48 §6: a pause of what is paused already is information, never a refusal; where something of it started since, Apply
    /// stops that as the pause's own, as <see cref="WorkPausing.PauseAsync"/> does.
    /// </summary>
    [Fact]
    public void A_pause_of_what_is_paused_already_is_said_and_stops_only_what_runs_now()
    {
        var idle = Works with { Work = [WorkPausing.Plan(AskPlan().Pieces with { Sessions = [] }, DriverConfig.Empty, NonePaused, null)] };

        var nothing = HelpProposals.Plan(Pause("pause", AskId), PausedAsk(), idle);
        var running = HelpProposals.Plan(Pause("pause", AskId), PausedAsk(), Works with { Wired = [] });

        Assert.Null(nothing.Refusal);
        Assert.Equal(
            "Ask `#a1paus00` is paused already, so applying changes nothing: nothing of it starts on this machine until you "
            + "resume it.",
            nothing.Describe);
        Assert.Null(nothing.Pause);
        Assert.Null(running.Refusal);
        Assert.StartsWith(
            "Ask `#a1paus00` is paused already, and 1 session of it runs now: applying stops it. Its tree keeps what it wrote, "
            + "and a quest it took stays taken here.",
            running.Describe);
        Assert.Equal(new PausedBy(WorkScope.Ask, AskId), running.Pause);
    }

    [Fact]
    public void A_pause_of_work_with_nothing_open_or_taken_is_said_and_changes_nothing()
    {
        var plan = HelpProposals.Plan(Pause("pause", "q5done00"), DriverConfig.Empty, Works);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Nothing of quest `#q5done00` is open or taken on this machine, so there is nothing to pause: applying changes nothing.",
            plan.Describe);
        Assert.Null(plan.Pause);
    }

    /// <summary>§2.4: Resume lifts the pause and releases each stop it made; it starts nothing itself.</summary>
    [Fact]
    public void Resuming_a_paused_ask_releases_its_stops()
    {
        var config = PausedAsk(new Dictionary<string, string> { ["q2taken0"] = "s3live00" });

        var plan = HelpProposals.Plan(Pause("resume", AskId), config, Works);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Resume ask `#a1paus00` on this machine: its pause is lifted, and the driver's next look plans its work as it would "
            + "have, a taken quest carried on in its tree. It releases the pause's stop of `s3live00` on `#q2taken0`.",
            plan.Describe);
        Assert.Equal("daoris-driver ask --resume a1paus00", plan.Terminal);
        Assert.Equal(new PausedBy(WorkScope.Ask, AskId), plan.Pause);
    }

    /// <summary>§2.4: another pause over a quest of the work is not lifted by this one, and the card names it by what holds it.</summary>
    [Fact]
    public void A_resume_names_the_pauses_it_does_not_lift()
    {
        var none = new Dictionary<string, string>();
        var facts = Works with
        {
            Work =
            [
                AskPlan(new Dictionary<string, PausedBy> { ["q1open00"] = new(WorkScope.Quest, "q1open00") }),
                QuestPlan(Taken, new Dictionary<string, PausedBy> { ["q2taken0"] = new(WorkScope.Quest, "q2taken0") }, Live),
            ],
        };

        var ask = HelpProposals.Plan(Pause("resume", AskId), PausedAsk().WithPausedQuest("q1open00", new WorkPause(At, none)), facts);
        var quest = HelpProposals.Plan(Pause("resume", "q2taken0"), PausedAsk().WithPausedQuest("q2taken0", new WorkPause(At, none)), facts);

        Assert.EndsWith(
            "`#q1open00` stays paused on its own: `daoris-driver quest resume q1open00` resumes it.",
            ask.Describe);
        Assert.Equal(
            "Resume quest `#q2taken0` on this machine: its pause is lifted, and the driver's next look plans its work as it would "
            + "have, a taken quest carried on in its tree. `#q2taken0` stays paused with ask `#a1paus00`: "
            + "`daoris-driver ask --resume a1paus00` resumes it.",
            quest.Describe);
    }

    /// <summary>D48 §6: a resume of what is not paused is information, never a refusal.</summary>
    [Fact]
    public void A_resume_of_what_is_not_paused_is_said_and_changes_nothing()
    {
        var plan = HelpProposals.Plan(Pause("resume", "q2taken0"), DriverConfig.Empty, Works);

        Assert.Null(plan.Refusal);
        Assert.Equal("Quest `#q2taken0` is not paused on this machine, so applying changes nothing.", plan.Describe);
        Assert.Null(plan.Pause);
    }

    /// <summary>The pause is this machine's, so one of an ask since deleted still resumes, as the terminal's resume does.</summary>
    [Fact]
    public void A_pause_of_an_ask_since_deleted_still_resumes()
    {
        var config = DriverConfig.Empty.WithPausedAsk("a9gone00", new WorkPause(At, new Dictionary<string, string>()));

        var resume = HelpProposals.Plan(Pause("resume", "a9gone00"), config, Works);
        var pause = HelpProposals.Plan(Pause("pause", "a9gone00"), config, Works);

        Assert.Null(resume.Refusal);
        Assert.StartsWith("Resume ask `#a9gone00` on this machine: its pause is lifted", resume.Describe);
        Assert.Equal(new PausedBy(WorkScope.Ask, "a9gone00"), resume.Pause);
        Assert.Equal("there is no ask or quest `#a9gone00` on this machine.", pause.Refusal);
    }

    [Theory]
    [InlineData("pause", "zz000000", "there is no ask or quest `#zz000000` on this machine.")]
    [InlineData("resume", "zz000000", "there is no ask or quest `#zz000000` on this machine.")]
    [InlineData("pause", null, "a pause proposal names one ask or one quest, by its id.")]
    [InlineData("pause", " # ", "a pause proposal names one ask or one quest, by its id.")]
    [InlineData("stop", AskId, "`stop` is not a pause's change — `pause` or `resume`; nothing else is proposed of an ask's or a quest's work.")]
    public void A_proposal_the_pause_would_not_take_is_refused(string door, string? target, string refusal)
    {
        var plan = HelpProposals.Plan(Pause(door, target), DriverConfig.Empty, Works);

        Assert.Equal(refusal, plan.Refusal);
        Assert.Null(plan.Pause);
    }

    /// <summary>D132 §7.4: abandon declines quests with the person's reason, which is their answer (D37), so it is never proposed.</summary>
    [Fact]
    public void An_abandon_is_refused_as_the_persons_own()
    {
        var plan = HelpProposals.Plan(Pause("abandon", AskId), DriverConfig.Empty, Works);

        Assert.Equal(
            "`abandon` is not a pause's change — `pause` or `resume`; abandoning is the person's own, on the ask's or the quest's "
            + "page, where a go takes them: its reason is their answer, which Ask Daoris never writes.",
            plan.Refusal);
    }

    /// <summary>PAUSE1f-c: the box writes which one was named, so an id both have is the one it said; a word it does not know is the lookup's.</summary>
    [Theory]
    [InlineData("ask", WorkScope.Ask)]
    [InlineData("quest", WorkScope.Quest)]
    public void An_id_both_have_is_the_one_the_proposal_names(string value, WorkScope scope)
    {
        var twin = Quest(AskId, "Open");
        var facts = Works with { Work = [AskPlan(), QuestPlan(twin)] };

        var plan = HelpProposals.Plan(Pause("pause", AskId) with { Value = value }, DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(scope, plan.Pause!.Scope);
        Assert.Equal(AskId, plan.Pause.Id);
    }

    /// <summary>A named kind that this machine has no work of is not found, though the other kind has the id.</summary>
    [Fact]
    public void A_named_kind_the_machine_does_not_have_is_not_found()
    {
        var plan = HelpProposals.Plan(Pause("pause", AskId) with { Value = "quest" }, DriverConfig.Empty, Works);

        Assert.Equal("there is no quest `#a1paus00` on this machine.", plan.Refusal);
    }

    [Fact]
    public void A_word_that_is_not_a_kind_leaves_the_lookup_to_tell()
    {
        var plan = HelpProposals.Plan(Pause("pause", AskId) with { Value = "both" }, DriverConfig.Empty, Works);

        Assert.Null(plan.Refusal);
        Assert.Equal(WorkScope.Ask, plan.Pause!.Scope);
    }

    /// <summary>The service writes the one id the helper named and, from a file before PAUSE1f-c, not whether it is an ask's or a quest's, so one both name is refused.</summary>
    [Fact]
    public void An_id_both_an_ask_and_a_quest_have_is_refused()
    {
        var twin = Quest(AskId, "Open");
        var plan = HelpProposals.Plan(Pause("pause", AskId), DriverConfig.Empty, Works with { Work = [AskPlan(), QuestPlan(twin)] });

        Assert.Equal(
            "`#a1paus00` names both an ask and a quest on this machine, and a pause proposal does not say which: the person "
            + "pauses it on its page.",
            plan.Refusal);
    }

    [Fact]
    public async Task A_pause_goes_through_the_pages_own_pause_and_its_refusal_is_said_in_its_words()
    {
        var (applied, doors) = await ApplyAsync(Pause("pause", AskId), DriverConfig.Empty, Works);
        var refusing = new HelpStandInDoors { WorkPaused = (false, "no ask #a1paus00 on this machine.") };
        var (refused, _) = await ApplyAsync(Pause("pause", AskId) with { Id = "p9" }, DriverConfig.Empty, Works, refusing);

        Assert.True(applied.Applied);
        Assert.Equal(["WORK_PAUSE ask a1paus00"], doors.Calls);
        Assert.Equal("Applied: `#p6` (`daoris-driver ask --pause a1paus00`) — Paused it.", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
        Assert.False(refused.Applied);
        Assert.Equal("Not applied: `#p9` (`daoris-driver ask --pause a1paus00`) — no ask #a1paus00 on this machine.", refused.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p9")!.State);
    }

    [Fact]
    public async Task A_resume_goes_through_the_pages_own_resume()
    {
        var config = DriverConfig.Empty.WithPausedQuest("q2taken0", new WorkPause(At, new Dictionary<string, string>()));

        var (applied, doors) = await ApplyAsync(Pause("resume", "q2taken0"), config, Works);

        Assert.True(applied.Applied);
        Assert.Equal(["WORK_RESUME quest q2taken0"], doors.Calls);
        Assert.Equal("Applied: `#p6` (`daoris-driver quest resume q2taken0`) — Resumed it.", applied.Told);
    }

    /// <summary>A card that changes nothing calls no door when applied, and says so.</summary>
    [Fact]
    public async Task A_card_that_changes_nothing_calls_no_door()
    {
        var (applied, doors) = await ApplyAsync(Pause("resume", "q2taken0"), DriverConfig.Empty, Works);

        Assert.True(applied.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal(
            "Applied: `#p6` — nothing changed: Quest `#q2taken0` is not paused on this machine, so applying changes nothing.",
            applied.Told);
    }

    /// <summary>The proposal's file written, judged against the config and the machine, and applied through stand-in doors.</summary>
    private async Task<(HelpApplied Applied, HelpStandInDoors Doors)> ApplyAsync(
        HelpProposal proposal, DriverConfig config, HelpMachineFacts facts, HelpStandInDoors? doors = null)
    {
        File(proposal.Id, proposal.Kind, proposal.Door, proposal.Target);
        doors ??= new HelpStandInDoors();
        var plan = HelpProposals.Plan(proposal, config, facts);
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);
        return (applied, doors);
    }
}

public sealed partial class HelpStandInDoors
{
    public (bool Ok, string Message) WorkPaused { get; set; } = (true, "Paused it.");

    public (bool Ok, string Message) WorkResumed { get; set; } = (true, "Resumed it.");

    public Task<(bool Ok, string Message)> PauseWorkAsync(WorkScope scope, string id, CancellationToken ct)
    {
        Calls.Add($"WORK_PAUSE {WorkPausing.Word(scope)} {id}");
        return Task.FromResult(WorkPaused);
    }

    public Task<(bool Ok, string Message)> ResumeWorkAsync(WorkScope scope, string id, CancellationToken ct)
    {
        Calls.Add($"WORK_RESUME {WorkPausing.Word(scope)} {id}");
        return Task.FromResult(WorkResumed);
    }
}
