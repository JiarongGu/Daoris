using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The gate's rule over every owed opinion, as its bus consumer judges each at a fact (<see cref="OpinionGateJudge.JudgeDueAsync"/>).</summary>
internal static class GateJudging
{
    internal static async Task<(IReadOnlyDictionary<string, OpinionSit> Sits, int Passes)> OpinionsAsync(
        this Daoris.Driver.Driver driver, Snapshot snapshot, List<string> events, CancellationToken ct, List<OpinionJudged>? judged = null)
    {
        var sits = new Dictionary<string, OpinionSit>(StringComparer.OrdinalIgnoreCase);
        var passes = 0;
        foreach (var due in new OpinionDues(driver.Context.Home).Open())
        {
            if (await driver.Gate.JudgeDueAsync(due, ct, events) is not { } fact) continue;
            if (fact.Begun) passes++;
            if (fact.Judged is { } kept) judged?.Add(kept);
            foreach (var (quest, why) in fact.Sits) sits[quest] = why;
        }

        return (sits, passes);
    }
}

/// <summary>
/// The second opinions owed at a look and the
/// person's presses, over real git. A done in a tree of its own under a rule that reads it is owed one; the look asks the pass
/// beside itself, from where the work leaves the line to its tip, keeps it at the gate the moment the host opens it, and sits the
/// chain's set-up step while an agent is still at work on it; once settled, it asks nothing more. The presses keep the person's
/// answer bound to its commit, ask a pass for the next look among the rule's reviewers only, and refuse what nothing waits on.
/// </summary>
/// <remarks>
/// Real git for the tree, the local host stood in (<see cref="OpinionStandIn"/>), and the pass stood in (<see cref="Daoris.Driver.Driver.Passes"/>):
/// no agent runs and nothing reaches a network.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class OpinionLookTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly List<(OpinionPassAsk Ask, ReviewerChoice Choice)> _passes = [];

    public OpinionLookTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "opinion-look", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task The_look_asks_an_owed_pass_beside_itself_keeps_it_at_the_gate_and_sits_the_set_up_step_until_settled()
    {
        var (host, tree, line) = await DoneAsync(required: false);
        host.Quest("q2", "Open", parent: "q1", setUpIn: "local");
        var driver = Driver(host);

        var (sits, passes) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        Assert.Equal(1, passes);
        Assert.StartsWith("set-up step `#q2` waits for a second opinion on the work it shows", sits["q2"].Reason);
        Assert.Equal(SittingFact.Landing, sits["q2"].Fact.Code);
        var (ask, choice) = Assert.Single(_passes);
        Assert.Equal((OpinionRules.Landing, "s1", "reports", line, await HeadAsync(tree)), (ask.Occasion, ask.Working, ask.Repository, ask.Base, ask.Tip));
        Assert.Equal(["q1", "q2"], ask.Quests.Select(quest => quest.Id));
        Assert.Equal(("codex-acp", ReviewerLabels.AnotherMaker), (choice.Reviewer, choice.Label));
        var kept = Assert.Single(new OpinionGates(_home).Read("q1", "reports").Asks);
        Assert.Equal(("op1", OpinionAskers.Look, "codex-acp"), (kept.Opinion, kept.By, kept.Reviewer));

        // Settled: nothing more is asked, and the set-up step goes on.
        var (after, again) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        Assert.Equal(0, again);
        Assert.Empty(after);
        Assert.Single(_passes);
    }

    /// <summary>
    /// An update's drain holds the second opinion's pass, a reviewer's session in a copy. The owed
    /// opinion is still judged and its quests still sit, but no pass starts, none is kept at the gate, and the look says the drain's hold
    /// once; after *Not now* the pass is asked.
    /// </summary>
    [Fact]
    public async Task A_draining_look_asks_no_pass_for_an_owed_opinion_and_after_not_now_it_asks()
    {
        var (host, _, _) = await DoneAsync(required: false);
        host.Quest("q2", "Open", parent: "q1", setUpIn: "local");
        var draining = Driver(host, drained: true);
        var events = new List<string>();

        var (sits, passes) = await draining.OpinionsAsync(new Snapshot([], [], []), events, CancellationToken.None);
        var (_, again) = await draining.OpinionsAsync(new Snapshot([], [], []), events, CancellationToken.None);
        await draining.Running.SettledAsync();

        Assert.Equal((0, 0), (passes, again));
        Assert.Empty(_passes);
        Assert.StartsWith("set-up step `#q2` waits for a second opinion", sits["q2"].Reason);
        Assert.Empty(new OpinionGates(_home).Read("q1", "reports").Asks);
        Assert.Equal(InstallUpdate.HeldLine("opinion", "the second opinion owed on session `s1`'s work"), Assert.Single(events));

        var driver = Driver(host);
        var (_, later) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        Assert.Equal(1, later);
        Assert.Single(_passes);
    }

    /// <summary>
    /// An opinion asked on a line that ran on two makers refuses a reviewer of
    /// either as another maker's. s1's work went on with Claude Code from s0, cut off by its limit on Codex; the look judges the work's
    /// families from every part of the line, so a listed reviewer of either maker reads it only as the same agent, fresh, and says
    /// that no other maker's agent could.
    /// </summary>
    [Theory]
    [InlineData("codex-acp")]
    [InlineData("claude-code")]
    public async Task An_opinion_asked_on_a_line_that_ran_on_two_makers_refuses_a_reviewer_of_either(string reviewer)
    {
        var (host, _, _) = await DoneAsync(required: false, reviewers: [reviewer]);
        host.Session("s0", "failed", adapter: "codex-acp");
        host.Set("s1", "from", "s0");
        host.Set("s1", "line", "s0");
        host.Set("s1", "fromWhy", HandOverWhy.Limit);
        var driver = Driver(host);

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        var (_, choice) = Assert.Single(_passes);
        Assert.Equal((reviewer, ReviewerLabels.SameAgent, ReviewerUnavailable.NotIndependent), (choice.Reviewer, choice.Label, choice.Code));
        Assert.Equal(["OpenAI", "Anthropic"], choice.Working.Select(family => family.Maker));
        Assert.Equal(ReviewerLabels.SameAgent, Assert.Single(new OpinionGates(_home).Read("q1", "reports").Asks).Label);
    }

    /// <summary>
    /// A due a chat run's press wrote names its run and no quest. The look reads the run's
    /// process and gate, asks the pass at the tip the press captured, not the tree's HEAD a later turn moved, hands it the conversation's
    /// opening and no quest, keeps it under the run's key, and holds no quest's set-up step.
    /// </summary>
    [Fact]
    public async Task The_look_asks_a_chat_runs_pass_at_the_press_tip_with_its_opening_keyed_by_the_run()
    {
        var (host, tree, line) = await DoneAsync(required: false);
        Govern("chat-c1a2b3c4", required: false, on: null);
        new OpinionDues(_home).Close("s1", OpinionRules.Landing, OpinionDueClosed.Off, DateTimeOffset.UtcNow);
        host.Quest("q2", "Open", parent: "q1", setUpIn: "local");
        var pressed = await HeadAsync(tree);
        await CommitAsync(tree, "later.ts", "a later turn", "a later turn's work");
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).Append(
            "c1a2b3c4", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the totals add up.\nThanks." });
        host.Session("c1a2b3c4", "working", tree: tree, kind: "chat");
        new OpinionDues(_home).Due(new OpinionDue("c1a2b3c4", "", "reports", "work", tree, OpinionRules.Landing, DateTimeOffset.UtcNow)
        {
            Run = "chat-c1a2b3c4", Tip = pressed, Turns = 1,
        });
        var driver = Driver(host);

        var (sits, passes) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        Assert.Equal(1, passes);
        Assert.Empty(sits);
        var (ask, _) = Assert.Single(_passes);
        Assert.Equal(("c1a2b3c4", pressed, line, "Make the totals add up."), (ask.Working, ask.Tip, ask.Base, ask.Opening));
        Assert.Empty(ask.Quests);
        Assert.Null(ask.Words);
        Assert.Equal(("op1", pressed), (Assert.Single(new OpinionGates(_home).Read("chat-c1a2b3c4", "reports").Asks).Opinion,
            new OpinionGates(_home).Read("chat-c1a2b3c4", "reports").Asks[0].Tip));
        Assert.Empty(new OpinionGates(_home).Read("q1", "reports").Asks);

        // §5.2 point 6: its pass ended, so the person, asked to press Accept again once it settles, is told so by a notice naming the
        // conversation, as the look reports what ended beside it.
        foreach (var ended in driver.Running.Drain()) driver.Lines.Ran(ended.Look, byPerson: false, ended.Line);
        var notice = Assert.Single(Notices.Read(_home, DateTimeOffset.UtcNow)!, each => each.Code == LookSaid.OpinionSettled);
        Assert.Equal((Notices.Look, "c1a2b3c4", "work"), (notice.Kind, notice.Session, notice.Workspace));
    }

    /// <summary>
    /// <i>Ask now</i> on a chat run reads its work instead of
    /// refusing it, and writes its due at <c>landing</c>, never at the rule's first occasion <c>steps</c>, which would close it past at once;
    /// carrying the tree's HEAD and the person's turn count read at its press. The request is kept under the run's key.
    /// </summary>
    [Fact]
    public async Task Ask_now_on_a_chat_run_writes_its_due_at_landing_with_the_press_tip_and_turn_count()
    {
        // A rule keeps its occasions landing first, so one whose first is `steps` names `steps` alone.
        var (host, tree, _) = await DoneAsync(required: false, on: [OpinionRules.Steps]);
        Govern("chat-c1a2b3c4", required: false, on: [OpinionRules.Steps]);
        foreach (var occasion in new[] { OpinionRules.Steps })
        {
            new OpinionDues(_home).Close("s1", occasion, OpinionDueClosed.Off, DateTimeOffset.UtcNow);
        }

        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).Append(
            "c1a2b3c4", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the totals add up.", Id = "w1" });
        host.Session("c1a2b3c4", "completed", tree: tree, kind: "chat").Set("c1a2b3c4", "quest", null);

        var asked = await new OpinionPresses(_home, host.Client()).AskAsync("c1a2b3c4", null, sameAgent: false, words: null, "asked");

        Assert.True(asked.Done, asked.Message);
        var due = Assert.Single(new OpinionDues(_home).Open(), each => each.OnRun);
        Assert.Equal(("chat-c1a2b3c4", OpinionRules.Landing, await HeadAsync(tree), (int?)1), (due.Run, due.Occasion, due.Tip, due.Turns));
        Assert.Equal("c1a2b3c4", Assert.Single(new OpinionGates(_home).Read("chat-c1a2b3c4", "reports").Requests).Session);
    }

    [Fact]
    public async Task The_presses_keep_the_persons_answer_and_ask_a_pass_among_the_rules_reviewers_for_the_next_look()
    {
        var (host, tree, _) = await DoneAsync(required: true);
        var presses = new OpinionPresses(_home, host.Client());

        var named = await presses.AskAsync("s1", "dsh", sameAgent: false, words: null, "asked");
        Assert.False(named.Done);
        Assert.StartsWith("`dsh` is not among `reports`'s reviewers (`codex-acp`)", named.Message);

        var asked = await presses.AskAsync("s1", null, sameAgent: true, words: "fresh eyes", "asked");
        Assert.True(asked.Done, asked.Message);
        Assert.Equal("A second opinion on session s1's work is asked: the same agent, in a fresh conversation, reads it from the driver's next look.", asked.Message);
        var request = Assert.Single(new OpinionGates(_home).Read("q1", "reports").Requests);
        Assert.Equal(("s1", "asked", true, "fresh eyes"), (request.Session, request.Occasion, request.SameAgent, request.Words));

        var anyway = await presses.AnywayAsync("s1", "a comment only", ReviewDoors.Terminal);
        Assert.True(anyway.Done, anyway.Message);
        Assert.Equal("You went on without a settled second opinion before one was asked: \"a comment only\".", anyway.Message);
        var word = Assert.Single(new OpinionGates(_home).Read("q1", "reports").Person);
        Assert.Equal((OpinionPersonSaid.Anyway, await HeadAsync(tree), ReviewDoors.Terminal), (word.Said, word.Tip, word.Door));

        var twice = await presses.MyselfAsync("s1", null, ReviewDoors.Screen);
        Assert.False(twice.Done);
        Assert.StartsWith("nothing waits for a second opinion on session s1's work:", twice.Message);

        var stop = await presses.StopAsync("nothing1", new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        Assert.Equal((false, "there is no second opinion `nothing1` on this machine."), (stop.Done, stop.Message));
    }

    /// <summary>
    /// XAGENT1g3: the look keeps each owed landing opinion's gate for the attention band's route, as that route judged it before
    /// (<see cref="OpinionPresses.GateAsync"/>): a required opinion none could be had for, and commits nobody read where the work
    /// lands by itself, each with its <c>auto</c>. A <c>steps</c> opinion is not kept.
    /// </summary>
    [Fact]
    public async Task The_look_keeps_a_landing_opinions_gate_as_the_route_judged_it_and_not_a_steps_one()
    {
        var (host, tree, _) = await DoneAsync(required: true, on: [OpinionRules.Landing, OpinionRules.Steps]);
        new OpinionGates(_home).Asked("q1", "reports", new OpinionAskKept(DateTimeOffset.UtcNow, OpinionRules.Landing, "s1", await HeadAsync(tree))
        {
            Code = ReviewerUnavailable.NoReviewer, By = OpinionAskers.Look,
        });
        var driver = Driver(host);
        var judged = new List<OpinionJudged>();

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
        await driver.Running.SettledAsync();

        var kept = Assert.Single(judged);
        var route = (await new OpinionPresses(_home, host.Client()).GateAsync("s1")).Gate!;
        Assert.Equal((route.Opinion.State, route.Opinion.Required, route.Opinion.Says), (kept.Gate.State, kept.Gate.Required, kept.Gate.Says));
        Assert.Equal((OpinionGateStates.Unavailable, true), (kept.Gate.State, kept.Gate.Required));
        Assert.Equal((route.Process?.Landing ?? Landing.Merge).AutoAccept, kept.Auto);
        Assert.Equal(("s1", "q1", "reports"), (kept.Session, kept.Quest, kept.Repository));
    }

    /// <summary>
    /// A pass an account's limit ended is gated <c>Unavailable</c> / <c>Cooling</c> with its account's
    /// reset read from its session's cool-off, and the next look asks the opinion again of a ready holder before that reset.
    /// </summary>
    [Fact]
    public async Task A_pass_a_limit_ended_is_asked_again_by_the_look_of_the_next_ready_holder_before_the_reset()
    {
        var (host, _, _) = await DoneAsync(required: true);
        var passes = 0;
        var driver = Driver(host, ask =>
        {
            var id = $"op{++passes}";
            ask.Opened?.Invoke(id, "r-" + id);
            if (passes == 1)
            {
                host.Opinion(id, "s1", state: "failed", why: OpinionViews.Limited, tip: ask.Tip, @base: ask.Base);
                return new OpinionPassRun($"failed  second opinion {id}", true) { Opinion = id, Session = "r-" + id, State = "failed" };
            }

            host.Opinion(id, "s1", weights: [], tip: ask.Tip, @base: ask.Base);
            return new OpinionPassRun($"completed  second opinion {id}", true) { Opinion = id, Session = "r-" + id, State = "completed" };
        });
        // The cool-off the limited pass's session wrote, on an agent no reviewer's walk reads, so only its reset is in play here.
        // Whole seconds, as the file keeps a moment.
        var seen = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var reset = seen.AddHours(1);
        AccountCooling.Cool(_home, new CoolingEntry("elsewhere", null, reset, Stated: true, "weekly", seen, "r-op1"), seen);

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        var judged = new List<OpinionJudged>();
        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
        await driver.Running.SettledAsync();

        // The gate read the limited pass cooling until its reset, and the same look asked the opinion of a ready holder before it.
        var gate = Assert.Single(judged).Gate;
        Assert.Equal((OpinionGateStates.Unavailable, ReviewerUnavailable.Cooling, reset), (gate.State, gate.Code, gate.Until));
        Assert.Equal(2, _passes.Count);
        Assert.Equal(["op1", "op2"], new OpinionGates(_home).Read("q1", "reports").Asks.Select(ask => ask.Opinion));
    }

    /// <summary>A reviewer that ended without an opinion is asked again by the next look itself, and the second pass settles the gate with nobody pressed.</summary>
    [Fact]
    public async Task A_reviewer_that_ended_without_an_opinion_is_asked_again_by_the_look_itself_and_the_second_pass_settles_the_gate()
    {
        var (host, _, _) = await DoneAsync(required: true);
        var passes = 0;
        var driver = Driver(host, ask =>
        {
            var id = $"op{++passes}";
            ask.Opened?.Invoke(id, "r-" + id);
            if (passes == 1)
            {
                host.Opinion(id, "s1", state: "failed", why: "ended", tip: ask.Tip, @base: ask.Base);
                return new OpinionPassRun($"failed  second opinion {id}", true) { Opinion = id, Session = "r-" + id, State = "failed" };
            }

            host.Opinion(id, "s1", weights: [], tip: ask.Tip, @base: ask.Base);
            return new OpinionPassRun($"completed  second opinion {id}", true) { Opinion = id, Session = "r-" + id, State = "completed" };
        });

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        var judged = new List<OpinionJudged>();
        var (_, asked) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
        await driver.Running.SettledAsync();

        // The look read the ended pass and asked again, naming it; no request of the person's stands.
        Assert.Equal(1, asked);
        var ended = Assert.Single(judged).Gate;
        Assert.Equal((OpinionGateStates.Unavailable, "ended", 1), (ended.State, ended.Code, ended.Ended));
        Assert.Equal(["op1", "op2"], new OpinionGates(_home).Read("q1", "reports").Asks.Select(ask => ask.Opinion));
        Assert.All(new OpinionGates(_home).Read("q1", "reports").Asks, ask => Assert.Equal(OpinionAskers.Look, ask.By));
        Assert.Empty(new OpinionGates(_home).Read("q1", "reports").Requests);

        // The second pass read it and the gate is settled: nothing more is asked, and nothing waits on the person.
        var settled = new List<OpinionJudged>();
        var (_, again) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, settled);
        await driver.Running.SettledAsync();
        Assert.Equal(0, again);
        Assert.Equal(OpinionGateStates.Settled, Assert.Single(settled).Gate.State);
        Assert.Equal(2, _passes.Count);
    }

    /// <summary>Reviewers that keep ending without an opinion are asked the bound's times and no more; the gate then stands spent, no request waiting.</summary>
    [Fact]
    public async Task Reviewers_that_stay_unanswered_past_the_bound_are_asked_no_more_and_the_gate_stands_spent()
    {
        var (host, _, _) = await DoneAsync(required: true);
        var passes = 0;
        var driver = Driver(host, ask =>
        {
            var id = $"op{++passes}";
            ask.Opened?.Invoke(id, "r-" + id);
            host.Opinion(id, "s1", state: "failed", why: "ended", tip: ask.Tip, @base: ask.Base);
            return new OpinionPassRun($"failed  second opinion {id}", true) { Opinion = id, Session = "r-" + id, State = "failed" };
        });

        var judged = new List<OpinionJudged>();
        for (var look = 0; look < OpinionAgain.Bound + 3; look++)
        {
            judged.Clear();
            await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
            await driver.Running.SettledAsync();
        }

        Assert.Equal(OpinionAgain.Bound, _passes.Count);
        var gate = Assert.Single(judged).Gate;
        Assert.Equal((OpinionGateStates.Unavailable, OpinionAgain.Bound, true), (gate.State, gate.Ended, OpinionAgain.Spent(gate)));
        Assert.Empty(new OpinionGates(_home).Read("q1", "reports").Requests);
    }

    /// <summary>A vacancy a level answered is filled once a reviewer holds the step, so its act shapes no later walk.</summary>
    [Fact]
    public async Task A_pass_that_opens_on_a_vacancy_fills_it_and_its_act_shapes_no_later_walk()
    {
        var (host, _, _) = await DoneAsync(required: false);
        var casts = new OpinionCasts(_home);
        var at = new DateTimeOffset(2026, 11, 16, 3, 40, 0, TimeSpan.Zero);
        casts.Keep("q1", "reports", new OpinionCast(at, "s1", OpinionCasting.Step, OpinionCasting.Step, OpinionCasting.Vacant, "codex-acp", ReviewerUnavailable.Cooling)
        {
            Offered = ["codex-acp"],
        });
        casts.Keep("q1", "reports", new OpinionCast(at.AddMinutes(1), "s1", LineLevels.Repository, "reports-supervisor", SupervisorKinds.Recast, "codex-acp", "its reset passed"));
        var driver = Driver(host);

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        var kept = casts.Read("q1", "reports", "s1");
        Assert.Equal((OpinionCasting.Filled, "codex-acp", "op1"), (kept[^1].Act, kept[^1].Agent, kept[^1].Why));
        Assert.Null(OpinionCasting.Newest(kept));
        Assert.False(OpinionCasting.Waiting(kept));
    }

    /// <summary>
    /// XAGENT1g3: commits made after the opinion read are kept as the route says them, the count and the sentence the same.
    /// </summary>
    [Fact]
    public async Task The_kept_gate_of_commits_since_the_opinion_read_says_what_the_route_says()
    {
        var (host, tree, _) = await DoneAsync(required: false);
        var driver = Driver(host);
        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        await CommitAsync(tree, "later.ts", "more", "a commit nobody read");
        var judged = new List<OpinionJudged>();

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
        await driver.Running.SettledAsync();

        var kept = Assert.Single(judged);
        var route = (await new OpinionPresses(_home, host.Client()).GateAsync("s1")).Gate!;
        Assert.Equal(OpinionGateStates.CommitsSince, kept.Gate.State);
        Assert.Equal((route.Opinion.State, route.Opinion.Since, route.Opinion.Says), (kept.Gate.State, kept.Gate.Since, kept.Gate.Says));
    }

    /// <summary>
    /// An owed opinion stays owed look after look while it waits on the person: each judgment over a tree and refs that did
    /// not move asks git nothing, so the look's opinions stage costs no queue for git's places however long it stays owed.
    /// A commit in the tree is read at the next look.
    /// </summary>
    [Fact]
    public async Task A_judgment_over_a_tree_that_did_not_move_starts_no_git()
    {
        var (host, tree, _) = await DoneAsync(required: false);
        var driver = Driver(host);
        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        await CommitAsync(tree, "later.ts", "more", "a commit nobody read");
        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, []);
        await driver.Running.SettledAsync();

        var (still, started) = await JudgedCountingAsync(driver);
        Assert.Equal((OpinionGateStates.CommitsSince, 1), (still.State, still.Since));
        Assert.Empty(started);

        await CommitAsync(tree, "later2.ts", "more again", "another commit nobody read");
        var (moved, _) = await JudgedCountingAsync(driver);
        Assert.Equal((OpinionGateStates.CommitsSince, 2), (moved.State, moved.Since));
    }

    /// <summary>One look's judgment of the one owed opinion, with every git it launched.</summary>
    private static async Task<(OpinionGateState Gate, List<string> Started)> JudgedCountingAsync(Daoris.Driver.Driver driver)
    {
        var started = new List<string>();
        var judged = new List<OpinionJudged>();
        GitFlow.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        try
        {
            await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
            await driver.Running.SettledAsync();
        }
        finally
        {
            GitFlow.Started.Value = null;
        }

        return (Assert.Single(judged).Gate, started);
    }

    /// <summary>
    /// A run bound to a graph version is asked its second opinion only once its record has begun its
    /// <c>opinion</c> node; while a check its graph places before the opinion waits, the look asks nothing, and once the pass begins the opinion
    /// the next look asks it.
    /// </summary>
    [Fact]
    public async Task A_graph_runs_opinion_is_not_asked_while_a_check_before_it_waits()
    {
        var (host, _, _) = await DoneAsync(required: false, governed: false);
        var digest = RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"check","kind":"acme.check/run","version":1,"reach":"performed"},
                      {"id":"opinion","kind":"opinion","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
             "wires":[{"id":"c>k","kind":"after","from":"claimed","to":"check"},{"id":"k>o","kind":"after","from":"check","to":"opinion"},
                      {"id":"o>h","kind":"after","from":"opinion","to":"handed"}]}
            """);
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding("q1", "reports", "work", "docs", WorkflowLevels.Repository, DateTimeOffset.UtcNow)
        {
            Version = 1, Digest = digest,
        }));
        var unit = RunUnits.IdOf("q1");
        void Record(string opinion) => new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["claimed"] = new(unit, "claimed", NodeStates.Reached),
                ["check"] = new(unit, "check", NodeStates.Active),
                ["opinion"] = new(unit, "opinion", opinion),
            },
        });
        var driver = Driver(host);

        Record(NodeStates.Blocked);
        var (_, held) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        Assert.Equal(0, held);
        Assert.Empty(_passes);

        Record(NodeStates.Active);
        var (_, asked) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        Assert.Equal(1, asked);
        Assert.Single(_passes);
    }

    // ——— the machine and the fixtures

    private string Command => Path.Combine(_home, "agent-here");

    private static Provider Adapter(string name, TestAgent? toolchain) => TestDoors.Door(name, toolchain);

    /// <summary>The build's own agents by name, each present by a file look, as <c>OpinionRecheckTests</c> has them.</summary>
    /// <param name="pass">What each pass does, opened by the stand-in; null opens <c>op1</c> and raises nothing.</param>
    private Daoris.Driver.Driver Driver(OpinionStandIn host, Func<OpinionPassAsk, OpinionPassRun>? pass = null, bool drained = false)
    {
        var adapters = new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "claude-code", "claude-code-acp", "codex-acp" })
        {
            var real = ProviderRegistry.Built().AgentNamed(name)!;
            adapters[name] = Adapter(name, new TestAgent(
                Binary: [Command], VersionArguments: [], ProfileVariable: real.Home.Variable, AccountOf: real.AccountOf, ProbeByPresence: true,
                // A test double whose own home is nobody's: the look's pass is this file's subject, not the account.
                Product: real.Product, Maker: real.Maker, RunsInOwnHome: true));
        }

        var set = TestDoors.Registry(adapters);
        return new Daoris.Driver.Driver(host.Client(), drained ? InstallUpdate.Drained(Config()) : Config(), set, _home, harnesses: new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Passes = (ask, choice, _) =>
            {
                _passes.Add((ask, choice));
                if (pass is not null) return Task.FromResult(pass(ask));
                // The host opens the opinion, and the reviewer raises nothing in what it read.
                ask.Opened?.Invoke("op1", "r-op1");
                host.Opinion("op1", "s1", weights: [], tip: ask.Tip, @base: ask.Base);
                return Task.FromResult(new OpinionPassRun("completed  second opinion op1", true) { Opinion = "op1", Session = "r-op1", State = "completed" });
            },
        };
    }

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>
    /// A session that worked in its own tree on `reports` and concluded on its quest's done, owed an opinion as the conclusion makes
    /// it, under a rule naming Codex: the host standing in, the tree, and where the work left the line. Where <paramref name="governed"/>,
    /// its run follows a graph whose second opinion it has begun (<see cref="Govern"/>), since work no workflow governs is held.
    /// </summary>
    private async Task<(OpinionStandIn Host, string Tree, string Line)> DoneAsync(
        bool required, string[]? on = null, string[]? reviewers = null, bool governed = true)
    {
        if (governed) Govern("q1", required, on);
        (DriverConfig.Empty with
        {
            Actors = StaffedRoles.On("claude-code-acp"),
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["reports"] = new OpinionRule(reviewers ?? ["codex-acp"], on ?? [OpinionRules.Landing], required),
            },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        OpinionRiskFixture.ClassAccept(_home);

        var root = Path.Combine(_scratch, "reports");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, "README.md", "# reports", "first");
        var line = await HeadAsync(root);
        var tree = (await new SessionTrees(_home).OpenAsync(root, "reports", "work")).Path;
        await CommitAsync(tree, "report.ts", "the work", "the work of s1");

        var host = new OpinionStandIn().Session("s1", "completed", tree: tree).Quest("q1");
        Assert.Equal(on ?? [OpinionRules.Landing], OpinionLook.Concluded(_home, Config(), "s1",
            new QuestView("q1", "ask #a1", "reports", "Work q1", "", "Done"), "Done", "completed", tree));
        return (host, tree, line);
    }

    /// <summary>
    /// Run <paramref name="run"/> on `reports` bound to a graph whose <c>opinion</c> reads at the rule's occasions with the rule's reviewers,
    /// and its record past the opinion's start, so the look asks it as the runtime begins it.
    /// </summary>
    private void Govern(string run, bool required, string[]? on)
    {
        var options = $"{{\"required\":{(required ? "true" : "false")},\"steps\":{((on ?? []).Contains(OpinionRules.Steps) ? "true" : "false")}}}";
        var digest = RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"landing",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"opinion","kind":"opinion","version":1,"reach":"observed","options":OPTIONS},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
             "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"opinion"},{"id":"o>l","kind":"after","from":"opinion","to":"landing"}]}
            """.Replace("OPTIONS", options, StringComparison.Ordinal), workflow: "governed");
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding(run, "reports", "work", "governed", WorkflowLevels.Repository, DateTimeOffset.UtcNow)
        {
            Version = 1, Digest = digest,
        }));
        var unit = RunUnits.IdOf(run);
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["claimed"] = new(unit, "claimed", NodeStates.Reached),
                ["opinion"] = new(unit, "opinion", NodeStates.Active),
            },
        });
    }

    private static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
