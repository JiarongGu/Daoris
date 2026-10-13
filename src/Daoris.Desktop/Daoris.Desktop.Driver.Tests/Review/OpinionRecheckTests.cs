using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1e (D155 point 7; the second-agent design §6.5–§6.6): the one recheck. It is due only where the working session
/// answered a finding fixed with a commit its turn made, under a rule that keeps it; it reads the commits from the first pass's
/// tip to the working tree's tip as the turn ended, by the same reviewer walked again for its account, handed the first pass's
/// findings and the answers; it runs once; and its findings go to the person, never back to the working session.
/// </summary>
/// <remarks>
/// The host is <see cref="OpinionStandIn"/> and git <see cref="StandInGit"/>, both in process; each agent is present by a file
/// look on a command this test writes (<see cref="HarnessToolchain.ProbeByPresence"/>), as <c>ReviewerChoiceTests</c> has it;
/// and the pass itself is handed in (<see cref="Daoris.Driver.Driver.Passes"/>), since a real one spawns an agent. The fast half.
/// </remarks>
public sealed class OpinionRecheckTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-recheck-" + Guid.NewGuid().ToString("N")[..8]);

    public OpinionRecheckTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>The build's own agents by name, present by a file look, or absent where <paramref name="absent"/> names them.</summary>
    private (AdapterSet Adapters, HarnessRoster Roster) Machine(params string[] absent)
    {
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "claude-code", "claude-code-acp", "codex-acp" })
        {
            var real = AdapterSet.Built().Resolve(name).Toolchain!;
            adapters[name] = new Adapter(name, new HarnessToolchain(
                Binary: [absent.Contains(name) ? "daoris-xagent1e-no-such-command" : Command], VersionArguments: [],
                ProfileVariable: real.ProfileVariable, AccountOf: real.AccountOf, ProbeByPresence: true, Product: real.Product, Maker: real.Maker));
        }

        var set = new AdapterSet(adapters);
        return (set, new HarnessRoster(set, Path.Combine(_home, "harnesses.json")));
    }

    private static readonly OpinionRule Rule = new(["codex-acp"], [OpinionRules.Landing]);

    private static readonly OpinionPassAsk Asked =
        new(OpinionRules.Landing, "s1", "reports", "D:/checkouts/reports", OpinionStandIn.Base, OpinionStandIn.Tip, Rule) { Workspace = "work" };

    /// <summary>The passes the driver asked, with the choice each was asked with.</summary>
    private readonly List<(OpinionPassAsk Ask, ReviewerChoice Choice)> _passes = [];

    private Daoris.Driver.Driver Driver(OpinionStandIn host, StandInGit git, params string[] absent)
    {
        var (adapters, roster) = Machine(absent);
        return new Daoris.Driver.Driver(host.Client(), DriverConfig.Empty with { Adapter = "claude-code-acp" }, adapters, _home, harnesses: roster)
        {
            OpinionGit = git.Read,
            Passes = (ask, choice, _) =>
            {
                _passes.Add((ask, choice));
                if (!choice.Chosen) return Task.FromResult(OpinionPassRun.Unavailable(choice));
                // The recheck the host opened, given: one `must` of its own, and the first pass's finding 1 withdrawn.
                host.Opinion("op2", "s1", weights: ["must"], pass: "recheck", rechecks: "op1", tip: ask.Tip, @base: ask.Base);
                host.Rechecked("op2", 1, "withdrawn");
                return Task.FromResult(new OpinionPassRun("completed  second opinion op2", true) { Opinion = "op2", Session = "r-op2", State = "completed" });
            },
        };
    }

    /// <summary>
    /// A first pass on the work up to the tip, its findings handed to s1, whose turn then ended with the words taken: the tree's
    /// history holds the tip, a fix after it, and a note after that, which is the tree's tip now.
    /// </summary>
    private (OpinionStandIn Host, StandInGit Git, string Tip, string Fix, string Head) Answered(params string[] weights)
    {
        var tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(tree);
        var git = new StandInGit(tree);
        git.Commit("base");
        var tip = git.Commit("the work read", ("src/report.ts", "v1"));
        var fix = git.Commit("the fix", ("src/report.ts", "v2"));
        var head = git.Commit("a note", ("docs/notes.md", "n"));

        var host = new OpinionStandIn().Session("s1", "completed", tree: tree, adapter: "claude-code-acp")
            .Opinion("op1", "s1", weights: weights.Length == 0 ? ["must", "should"] : weights, tip: tip);
        return (host, git, tip, fix, head);
    }

    private static OpinionPassAsk AskedAt(string tip) => Asked with { Tip = tip };

    // ——— When it is due (design §6.5).

    private static OpinionView First(string pass = "first") =>
        new("op1", "landing", pass, "s1", "r-op1", "reports", OpinionStandIn.Base, OpinionStandIn.Tip, "codex-acp", "another-maker", "given")
        {
            Findings = [new OpinionFindingView(1, "must", "src/report.ts:3", "claim", "consequence", "reproduce", "sure")],
        };

    private static OpinionReading Read(string counts, string? tip = "3333333333333333333333333333333333333333") =>
        new("s1", tip, DateTimeOffset.UnixEpoch, [new OpinionAnswerRead(1, "must", counts) { Said = counts }]);

    /// <summary>
    /// 🔴 One recheck, only where a finding was answered fixed with a commit the turn made (design §6.5), under a rule that keeps
    /// it; never of a recheck, and never before the working session's answers are read.
    /// </summary>
    [Fact]
    public void A_recheck_is_due_only_where_a_finding_was_answered_fixed()
    {
        Assert.Null(OpinionRechecks.WhyNot(First(), Read("fixed"), Rule));
        Assert.Equal(OpinionRechecks.NoFix, OpinionRechecks.WhyNot(First(), Read("rejected"), Rule));
        Assert.Equal(OpinionRechecks.NoFix, OpinionRechecks.WhyNot(First(), Read("unresolved"), Rule));
        Assert.Equal(OpinionRechecks.NoFix, OpinionRechecks.WhyNot(First(), Read("fixed", tip: null), Rule));
        Assert.Equal(OpinionRechecks.NotAnswered, OpinionRechecks.WhyNot(First(), null, Rule));
        Assert.Equal(OpinionRechecks.Off, OpinionRechecks.WhyNot(First(), Read("fixed"), Rule with { Recheck = false }));
        Assert.Equal(OpinionRechecks.NotFirst, OpinionRechecks.WhyNot(First("recheck"), Read("fixed"), Rule));
    }

    /// <summary>
    /// 🔴 The recheck reads the commits from the first pass's tip to the working tree's tip as the turn ended, by the same
    /// reviewer, walked again for its account; handed the first pass's findings and the answers as read, each fix's commit
    /// checked. It runs once. Its findings go to the person and are never handed to the working session.
    /// </summary>
    [Fact]
    public async Task The_one_recheck_reads_the_commits_since_by_the_same_reviewer_and_goes_to_the_person()
    {
        var (host, git, tip, fix, head) = Answered();
        var driver = Driver(host, git);
        Assert.Equal(OpinionDeliveryStates.Handed, (await driver.DeliverAsync("op1", AskedAt(tip))).State);
        host.Take("s1");
        host.Answer("op1", 1, "fixed", commit: fix);
        host.Answer("op1", 2, "rejected", evidence: "the twin test holds the table");

        var delivered = await driver.DeliverAsync("op1", AskedAt(tip));

        var (ask, choice) = Assert.Single(_passes);
        Assert.Equal((OpinionRules.Landing, "s1", tip, head), (ask.Occasion, ask.Working, ask.Base, ask.Tip));
        Assert.Equal(("op1", "reports", "D:/checkouts/reports", "work"), (ask.Rechecks!.First.Id, ask.Repository, ask.Root, ask.Workspace));
        Assert.Equal([("fixed", (string?)fix), ("rejected", null)], ask.Rechecks.Answers.Findings.Select(row => (row.Counts, row.Fix)));
        Assert.Equal(("codex-acp", ReviewerLabels.AnotherMaker), (choice.Reviewer, choice.Label));

        Assert.Equal(OpinionDeliveryStates.Answered, delivered.State);
        Assert.Equal("op2", delivered.Recheck!.Opinion);
        Assert.Contains("Recheck op2 reads the commits since.", delivered.Line);
        Assert.Equal("op2", new OpinionDeliveries(_home).Read("op1").Recheck);

        // What waits for the person: the recheck's own `must`; the first pass's `must` was fixed, so it waits for nothing.
        Assert.Equal("first [] raised [1]", Disputes(delivered.Disputes!));

        // Once: the look after asks no second recheck.
        var again = await driver.DeliverAsync("op1", AskedAt(tip));
        Assert.Single(_passes);
        Assert.Null(again.Recheck);
        Assert.Equal("first [] raised [1]", Disputes(again.Disputes!));

        // The recheck's findings go to the person, never handed back to the working session.
        var recheck = await driver.DeliverAsync("op2", AskedAt(tip));
        Assert.Equal((OpinionDeliveryStates.ToPerson, OpinionToPerson.Recheck), (recheck.State, recheck.Why));
        Assert.Equal(["op1"], host.Hands);
        Assert.Empty(host.Said("s1"));
    }

    /// <summary>Nothing fixed, nothing new to read: no recheck is asked, and why is kept, so no later look asks one either.</summary>
    [Fact]
    public async Task No_recheck_is_asked_where_nothing_was_fixed()
    {
        var (host, git, tip, _, _) = Answered("must");
        var driver = Driver(host, git);
        await driver.DeliverAsync("op1", AskedAt(tip));
        host.Take("s1");
        host.Answer("op1", 1, "rejected", evidence: "the check shows it holds");

        var delivered = await driver.DeliverAsync("op1", AskedAt(tip));

        Assert.Empty(_passes);
        Assert.Equal(OpinionRechecks.NoFix, new OpinionDeliveries(_home).Read("op1").RecheckWhy);
        Assert.Contains("No recheck: no finding was answered fixed", delivered.Line);
        // The rejected `must` waits for the person: two agents disagreeing establish nothing (design §6.6).
        Assert.Equal([1], delivered.Disputes!.First);
    }

    /// <summary>
    /// A fix whose commit git does not read as one the turn made is no fix (design §6.4): it reads as unresolved, asks no
    /// recheck, and its <c>must</c> waits for the person.
    /// </summary>
    [Fact]
    public async Task A_fix_whose_commit_the_turn_did_not_make_asks_no_recheck()
    {
        var (host, git, tip, _, _) = Answered("must");
        var driver = Driver(host, git);
        await driver.DeliverAsync("op1", AskedAt(tip));
        host.Take("s1");
        host.Answer("op1", 1, "fixed", commit: tip);

        var delivered = await driver.DeliverAsync("op1", AskedAt(tip));

        Assert.Empty(_passes);
        Assert.Equal((OpinionViews.Unresolved, OpinionAnswerWhy.FixNotAfter), (delivered.Reading!.Findings[0].Counts, delivered.Reading.Findings[0].Why));
        Assert.Equal([1], delivered.Disputes!.First);
    }

    /// <summary>
    /// The reviewer cannot run again (§3.3): the recheck is unavailable with the choice's code, kept so no later look tries it,
    /// and the first pass's disputes stand for the person.
    /// </summary>
    [Fact]
    public async Task A_recheck_nobody_can_read_says_why_and_is_not_asked_again()
    {
        var (host, git, tip, fix, _) = Answered("must", "must");
        var driver = Driver(host, git, "codex-acp");
        await driver.DeliverAsync("op1", AskedAt(tip));
        host.Take("s1");
        host.Answer("op1", 1, "fixed", commit: fix);

        var delivered = await driver.DeliverAsync("op1", AskedAt(tip));
        await driver.DeliverAsync("op1", AskedAt(tip));

        var (_, choice) = Assert.Single(_passes);
        Assert.False(choice.Chosen);
        Assert.Equal(ReviewerUnavailable.NoReviewer, new OpinionDeliveries(_home).Read("op1").RecheckWhy);
        Assert.Null(delivered.Recheck!.Opinion);
        Assert.Contains("No recheck: no reviewer could read it again (no-reviewer).", (await driver.DeliverAsync("op1", AskedAt(tip))).Line);
        Assert.Equal([2], delivered.Disputes!.First);
    }

    // ——— What waits for the person (design §6.6).

    /// <summary>
    /// 🔴 A <c>must</c> the working session rejected or left unresolved is disputed unless the recheck withdrew it, and every
    /// <c>must</c> the recheck raised is; a fixed one, a <c>should</c> and a <c>note</c> wait for nothing. Agreement between the
    /// two agents establishes nothing: only the recheck's own word lifts a dispute, and only the person answers one.
    /// </summary>
    [Fact]
    public void What_waits_for_the_person_after_the_recheck()
    {
        var reading = new OpinionReading("s1", OpinionStandIn.Tip, DateTimeOffset.UnixEpoch,
        [
            new OpinionAnswerRead(1, "must", "rejected"),
            new OpinionAnswerRead(2, "must", "unresolved") { Why = OpinionAnswerWhy.NotAnswered },
            new OpinionAnswerRead(3, "must", "fixed") { Fix = OpinionStandIn.Tip },
            new OpinionAnswerRead(4, "should", "rejected"),
            new OpinionAnswerRead(5, "must", "unresolved"),
        ]);
        var recheck = First("recheck") with
        {
            Findings =
            [
                new OpinionFindingView(1, "must", "src/a.ts:1", "c", "c", "r", "sure"),
                new OpinionFindingView(2, "note", "general", "c", "c", "r", "unsure"),
            ],
            Rechecked = new Dictionary<int, string> { [1] = "withdrawn", [2] = "stands", [3] = "stands" },
        };

        Assert.Equal("first [1,2,5] raised []", Disputes(OpinionDisputes.Of(reading, null)));
        Assert.Equal("first [2,5] raised [1]", Disputes(OpinionDisputes.Of(reading, recheck)));
        // A recheck still reading has said nothing, so it lifts nothing and raises nothing.
        Assert.Equal("first [1,2,5] raised []", Disputes(OpinionDisputes.Of(reading, recheck with { State = "reading", Findings = null })));
        Assert.Equal(3, OpinionDisputes.Of(reading, recheck).Count);
    }

    /// <summary>The disputes as text, first pass's then the recheck's, so a comparison reads their numbers rather than the lists.</summary>
    private static string Disputes(OpinionDisputes disputes) => $"first [{string.Join(",", disputes.First)}] raised [{string.Join(",", disputes.Raised)}]";
}
