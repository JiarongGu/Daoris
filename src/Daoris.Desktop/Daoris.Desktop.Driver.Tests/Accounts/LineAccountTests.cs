using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.AccountRotationWalkTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1a-i (the foundation design §3.2 and row a-i): the account rule as one call, <see cref="LineAccount.Decide"/>, over what
/// LIMITSWITCH1 and LIMITSWITCH2 built. Every case is the line's own account (ready, near, cooling, signed out, refused, missing, outside,
/// kept, not offered) × the person's new session and a limit's end × the others' states × the line's kind, run through the roster's resume
/// and through the rule over a walk handed lazily, and the two must agree in <b>the answer, how many walks were made and how many starts
/// were counted</b> (X4-1.1, O4-4). A second check pins each case to what today's callers answered when the rule was written, the table
/// <c>line-account-cases.txt</c> beside this file: it holds the callers moved onto the rule to the answers they gave.
/// </summary>
/// <remarks>
/// Nothing here starts a process, a window or a real console: the agent is present by a file look on a command this test writes, as in
/// <see cref="ResumeAccountTests"/>, and every case has its own scratch home under <c>_fixtures/</c>; no case sets <c>DAORIS_HOME</c>.
/// </remarks>
public sealed class LineAccountTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 3, 15, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "line-account", Guid.NewGuid().ToString("N")[..8]);

    public LineAccountTests() => Directory.CreateDirectory(_root);

    public void Dispose() => GitSeed.Discard(_root);

    // ——— The cases.

    public enum OwnState { Ready, Near, Cooling, SignedOut, Refused, Missing, MissingCooling, Outside, Kept, NotOffered, NoToolchain }

    public enum Others { Ready, OtherAgent, NoneSignedOut, CoolingAfter, CoolingBefore }

    public enum Trigger { None, NewSession, Limited, Both }

    public enum Kind { DrivenOpen, DrivenClosedCarries, DrivenClosed, DrivenLimit, ConversationCarries, AskDaoris }

    public sealed record Case(OwnState Own, Others Others, Trigger Trigger, Kind Kind)
    {
        public string Key => $"{Own}/{Others}/{Trigger}/{Kind}";

        public string Group => $"{Own}/{Trigger}/{Kind}";

        public LineKind Line => Kind switch
        {
            Kind.DrivenOpen => LineKind.DrivenOpen,
            Kind.DrivenClosedCarries => LineKind.DrivenClosed(carries: true),
            Kind.DrivenClosed => LineKind.DrivenClosed(carries: false),
            Kind.DrivenLimit => LineKind.DrivenLimit,
            Kind.ConversationCarries => LineKind.Conversation(carries: true),
            _ => LineKind.AskDaoris,
        };

        public StartKind Start => Kind is Kind.ConversationCarries or Kind.AskDaoris ? StartKind.Conversation : StartKind.Driven;

        public bool NewSession => Trigger is Trigger.NewSession or Trigger.Both;

        public bool Limited => Trigger is Trigger.Limited or Trigger.Both;

        public string Adapter => Own == OwnState.NoToolchain ? "plain" : "fake";

        public string Account => Own switch
        {
            OwnState.Missing or OwnState.MissingCooling => "account-9",
            OwnState.Outside => "account-4",
            _ => "account-1",
        };

        public string? Model => Own == OwnState.NotOffered ? "opus" : null;
    }

    /// <summary>
    /// Every case of the grid: each own state with the others' states that can change its answer, every trigger and every kind. Near is
    /// never what the roster's judgement says, and no account is outside the set (POOL1a2, D197 point 1), so those two are the rule's alone.
    /// </summary>
    public static IEnumerable<Case> Grid()
    {
        foreach (var own in Enum.GetValues<OwnState>().Where(own => own is not (OwnState.Near or OwnState.Outside)))
        foreach (var trigger in Enum.GetValues<Trigger>())
        foreach (var kind in Enum.GetValues<Kind>())
        foreach (var others in Enum.GetValues<Others>().Where(others => own != OwnState.NoToolchain || others == Others.Ready))
        {
            yield return new Case(own, others, trigger, kind);
        }
    }

    /// <summary>What a resume answered, as far as its callers read it.</summary>
    public sealed record Answered(
        NextHold Own, bool Allowed, string? Profile, string? Cooling, bool NewSession, bool Limited, bool Switched, bool Waits, bool NotOffered)
    {
        public override string ToString() =>
            $"{Own} {(Allowed ? "allowed" : "refused")} profile={Profile ?? "-"} cooling={Cooling ?? "-"} "
            + $"flags={(NewSession ? "N" : "")}{(Limited ? "L" : "")}{(Switched ? "S" : "")}{(Waits ? "W" : "")}{(NotOffered ? "X" : "")}";
    }

    /// <summary>The answer, the walks asked and the starts counted; <paramref name="Text"/> is what the answer says, hashed.</summary>
    public sealed record Outcome(Answered Answer, long Walks, long Starts, string Text = "-", string Rule = "-")
    {
        /// <summary>What a caller moved onto the rule must still give: the answer, its words, the walks and the starts.</summary>
        public string Today => $"{Answer} walks={Walks} starts={Starts} text={Text}";

        /// <summary>What the rule gives: the answer's shape, the walks it asks and the starts counted.</summary>
        public string Shaped => $"{Answer} walks={Walks} starts={Starts} rule={Rule}";
    }

    // ——— Today: the roster's resume.

    private static string Hash(string? text) =>
        text is null ? "-" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8].ToLowerInvariant();

    private static string CoolingOf(CoolingEntry? cooling) =>
        cooling is null ? "-" : $"{cooling.Account ?? "own"}+{(cooling.Until - Seen).TotalHours:0.##}h";

    private static string TextOf(ResumeChoice resume) => Hash(
        string.Join(
            "|", resume.Selection.Refusal, resume.Waiting?.Note, string.Join(",", resume.Waiting?.Parts.Select(part => part.Code) ?? []),
            resume.Selection.RefusalNote?.Note, resume.Elsewhere?.Code, resume.Elsewhere?.Detail?.Note, resume.Selection.Profile));

    private async Task<Outcome> Today(Case each)
    {
        var (roster, _) = Build(each);
        var (selected, marked) = (roster.Selected, roster.Mark());
        var resume = await roster.ResumeAsync(
            each.Adapter, Config, null, each.Account, each.Start, each.NewSession, each.Line, limited: each.Limited, model: each.Model);
        return new Outcome(
            new Answered(
                resume.Own, resume.Selection.Allowed, resume.Selection.Profile, CoolingOf(resume.Selection.Cooling), resume.NewSession, resume.Limited,
                resume.Switched, resume.Waits, resume.NotOffered),
            roster.Selected - selected, roster.Mark() - marked, TextOf(resume));
    }

    // ——— The rule: the same case read by LineAccount.Decide, its walk handed lazily to a roster that counts what it is asked.

    private async Task<Outcome> Rule(Case each)
    {
        var (roster, home) = Build(each);
        var (marked, asked) = (roster.Mark(), 0L);
        var trigger = new LineTrigger(each.NewSession, each.Limited);

        Task<HarnessSelection> Walk(LineAsk ask)
        {
            asked++;
            return ask.Place switch
            {
                LineWalkPlace.NoToolchain => roster.SelectAsync(each.Adapter, Config, null, null, each.Start),
                LineWalkPlace.Switch => roster.SelectAsync("fake", Config, null, null, each.Start, default, each.Model, counts: ask.Counts),
                LineWalkPlace.Elsewhere => roster.SelectAsync(
                    "fake", Config, null, null, each.Start, default, each.Model, ask.NotOffered ? [each.Account] : null),
                _ => throw new InvalidOperationException($"a resume's rule asks no {ask.Place} walk"),
            };
        }

        var own = OwnOf(each, roster, home);
        var answer = await LineAccount.DecideAsync(own, trigger, LineConstraint.None, each.Line, Walk);
        HarnessSelection? named = null;
        if (answer is LineAnswer.Resume { Walked: null })
        {
            // The own account ready is asked for by name, as a pick is: the roster's own step beside the rule, counted as a start.
            named = await roster.SelectAsync("fake", Config, null, each.Account, each.Start);
            if (named is { Allowed: false, NotReady: AccountReadiness.SignedOut })
            {
                answer = await LineAccount.DecideAsync(own with { Hold = NextHold.SignedOut, NotOffered = false }, trigger, LineConstraint.None, each.Line, Walk);
                named = null;
            }
        }

        return new Outcome(Shape(answer, each.NewSession, each.Limited, named), asked, roster.Mark() - marked, Rule: RuleKind(answer));
    }

    /// <summary>How the rule's answer reads as the roster's resume does: the same fields, composed by the shape of the answer alone.</summary>
    private static Answered Shape(LineAnswer answer, bool newSession, bool limited, HarnessSelection? named)
    {
        bool NewSession(NextHold why) => newSession && why == NextHold.Cooling;
        return answer switch
        {
            LineAnswer.Resume { Walked: { } walked } => new(NextHold.Ready, walked.Allowed, walked.Profile, CoolingOf(walked.Cooling), false, false, false, false, false),
            LineAnswer.Resume => new(NextHold.Ready, named!.Allowed, named.Profile, CoolingOf(named.Cooling), false, false, false, false, false),
            LineAnswer.Elsewhere { Switched: true } e => new(e.Why, true, e.Selection.Profile, "-", false, false, true, false, false),
            LineAnswer.Elsewhere e => new(e.Why, true, e.Selection.Profile, "-", NewSession(e.Why), limited, false, false, e.NotOffered),
            LineAnswer.Wait { Set: ResetSet.Switch } w => new(w.Why, false, null, CoolingOf(w.Earliest), false, false, false, true, false),
            LineAnswer.Wait w => new(w.Why, false, null, CoolingOf(w.Earliest), NewSession(w.Why), limited, false, false, w.NotOffered),
            LineAnswer.Hold h => new(h.Why, false, null, h.Walked is null ? CoolingOf(h.Cooling) : "-", NewSession(h.Why), limited, false, false, h.NotOffered),
            _ => throw new InvalidOperationException("an answer the rule does not give"),
        };
    }

    private static string RuleKind(LineAnswer answer) => answer switch
    {
        LineAnswer.Resume { Walked: null } => "Resume",
        LineAnswer.Resume => "Resume.Walked",
        LineAnswer.Elsewhere { Switched: true } => "Elsewhere.Switched",
        LineAnswer.Elsewhere => "Elsewhere",
        LineAnswer.Wait { Set: ResetSet.Switch, OwnFirst: true } => "Wait.Switch.Own",
        LineAnswer.Wait { Set: ResetSet.Switch } => "Wait.Switch.Walked",
        LineAnswer.Wait => "Wait.Walk",
        LineAnswer.Hold h => $"Hold.{h.Repair}",
        _ => "?",
    };

    /// <summary>The own account as the roster reads it before any probe: the pure judgement over what the case set up.</summary>
    private LineOwn OwnOf(Case each, HarnessRoster roster, string home)
    {
        if (each.Own == OwnState.NoToolchain) return LineOwn.Without;

        // The set is every account on the machine (POOL1a2): the roster reads it folded, so the judgement does.
        var scope = roster.Settings.Set("fake");
        var present = HarnessSettings.Profiles(home, "fake");
        var cooling = each.Own is OwnState.Cooling or OwnState.MissingCooling
            ? new CoolingEntry("fake", each.Account, Seen.AddHours(2), true, "session", Seen.AddMinutes(-5), "s1")
            : null;
        var readiness = each.Own switch
        {
            OwnState.Cooling or OwnState.MissingCooling => AccountReadiness.Cooling,
            OwnState.Refused => AccountReadiness.Refused,
            _ => AccountReadiness.Ready,
        };
        var hold = ResumeAccount.Judge(each.Account, scope, each.Start, new AccountState(each.Account, readiness, cooling), present);
        if (each.Own == OwnState.Near) hold = NextHold.Near;
        return new LineOwn(hold, each.Own == OwnState.NotOffered && hold == NextHold.Ready, cooling);
    }

    // ——— A case's home.

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Pipe;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private static int _built;

    /// <summary>A roster on a fresh home set up as the case says: the accounts, the set, the others' states and the own account's.</summary>
    private (HarnessRoster Roster, string Home) Build(Case each)
    {
        var home = Path.Combine(_root, $"case-{Interlocked.Increment(ref _built)}");
        Directory.CreateDirectory(home);
        var command = Path.Combine(home, "agent-here");
        File.WriteAllText(command, "");
        var settings = DaorisHome.Entry(home, HomeEntry.Harnesses);
        foreach (var name in new[] { "account-1", "account-2", "account-3" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(home, "fake", name));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(home, "elsewhere", "account-1"));
        var written = new HarnessSettings().WithRotation("fake", ["account-1", "account-2", "account-3"]);
        if (each.Own == OwnState.Kept) written = written.WithUse("fake", new UseChange(Keep: "account-1"));
        written.Save(settings);

        void Cool(string account, double hours) =>
            AccountCooling.Cool(home, new CoolingEntry("fake", account, Seen.AddHours(hours), true, "session", Seen.AddMinutes(-5), "s1"), Seen);

        var roster = new HarnessRoster(
            new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
            {
                ["fake"] = new Adapter("fake", new HarnessToolchain(Binary: [command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
                ["elsewhere"] = new Adapter("elsewhere", new HarnessToolchain(Binary: [command], VersionArguments: ["--version"], ProfileVariable: "ELSEWHERE_HOME", ProbeByPresence: true)),
                ["plain"] = new Adapter("plain", null),
            }), settings)
        {
            Clock = () => Seen,
            Zone = TimeZoneInfo.Utc,
        };

        switch (each.Others)
        {
            case Others.OtherAgent:
                // Another agent's account is ready; the roster reads this agent's accounts alone.
                Cool("account-2", 40);
                Cool("account-3", 41);
                break;
            case Others.NoneSignedOut:
                AccountReads.Keep(home, "fake", "account-2", LoginState.Out, Seen);
                AccountReads.Keep(home, "fake", "account-3", LoginState.Out, Seen);
                break;
            case Others.CoolingAfter:
                Cool("account-2", 5);
                Cool("account-3", 9);
                break;
            case Others.CoolingBefore:
                Cool("account-2", 1);
                Cool("account-3", 9);
                break;
        }

        switch (each.Own)
        {
            case OwnState.Cooling or OwnState.MissingCooling:
                Cool(each.Account, 2);
                break;
            case OwnState.SignedOut:
                AccountReads.Keep(home, "fake", each.Account, LoginState.Out, Seen);
                break;
            case OwnState.Refused:
                roster.Refuse("fake", each.Account, "its provider refused it");
                break;
            case OwnState.NotOffered:
                AgentOffers.Refused(home, "fake", null, each.Account, "opus", Seen);
                break;
        }

        return (roster, home);
    }

    // ——— The proof: the rule against the roster's resume as it stands.

    /// <summary>
    /// Every case of the grid, the roster's resume against the rule: the same answer, the same number of walks, the same number of
    /// starts counted. A walk the rule asks where today's code makes none, or one it does not ask where today's does, fails here.
    /// </summary>
    [Fact]
    public async Task The_rule_agrees_with_the_roster_s_resume_on_every_case()
    {
        var failures = new List<string>();
        var cases = 0;
        foreach (var each in Grid())
        {
            cases++;
            var today = await Today(each);
            var rule = await Rule(each);
            if (today.Answer != rule.Answer || today.Walks != rule.Walks || today.Starts != rule.Starts)
            {
                failures.Add($"{each.Key}\n    today {today.Today}\n    rule  {rule.Shaped}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {cases} cases disagree:\n" + string.Join("\n", failures.Take(25)));
    }

    // ——— The proof: each case against what today's callers answered, in the table beside this file.

    private static string TablePath => Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "Accounts", "line-account-cases.txt");

    /// <summary>
    /// The table: the distinct rows, each named, then a line for each own state, trigger and kind naming the row of each of the others'
    /// states in order (<see cref="Others"/>; one name where they are all the same). A row is the answer's fields, the walks asked, the
    /// starts counted, a hash of what it said, and the shape of the rule's answer.
    /// </summary>
    private static Dictionary<string, string> Table()
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        var others = Enum.GetNames<Others>();
        foreach (var line in File.ReadAllLines(TablePath))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var (head, rest) = (line[..space], line[(space + 1)..]);
            if (head.StartsWith("row-", StringComparison.Ordinal))
            {
                named[head] = rest;
                continue;
            }

            var ids = rest.Split(' ');
            for (var at = 0; at < others.Length; at++) rows[$"{head}|{others[at]}"] = named[ids[Math.Min(at, ids.Length - 1)]];
        }

        return rows;
    }

    private static string Expected(Dictionary<string, string> table, Case each) =>
        table.GetValueOrDefault($"{each.Group}|{each.Others}", "no row");

    /// <summary>
    /// Today's callers' answers, case by case, kept to the byte: the answer's fields, its words (hashed), how many walks and how many
    /// starts. This is the proof that moving the callers onto the rule changed nothing: the table was written from the roster's resume
    /// before any caller moved, and every case in it still answers so.
    /// </summary>
    [Fact]
    public async Task The_roster_s_resume_gives_every_case_the_answer_it_gave_before_the_rule()
    {
        var table = Table();
        var failures = new List<string>();
        foreach (var each in Grid())
        {
            var today = await Today(each);
            var expected = Expected(table, each);
            if (!expected.StartsWith(today.Today, StringComparison.Ordinal)) failures.Add($"{each.Key}\n    expected {expected}\n    today    {today.Today}");
        }

        Assert.True(failures.Count == 0, $"{failures.Count} cases changed:\n" + string.Join("\n", failures.Take(25)));
    }

    /// <summary>The rule gives every case the shape and the counts the table keeps for it.</summary>
    [Fact]
    public async Task The_rule_gives_every_case_the_shape_the_table_keeps()
    {
        var table = Table();
        var failures = new List<string>();
        foreach (var each in Grid())
        {
            var rule = await Rule(each);
            var expected = Expected(table, each);
            if (Regex.Replace(expected, @" text=\S+", "") != rule.Shaped) failures.Add($"{each.Key}\n    expected {expected}\n    rule     {rule.Shaped}");
        }

        Assert.True(failures.Count == 0, $"{failures.Count} cases changed:\n" + string.Join("\n", failures.Take(25)));
    }

    // ——— The constraint: a handed quest's named account and a hand-over press's pick.

    /// <summary>What a start asked for the account by its constraint answered: allowed, its profile, its cool-off, and the starts counted.</summary>
    private sealed record Bound(bool Allowed, string? Profile, string Cooling, long Starts, string? Passed)
    {
        public override string ToString() => $"{(Allowed ? "allowed" : "refused")} profile={Profile ?? "-"} cooling={Cooling} starts={Starts} passed={Passed ?? "-"}";
    }

    private static readonly OwnState[] Bounds = [OwnState.Ready, OwnState.Cooling, OwnState.Refused, OwnState.SignedOut];

    private async Task<Bound> BoundToday(Case each, bool handed)
    {
        var (roster, _) = Build(each);
        var marked = roster.Mark();
        if (!handed)
        {
            var picked = await roster.SelectAsync("fake", Config, null, "account-1", StartKind.Driven);
            return new Bound(picked.Allowed, picked.Profile, CoolingOf(picked.Cooling), roster.Mark() - marked, null);
        }

        var (selection, passed) = await roster.SelectHandedAsync("fake", Config, null, "account-1", StartKind.Driven);
        return new Bound(selection.Allowed, selection.Profile, CoolingOf(selection.Cooling), roster.Mark() - marked, selection.Allowed ? passed : null);
    }

    private async Task<(Bound Bound, long Asked)> BoundRule(Case each, bool handed)
    {
        var (roster, home) = Build(each);
        var (marked, asked) = (roster.Mark(), 0L);
        string? passed = null;

        async Task<HarnessSelection> Walk(LineAsk ask)
        {
            asked++;
            Assert.Equal(LineWalkPlace.Handed, ask.Place);
            var (selection, said) = await roster.SelectHandedAsync("fake", Config, null, ask.Named, StartKind.Driven);
            passed = said;
            return selection;
        }

        var constraint = handed ? LineConstraint.Handed("account-1") : LineConstraint.Pick("account-1");
        var own = OwnOf(each, roster, home);
        var trigger = new LineTrigger();
        var answer = await LineAccount.DecideAsync(own, trigger, constraint, LineKind.DrivenOpen, Walk);
        HarnessSelection? named = null;
        if (answer is LineAnswer.Resume { Walked: null })
        {
            named = await roster.SelectAsync("fake", Config, null, "account-1", StartKind.Driven);
            if (named is { Allowed: false, NotReady: AccountReadiness.SignedOut })
            {
                answer = await LineAccount.DecideAsync(own with { Hold = NextHold.SignedOut }, trigger, constraint, LineKind.DrivenOpen, Walk);
                named = null;
            }
        }

        var starts = roster.Mark() - marked;
        return (answer switch
        {
            LineAnswer.Resume => new Bound(named!.Allowed, named.Profile, CoolingOf(named.Cooling), starts, null),
            LineAnswer.Elsewhere e => new Bound(true, e.Selection.Profile, "-", starts, e.Passed),
            LineAnswer.Wait w => new Bound(false, null, CoolingOf(w.Earliest), starts, null),
            _ => new Bound(false, null, "-", starts, null),
        }, asked);
    }

    /// <summary>
    /// A pick is the one account, never moved; a handed quest's named account comes first and the walk goes on within the same agent's.
    /// The rule answers each as <c>SelectAsync</c> with a choice and <c>SelectHandedAsync</c> do, counting the same starts, and asks the
    /// walk only where the named account cannot run (a pick never).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_constraint_answers_as_the_roster_s_start_does(bool handed)
    {
        var failures = new List<string>();
        foreach (var own in Bounds)
        foreach (var others in Enum.GetValues<Others>())
        {
            var each = new Case(own, others, Trigger.None, Kind.DrivenOpen);
            var today = await BoundToday(each, handed);
            var (rule, asked) = await BoundRule(each, handed);
            var walks = handed && own is not OwnState.Ready ? 1 : 0;
            if (today != rule || asked != walks) failures.Add($"{each.Key}\n    today {today}\n    rule  {rule}, walks asked {asked} (wanted {walks})");
        }

        Assert.True(failures.Count == 0, $"{failures.Count} cases disagree:\n" + string.Join("\n", failures));
    }

    // ——— The rule alone.

    private static HarnessSelection Pick { get; } = new(Refusal: null, Profile: "account-2");

    /// <summary>
    /// No account is outside the set in a roster (POOL1a2, D197 point 1) and none is near for a resume, so the roster never gives the rule
    /// these two; the rule reads an outside one as it reads any that cannot carry the words, and a near one as a ready one.
    /// </summary>
    [Fact]
    public void An_outside_account_is_held_as_any_that_cannot_carry_and_a_near_one_as_ready()
    {
        foreach (var line in new[] { LineKind.DrivenOpen, LineKind.DrivenClosed(false), LineKind.DrivenLimit, LineKind.Conversation(true), LineKind.AskDaoris })
        foreach (var trigger in new[] { default, new LineTrigger(NewSession: true), new LineTrigger(Limited: true) })
        {
            var asked = new List<LineAsk>();
            var outside = LineAccount.Decide(new LineOwn(NextHold.Outside), trigger, LineConstraint.None, line, ask => { asked.Add(ask); return Pick; });
            if (line.Walks)
            {
                Assert.Equal(new LineAsk(LineWalkPlace.Elsewhere, Counts: true), Assert.Single(asked));
                Assert.Equal(new LineAnswer.Elsewhere(Pick, NextHold.Outside), outside);
            }
            else
            {
                Assert.Empty(asked);
                Assert.Equal(new LineAnswer.Hold(NextHold.Outside, LineRepair.ChangeSet), outside);
            }

            asked.Clear();
            var near = LineAccount.Decide(new LineOwn(NextHold.Near), trigger, LineConstraint.None, line, ask => { asked.Add(ask); return Pick; });
            Assert.Equal(new LineAnswer.Resume(), near);
            Assert.Empty(asked);
        }
    }

    /// <summary>
    /// The walk is a lazy input (X4-1.1, O4-4): asked at most once, and only at the places today's resume walks. Every own state, trigger and
    /// kind is judged for where it asks, apart from any roster.
    /// </summary>
    [Fact]
    public void The_walk_is_asked_only_where_today_s_resume_walks()
    {
        var cooling = new CoolingEntry("fake", "account-1", Seen.AddHours(2), true, "session", Seen.AddMinutes(-5), "s1");
        foreach (var hold in Enum.GetValues<NextHold>())
        foreach (var notOffered in new[] { false, true })
        foreach (var trigger in new[] { default, new LineTrigger(NewSession: true), new LineTrigger(Limited: true), new LineTrigger(true, true) })
        foreach (var line in new[] { LineKind.DrivenOpen, LineKind.DrivenClosed(true), LineKind.DrivenClosed(false), LineKind.DrivenLimit, LineKind.Conversation(true), LineKind.AskDaoris })
        {
            var own = new LineOwn(hold, notOffered, hold == NextHold.Cooling ? cooling : null);
            var asked = new List<LineAsk>();
            LineAccount.Decide(own, trigger, LineConstraint.None, line, ask => { asked.Add(ask); return Pick; });

            // The own account cooling under a line that switches: the switch's walk, counted only by the driver's carry-on.
            // An account that cannot carry the words, or a ready one the model is not offered on, walks only a line that walks.
            LineAsk? wanted = hold == NextHold.Cooling && !trigger.NewSession && !trigger.Limited
                ? line.Switches ? new LineAsk(LineWalkPlace.Switch, line.Sort == LineSort.DrivenOpen) : null
                : (hold is not (NextHold.Ready or NextHold.Near) || notOffered) && line.Walks
                    ? new LineAsk(LineWalkPlace.Elsewhere, Counts: true, NotOffered: hold is NextHold.Ready or NextHold.Near)
                    : null;
            Assert.True(
                wanted is null ? asked.Count == 0 : asked.Count == 1 && asked[0] == wanted,
                $"{hold} notOffered={notOffered} {trigger} {line}: asked {string.Join(", ", asked)}");
        }
    }

    // ——— The census.

    /// <summary>
    /// The files that read a cooling account (<c>CoolingOf</c>, <c>CoolingsOf</c>), ask a resume (<c>ResumeAsync</c>) or read whether its words
    /// wait (<c>ResumeChoice.Waits</c>) and have not moved onto the rule. Each only hands the roster's reading of an account's cool-off to a
    /// listing or a verb of the shell, which decides nothing by it (FOUNDATION1a-i's census; each moves with the row that moves its reader).
    /// </summary>
    private static readonly string[] NotYetMoved =
    [
        "Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs",
        "Daoris.Desktop.Modules/Bridge/Routes/SessionsVerbs.cs",
    ];

    /// <summary>
    /// Every caller of the roster's resume, of <c>CoolingOf</c> and <c>CoolingsOf</c>, and every reader of <c>ResumeChoice.Waits</c>, reads
    /// <see cref="LineAccount"/> or is named <i>not yet moved</i> (X4-1.1); and the walks the rule asks are the places today's resume walks,
    /// the handed quest's start beside them.
    /// </summary>
    [Fact]
    public void Every_caller_of_the_account_facts_reads_the_rule_or_is_named_not_yet_moved()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");
        var sites = new[] { @"Harnesses\s*\.ResumeAsync\s*\(", @"\.CoolingsOf\(", @"\.CoolingOf\(", @"\bresume\s+is\s*\{\s*Waits\b", @"\bresume\.Waits\b" };
        var unread = new List<string>();
        var found = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/") || relative.Split('/')[0].EndsWith(".Tests", StringComparison.Ordinal)
                || relative.EndsWith("Harnesses/HarnessRoster.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (!sites.Any(site => Regex.IsMatch(text, site))) continue;

            found++;
            if (!Regex.IsMatch(text, @"\b(LineAccount|LineKind|LineAnswer)\b") && !NotYetMoved.Contains(relative)) unread.Add(relative);
        }

        Assert.True(found >= 5, $"the census found {found} files that read an account's cool-off or resume: its patterns have drifted");
        Assert.True(unread.Count == 0, "read neither the account rule nor named not yet moved: " + string.Join(", ", unread));
        Assert.All(NotYetMoved, name => Assert.True(File.Exists(Path.Combine(root, name)), $"{name} is named not yet moved but is not there"));
        Assert.Equal(["NoToolchain", "Switch", "Elsewhere", "Handed"], Enum.GetNames<LineWalkPlace>());
    }

    // ——— The earliest reset, each caller's own set.

    /// <summary>
    /// <see cref="LineAccount.Earliest"/> reads the set it is named for: the walk's states as <c>Unready</c> reads them, the wake's coolings as
    /// <c>CoolingsOf</c> lists them (every cooling account of the agent, the walk's or not), and the switch's walk against the own reset,
    /// the walk's only where strictly earlier.
    /// </summary>
    [Fact]
    public async Task The_earliest_reset_reads_each_callers_own_set()
    {
        var (roster, home) = Build(new Case(OwnState.Cooling, Others.CoolingAfter, Trigger.None, Kind.DrivenOpen));
        // An account no longer on this machine cools sooner than any of them: the wake's set only.
        AccountCooling.Cool(home, new CoolingEntry("fake", "account-9", Seen.AddMinutes(30), true, "session", Seen.AddMinutes(-5), "s1"), Seen);

        var coolings = roster.CoolingsOf("fake");
        Assert.Equal("account-9", coolings[0].Account);
        Assert.Equal(coolings[0], LineAccount.Earliest(ResetSet.Wake, coolings: coolings));

        // The walk refuses with the first reset among its own accounts, which is not the wake's.
        var refused = await roster.SelectAsync("fake", Config, null, null, StartKind.Conversation);
        Assert.Equal(("account-1", false), (refused.Cooling!.Account, refused.Allowed));
        var states = coolings.Where(entry => entry.Account != "account-9").Select(entry => new AccountState(entry.Account, AccountReadiness.Cooling, entry)).ToList();
        Assert.Equal(refused.Cooling, LineAccount.Earliest(ResetSet.Walk, states));
        Assert.Null(LineAccount.Earliest(ResetSet.Walk, [new AccountState("account-2", AccountReadiness.SignedOut)]));
        Assert.Null(LineAccount.Earliest(ResetSet.Wake, coolings: []));

        var own = coolings.Single(entry => entry.Account == "account-1");
        var sooner = own with { Account = "account-2", Until = own.Until.AddHours(-1) };
        var later = own with { Account = "account-3", Until = own.Until.AddHours(1) };
        var same = own with { Account = "account-3" };
        Assert.Same(sooner, LineAccount.Earliest(ResetSet.Switch, own: own, walked: sooner));
        Assert.Same(own, LineAccount.Earliest(ResetSet.Switch, own: own, walked: later));
        Assert.Same(own, LineAccount.Earliest(ResetSet.Switch, own: own, walked: same));
        Assert.Same(own, LineAccount.Earliest(ResetSet.Switch, own: own));
    }
}
