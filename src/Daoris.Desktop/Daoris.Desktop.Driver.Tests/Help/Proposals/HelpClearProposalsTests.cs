using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>clear</c> proposal (HIST1f-b, D153, the history-clearing design §6.4): a closed quest's work, an ask's or a
/// workspace's finished history cleared from this machine. Its card is the first press, judged by what
/// <see cref="HistoryClearing.PlanAsync"/> answers; drawing it holds the units it listed in the proposal's file, and Apply, the
/// second press, sends exactly those through the screen's own clear, which judges each again (§5). Nothing is cleared until the
/// person's press (D89), and Ask Daoris never clears (D95, D37).
/// </summary>
public sealed class HelpClearProposalsTests : HelpProposalsFixture
{
    private static HelpProposal Clear(string door, string? target) => Of("clear", door, target);

    private static readonly HistoryUnitPlan QuestDone = new(HistoryKinds.Quest, "q1done00", "work")
    {
        Quests = ["q1done00"], Sessions = ["s1ended0", "s2ended0"], Bytes = new HistoryBytes(1024, 2048, 0, 0, 0),
    };

    /// <summary>A quest a remote numbered: forgotten here, and kept by the team (§3.2).</summary>
    private static readonly HistoryUnitPlan QuestForgotten = new(HistoryKinds.Quest, "q4done00", "work")
    {
        Quests = ["q4done00"], Forgotten = ["q4done00"], Sessions = ["s4ended0"], Bytes = new HistoryBytes(512, 0, 0, 0, 0),
    };

    private static readonly HistoryUnitPlan QuestOpen = new(HistoryKinds.Quest, "q2open00", "work")
    {
        Quests = ["q2open00"], Keep = new HistoryKeep(HistoryWords.Open, "`#q2open00` is still open: its record is work in progress."),
    };

    private static readonly HistoryUnitPlan QuestAsked = new(HistoryKinds.Quest, "q3asked0", "work")
    {
        Keep = new HistoryKeep(HistoryWords.Asked, "ask `#a1done00` asked it; clear the ask, which takes every quest it became."),
    };

    private static readonly HistoryUnitPlan QuestUnknown = new(HistoryKinds.Quest, "zz000000", null)
    {
        Keep = new HistoryKeep(HistoryWords.Unknown, "No quest `#zz000000` on this machine."),
    };

    private static readonly HistoryUnitPlan AskDone = new(HistoryKinds.Ask, "a1done00", "work")
    {
        Asks = ["a1done00"], Quests = ["q3asked0", "q5done00"], Sessions = ["i1intake", "s5ended0", "s6ended0"],
        Teammates = ["north/s7team0"], Bytes = new HistoryBytes(4096, 4096, 0, 1024, 0),
    };

    private static readonly HistoryUnitPlan AskNeedsYou = new(HistoryKinds.Ask, "a2needs0", "work")
    {
        Keep = new HistoryKeep(HistoryWords.NeedsYou, "ask `#a2needs0` waits for you to publish or close it."),
    };

    private static readonly HistoryUnitPlan QuestSeven = new(HistoryKinds.Quest, "q7done00", "work")
    {
        Quests = ["q7done00"], Sessions = ["s7ended0"], Bytes = new HistoryBytes(1024, 0, 0, 0, 0),
    };

    /// <summary>HIST1f-d: a closed quest's failed sessions of this machine's, and a teammate's kept (§4).</summary>
    private static readonly HistoryUnitPlan FailedDone = new(HistoryKinds.Failed, "q1done00", "work")
    {
        Sessions = ["f1fail00", "f2fail00"], Bytes = new HistoryBytes(512, 512, 0, 0, 0),
        Kept = [new HistoryKeep(HistoryWords.Live, "session `t1team00` ran on north and stays there.") { Session = "t1team00" }],
    };

    private static readonly HistoryUnitPlan FailedNone = new(HistoryKinds.Failed, "q8done00", "work");

    private static readonly HistoryUnitPlan FailedOpen = new(HistoryKinds.Failed, "q2open00", "work")
    {
        Keep = new HistoryKeep(HistoryWords.Open, "`#q2open00` is still open: its failed sessions count its strikes.") { Context = HistoryContexts.Failed },
    };

    private static HistoryPlan One(HistoryScope scope, string id, HistoryUnitPlan unit) => new(scope, id, [unit]);

    private static HistoryPlan Work(HistoryReading reading, params HistoryUnitPlan[] units) =>
        new(HistoryScope.Workspace, "work", units) { Reading = reading };

    private static readonly HistoryReading WorkKeeps = new("work")
    {
        Quests = 5, Asks = 2, Sessions = 6, Teammates = 1, LeftOver = 1, LeftOverBytes = 100,
    };

    /// <summary>What this machine keeps as <see cref="HistoryClearing.PlanAsync"/> answers it, for each id a clear proposal names.</summary>
    private static readonly HistoryMachine Kept = new(
    [
        One(HistoryScope.Quest, "q1done00", QuestDone),
        One(HistoryScope.Quest, "q4done00", QuestForgotten),
        One(HistoryScope.Quest, "q2open00", QuestOpen),
        One(HistoryScope.Quest, "q3asked0", QuestAsked),
        One(HistoryScope.Quest, "zz000000", QuestUnknown),
        One(HistoryScope.Ask, "a1done00", AskDone),
        One(HistoryScope.Failed, "q1done00", FailedDone),
        One(HistoryScope.Failed, "q8done00", FailedNone),
        One(HistoryScope.Failed, "q2open00", FailedOpen),
        One(HistoryScope.Failed, "zz000000", QuestUnknown),
        Work(WorkKeeps, QuestDone, QuestForgotten, AskDone, QuestOpen, AskNeedsYou),
        new(HistoryScope.Workspace, "default", []) { Reading = new HistoryReading("default") },
    ]);

    private sealed record HistoryMachine(IReadOnlyList<HistoryPlan> Plans)
    {
        public static implicit operator HelpMachineFacts(HistoryMachine machine) => Facts with { History = machine.Plans };
    }

    private static HelpPlan Planned(HelpProposal proposal, HelpMachineFacts? facts = null) =>
        HelpProposals.Plan(proposal, DriverConfig.Empty, facts ?? Kept);

    /// <summary>§6.1's ask, in the terminal's words: what goes, what the home kept of it, and that nothing brings it back.</summary>
    [Fact]
    public void A_closed_quests_work_is_listed_with_what_it_takes_and_the_terminals_line()
    {
        var plan = Planned(Clear("quest", "#Q1DONE00"));

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Clears `#q1done00` from this machine: 1 quest and 2 sessions, with what this machine kept of them (3 KB): their words, "
            + "transcripts and files. Nothing brings it back.",
            plan.Describe);
        Assert.Equal("daoris-driver quest clear q1done00 --yes", plan.Terminal);
        Assert.Equal(new HistoryUnitName(HistoryKinds.Quest, "q1done00"), Assert.Single(plan.Clear!.Units));
        Assert.Equal(HistoryScope.Quest, plan.Clear.Scope);
        Assert.False(plan.Clear.Held);
    }

    [Fact]
    public void An_asks_work_goes_whole_and_its_card_counts_every_piece()
    {
        var plan = Planned(Clear("ask", "a1done00"));

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Clears ask `#a1done00` from this machine: 1 ask, 2 quests, 3 sessions and 1 teammate's copy, with what this machine kept "
            + "of them (9 KB): their words, transcripts and files. Nothing brings it back.",
            plan.Describe);
        Assert.Equal("daoris-driver ask --clear a1done00 --yes", plan.Terminal);
        Assert.Equal([new HistoryUnitName(HistoryKinds.Ask, "a1done00")], plan.Clear!.Units);
    }

    /// <summary>§6.1: on a wired workspace the ask's last sentence says the remote keeps the team's copy (§3.2).</summary>
    [Fact]
    public void A_quest_a_remote_numbered_says_the_remote_keeps_the_teams_copy()
    {
        var plan = Planned(Clear("quest", "q4done00"));

        Assert.EndsWith(
            "Nothing brings it back. The remote for `work` keeps the team's copy; this machine will not fetch it again.", plan.Describe);
    }

    /// <summary>
    /// §1.2: a quest or an ask the clear would keep is refused in the keep's own sentence, as the page offers no press for it;
    /// a quest an ask asked names the ask to clear instead. An id this machine has no record of is refused too.
    /// </summary>
    [Theory]
    [InlineData("quest", "q2open00", "`#q2open00` stays on this machine: `#q2open00` is still open: its record is work in progress.")]
    [InlineData("quest", "Q3ASKED0", "`#q3asked0` stays on this machine: ask `#a1done00` asked it; clear the ask, which takes every quest it became.")]
    [InlineData("quest", "zz000000", "No quest `#zz000000` on this machine.")]
    [InlineData("ask", "a9none00", "there is no ask `#a9none00` on this machine.")]
    [InlineData("workspace", "nowhere", "there is no workspace `nowhere` on this machine — use a workspace's name as Repositories lists it.")]
    [InlineData("quest", " # ", "a clear proposal names one quest or one ask by its id, or one workspace by its name.")]
    [InlineData("failed", "q2open00", "`#q2open00`'s failed sessions stay on this machine: `#q2open00` is still open: its failed sessions count its strikes.")]
    [InlineData("failed", "zz000000", "No quest `#zz000000` on this machine.")]
    [InlineData("failed", "q9none00", "there is no quest `#q9none00` on this machine.")]
    [InlineData("failed", " # ", "a clear proposal names one quest or one ask by its id, or one workspace by its name.")]
    [InlineData("delete", "q1done00", "`delete` is not a clear's door — `quest`, `ask`, `failed` or `workspace`; nothing else clears history.")]
    public void What_the_clear_would_keep_or_cannot_name_is_refused_in_its_words(string door, string target, string refusal)
    {
        var plan = Planned(Clear(door, target));

        Assert.Equal(refusal, plan.Refusal);
        Assert.Null(plan.Clear);
    }

    /// <summary>
    /// HIST1f-d, §1.1, §4: a closed quest's failed sessions are the quest page's *Clear failed sessions…*, and the card names them,
    /// not the quest, says the quest and its other sessions stay, and keeps a teammate's failed session in its keep's sentence.
    /// </summary>
    [Fact]
    public void A_closed_quests_failed_sessions_are_listed_and_the_quest_stays()
    {
        var plan = Planned(Clear("failed", "#Q1DONE00"));

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Clears `#q1done00`'s failed sessions from this machine: 2 sessions, with what this machine kept of them (1 KB): their "
            + "words, transcripts and files. Nothing brings it back. It keeps `t1team00`: session `t1team00` ran on north and stays there.",
            plan.Describe);
        Assert.Equal("daoris-driver quest clear q1done00 --failed --yes", plan.Terminal);
        Assert.Equal(new HistoryUnitName(HistoryKinds.Failed, "q1done00"), Assert.Single(plan.Clear!.Units));
        Assert.Equal(HistoryScope.Failed, plan.Clear.Scope);
    }

    /// <summary>D48 §6: a closed quest with none of this machine's failed sessions is said, never refused, and its Apply calls no door.</summary>
    [Fact]
    public async Task A_quest_with_no_failed_session_of_this_machines_says_so_and_changes_nothing()
    {
        var plan = Planned(Clear("failed", "q8done00"));
        var (applied, doors, _) = await ApplyAsync(Clear("failed", "q8done00"), facts: Kept);

        Assert.Null(plan.Refusal);
        Assert.Null(plan.Clear);
        Assert.Equal("`#q8done00`'s failed sessions: this machine keeps none of them, so applying changes nothing.", plan.Describe);
        Assert.True(applied.Applied);
        Assert.Empty(doors.Calls);
    }

    /// <summary>§5, §6.4: Apply sends exactly the failed unit the card listed, through the screen's own clear.</summary>
    [Fact]
    public async Task A_failed_sessions_clear_goes_through_the_screens_door_and_says_what_went()
    {
        var doors = new HelpStandInDoors
        {
            HistoryCleared = new HistoryOutcome(HistoryScope.Failed, "q1done00")
            {
                Listed = 1, Cleared = [FailedDone with { Kept = [] }], Bytes = 1024,
            },
        };

        var (applied, _) = await DrawThenApplyAsync(Clear("failed", "q1done00"), Kept, Kept, doors);

        Assert.Equal(["HISTORY_CLEAR failed q1done00 failed:q1done00"], doors.Calls);
        Assert.True(applied.Applied);
        Assert.Equal(
            "Applied: `#p6` (`daoris-driver quest clear q1done00 --failed --yes`) — cleared `#q1done00`'s failed sessions from this "
            + "machine: 2 sessions, 1 KB.",
            applied.Told);
    }

    /// <summary>
    /// §5 step 1, §6.1: a workspace's first press counts what goes by kind, names each unit it takes, and lists every kept unit
    /// with its reason; the left-over files go with it. The terminal's twin is the list's own <c>--yes</c>.
    /// </summary>
    [Fact]
    public void A_workspaces_card_counts_what_goes_and_lists_every_kept_unit_with_its_reason()
    {
        var plan = Planned(Clear("workspace", "Work"));

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Clears `work`'s finished history from this machine: 4 quests, 1 ask, 6 sessions, 1 teammate's copy and 1 left-over "
            + "file, 12.6 KB. Nothing brings it back. It takes `#q1done00`, `#q4done00` and ask `#a1done00`. It keeps `#q2open00`: "
            + "`#q2open00` is still open: its record is work in progress. It keeps ask `#a2needs0`: ask `#a2needs0` waits for you "
            + "to publish or close it. The remote for `work` keeps the team's copy of 1 of these quests; this machine will not "
            + "fetch it again. Apply clears what this card lists, each judged again as it goes.",
            plan.Describe);
        Assert.Equal("daoris-driver history clear --workspace work --yes", plan.Terminal);
        Assert.Equal(
            [new HistoryUnitName("quest", "q1done00"), new HistoryUnitName("quest", "q4done00"), new HistoryUnitName("ask", "a1done00")],
            plan.Clear!.Units);
    }

    /// <summary>D48 §6: nothing to clear is information, never a refusal, and its card's Apply calls no door.</summary>
    [Fact]
    public async Task A_workspace_with_nothing_that_may_go_says_so_and_changes_nothing()
    {
        HelpMachineFacts kept = new HistoryMachine([Work(WorkKeeps with { LeftOver = 0, LeftOverBytes = 0 }, QuestOpen)]);

        var plan = Planned(Clear("workspace", "work"), kept);
        var empty = Planned(Clear("workspace", "default"));
        var (applied, doors, _) = await ApplyAsync(Clear("workspace", "work"), facts: kept);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Nothing of `work`'s finished history may be cleared now, so applying changes nothing. It keeps `#q2open00`: "
            + "`#q2open00` is still open: its record is work in progress.",
            plan.Describe);
        Assert.Null(plan.Clear);
        Assert.Equal("daoris-driver history clear --workspace work", plan.Terminal);
        Assert.Equal("`default` keeps no finished work on this machine, so applying changes nothing.", empty.Describe);
        Assert.True(applied.Applied);
        Assert.Empty(doors.Calls);
        Assert.StartsWith("Applied: `#p6` — nothing changed: Nothing of `work`'s finished history", applied.Told);
    }

    /// <summary>
    /// §5: the card is the first press, so drawing it holds what it listed in the proposal's file, once; a later draw, a refused
    /// card and one that changes nothing hold nothing.
    /// </summary>
    [Fact]
    public void Drawing_the_card_holds_what_it_listed_in_the_proposals_file_once()
    {
        var id = File("p7", "clear", "workspace", "work");
        var proposal = HelpProposals.Find(_home, id)!;
        Assert.Null(proposal.Units);

        HelpProposals.Drawn(_home, proposal, Planned(proposal));
        HelpProposals.Drawn(_home, proposal, Planned(proposal, new HistoryMachine([Work(WorkKeeps, QuestSeven)])));
        var refused = HelpProposals.Find(_home, File("p8", "clear", "quest", "q2open00"))!;
        HelpProposals.Drawn(_home, refused, Planned(refused));
        var nothing = HelpProposals.Find(_home, File("p9", "clear", "workspace", "default"))!;
        HelpProposals.Drawn(_home, nothing, Planned(nothing));

        Assert.Equal(
            [new HistoryUnitName("quest", "q1done00"), new HistoryUnitName("quest", "q4done00"), new HistoryUnitName("ask", "a1done00")],
            HelpProposals.Find(_home, id)!.Units);
        Assert.Equal("proposed", HelpProposals.Find(_home, id)!.State);
        Assert.Null(HelpProposals.Find(_home, "p8")!.Units);
        Assert.Null(HelpProposals.Find(_home, "p9")!.Units);
    }

    /// <summary>
    /// §5 step 2, §6.4: Apply sends exactly the units the card listed, never a fresh plan's. One kept since, or gone, is still
    /// sent, for the press to judge and count; one that may go now and the card did not list is said, and left to a new card.
    /// </summary>
    [Fact]
    public async Task Apply_sends_exactly_what_the_card_listed_and_the_card_says_what_changed_since()
    {
        var since = QuestForgotten with { Keep = new HistoryKeep(HistoryWords.Live, "session `s4ended0` is still running; stop it first.") };
        HelpMachineFacts now = new HistoryMachine([Work(WorkKeeps with { LeftOver = 0, LeftOverBytes = 0 }, QuestDone, since, QuestOpen, QuestSeven)]);
        var doors = new HelpStandInDoors
        {
            HistoryCleared = new HistoryOutcome(HistoryScope.Workspace, "work")
            {
                Listed = 3, Cleared = [QuestDone], Bytes = 3072,
                Changed = [since, AskDone with { Keep = new HistoryKeep(HistoryWords.Unknown, "No ask `#a1done00` on this machine.") }],
            },
        };

        var (applied, plan) = await DrawThenApplyAsync(Clear("workspace", "work"), Kept, now, doors);

        Assert.Equal(
            "Clears `work`'s finished history from this machine: 1 quest and 2 sessions, 3 KB. Nothing brings it back. It takes "
            + "`#q1done00`. `#q4done00` changed since this card listed it and is kept now: session `s4ended0` is still running; stop "
            + "it first. Ask `#a1done00` is no longer on this machine. It keeps `#q2open00`: `#q2open00` is still open: its record "
            + "is work in progress. This card did not list `#q7done00`, which may be cleared now too: a new proposal lists it. Apply "
            + "clears what this card lists, each judged again as it goes.",
            plan.Describe);
        Assert.Equal(["HISTORY_CLEAR workspace work quest:q1done00,quest:q4done00,ask:a1done00"], doors.Calls);
        Assert.True(applied.Applied);
        Assert.Equal(
            "Applied: `#p6` (`daoris-driver history clear --workspace work --yes`) — cleared `work`'s finished history from this "
            + "machine: 1 of 3 listed, 1 quest and 2 sessions, 3 KB; 2 changed since the list and were kept. Kept `#q4done00`, "
            + "which changed since the list: session `s4ended0` is still running; stop it first. Kept ask `#a1done00`, which "
            + "changed since the list: No ask `#a1done00` on this machine.",
            applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
    }

    [Fact]
    public async Task A_quests_clear_goes_through_the_screens_door_and_says_what_went()
    {
        var doors = new HelpStandInDoors
        {
            HistoryCleared = new HistoryOutcome(HistoryScope.Quest, "q1done00") { Listed = 1, Cleared = [QuestDone], Bytes = 3072 },
        };

        var (applied, _) = await DrawThenApplyAsync(Clear("quest", "q1done00"), Kept, Kept, doors);

        Assert.Equal(["HISTORY_CLEAR quest q1done00 quest:q1done00"], doors.Calls);
        Assert.True(applied.Applied);
        Assert.Equal(
            "Applied: `#p6` (`daoris-driver quest clear q1done00 --yes`) — cleared `#q1done00` from this machine: 1 quest and 2 "
            + "sessions, 3 KB.",
            applied.Told);
    }

    /// <summary>§5: a unit that changed since the list is kept by the press and counted; the card is settled refused, in its words.</summary>
    [Fact]
    public async Task A_quest_the_press_keeps_is_not_applied_and_says_why()
    {
        var keep = new HistoryKeep(HistoryWords.Live, "session `s1ended0` is still running; stop it first.");
        var doors = new HelpStandInDoors
        {
            HistoryCleared = new HistoryOutcome(HistoryScope.Quest, "q1done00") { Listed = 1, Changed = [QuestDone with { Keep = keep }] },
        };

        var (applied, _) = await DrawThenApplyAsync(Clear("quest", "q1done00"), Kept, Kept, doors);

        Assert.False(applied.Applied);
        Assert.Equal(
            "Not applied: `#p6` (`daoris-driver quest clear q1done00 --yes`) — kept `#q1done00`, which changed since the list: "
            + "session `s1ended0` is still running; stop it first.",
            applied.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>
    /// A quest the card listed that may no longer go by the Apply is refused before any door, in the keep's sentence, so nothing
    /// is sent that the press would only keep.
    /// </summary>
    [Fact]
    public async Task A_listed_quest_kept_since_is_refused_before_any_door()
    {
        HelpMachineFacts now = new HistoryMachine([One(HistoryScope.Quest, "q1done00", QuestDone with { Keep = QuestOpen.Keep })]);

        var (applied, doors) = await DrawThenApplyAsync(Clear("quest", "q1done00"), Kept, now);

        Assert.False(applied.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal(
            "Not applied: `#p6` — the route refuses it: `#q1done00` stays on this machine: `#q2open00` is still open: its record is "
            + "work in progress.",
            applied.Told);
    }

    /// <summary>
    /// A card never drawn has listed nothing, so its Apply sends nothing and leaves it standing, unsettled, for the card to be drawn
    /// and pressed: Apply is the second press, never a first.
    /// </summary>
    [Fact]
    public async Task A_card_never_drawn_is_not_applied_and_stays()
    {
        var error = await Assert.ThrowsAsync<DriverException>(() => ApplyAsync(Clear("workspace", "work"), facts: Kept));

        Assert.Equal(
            "the card for `#p6` has not listed what it clears yet: Apply clears only what its card lists, so it is drawn first, then "
            + "applied.",
            error.Message);
        Assert.Equal("proposed", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>The proposal's file written as the box writes it, drawn against one machine, then judged and applied against another.</summary>
    private async Task<(HelpApplied Applied, HelpPlan Plan)> DrawThenApplyAsync(
        HelpProposal proposal, HelpMachineFacts drawn, HelpMachineFacts now, HelpStandInDoors doors)
    {
        File(proposal.Id, proposal.Kind, proposal.Door, proposal.Target);
        var first = HelpProposals.Find(_home, proposal.Id)!;
        HelpProposals.Drawn(_home, first, HelpProposals.Plan(first, DriverConfig.Empty, drawn));
        var held = HelpProposals.Find(_home, proposal.Id)!;
        var plan = HelpProposals.Plan(held, DriverConfig.Empty, now);
        return (await HelpProposals.ApplyAsync(_home, held, plan, doors, _ => { }, CancellationToken.None), plan);
    }

    private async Task<(HelpApplied Applied, HelpStandInDoors Doors)> DrawThenApplyAsync(
        HelpProposal proposal, HelpMachineFacts drawn, HelpMachineFacts now)
    {
        var doors = new HelpStandInDoors();
        var (applied, _) = await DrawThenApplyAsync(proposal, drawn, now, doors);
        return (applied, doors);
    }
}

public sealed partial class HelpStandInDoors
{
    /// <summary>What the screen's clear answers; by default every unit sent was listed and none went.</summary>
    public HistoryOutcome? HistoryCleared { get; set; }

    public Task<HistoryOutcome> ClearHistoryAsync(HistoryScope scope, string id, IReadOnlyList<HistoryUnitName> units, CancellationToken ct)
    {
        Calls.Add($"HISTORY_CLEAR {HistoryClearing.Word(scope)} {id} {string.Join(",", units.Select(unit => $"{unit.Kind}:{unit.Id}"))}");
        return Task.FromResult(HistoryCleared ?? new HistoryOutcome(scope, id) { Listed = units.Count });
    }
}
