using System.Globalization;

namespace Daoris.Driver;

/// <summary>Why an account a start may use is or is not ready (TOOL4f, D125 §3.3): every way of not being ready is walked past alike.</summary>
public enum AccountReadiness
{
    /// <summary>Not cooling, not refused, and not signed out: it may run.</summary>
    Ready,

    /// <summary>Its cool-off has not passed (TOOL4d): it waits for a time.</summary>
    Cooling,

    /// <summary>Its provider refused its credential (AGT3b): it waits for a person.</summary>
    Refused,

    /// <summary>The agent says nobody is signed in there, or its key is gone: it waits for a person.</summary>
    SignedOut,
}

/// <summary>
/// The accounts a held start passed because they are not signed in (TOOL6g): whose, and which, in the order the walk tried
/// them. Machine-local, as every account name is: the page says them in the reader's language, and the log by name.
/// </summary>
/// <param name="Agent">Whose accounts: the owner a door runs as (AGT7), which `daoris agent login` takes.</param>
public sealed record SignedOutAccounts(string Agent, IReadOnlyList<string> Accounts)
{
    /// <summary>
    /// The name the person gave each of <see cref="Accounts"/> (ACCT2b), in the same order, null where one has none: read when
    /// the hold was said, so the page says each by it. The ids stay the facts a sign-in and the log take.
    /// </summary>
    public IReadOnlyList<string?> Names { get; init; } = [];

    /// <summary>The accounts, each with the name the person gave it where <paramref name="names"/> holds one.</summary>
    public static SignedOutAccounts Of(string agent, IReadOnlyList<string> accounts, IReadOnlyDictionary<string, string>? names) =>
        new(agent, accounts)
        {
            Names = [.. accounts.Select(account => names is not null && names.TryGetValue(account, out var name) ? name : null)],
        };
}

/// <summary>What a start is, for the walk (TOOL6b, D130 §4.6, §16.3): driven work never starts on a kept account.</summary>
public enum StartKind
{
    /// <summary>A driven start, a carry-on, a resume, an intake or a set-up.</summary>
    Driven,

    /// <summary>A conversation or Ask Daoris's opening.</summary>
    Conversation,
}

/// <summary>
/// The step of the walk that chose a start's account (TOOL6b, D130 §13 as §16 amends it): the log's <c>why</c>. <c>Near</c>
/// and <c>Pace</c> read the agent's own word about its windows (TOOL6c).
/// </summary>
public enum WalkStep { Cooling, Refused, SignedOut, Kept, Near, Fewest, Lapsing, Pace, LeastRecent, List }

/// <summary>
/// What Daoris knows of one account (TOOL6b, D130 §16.4), and what its agent last said about its windows, where it said
/// anything (TOOL6c, §5.2): null is unknown, never spent and never fresh.
/// </summary>
public sealed record AccountFacts(
    int Running = 0, DateTimeOffset? LastStarted = null, long? Chosen = null, DateTimeOffset? WeekResets = null, AccountSaid? Said = null);

/// <summary>The step that chose a start's account, and the one that chose it among the rest where the first passed one.</summary>
public sealed record WalkChoice(WalkStep Step, WalkStep? Rest = null)
{
    /// <summary>
    /// The account <see cref="Step"/> weighed the one the start ran on against, where it is near or pace (TOOL6c): near names
    /// the account passed, pace the one it was ahead of, so the start's first line can say what that account said.
    /// </summary>
    public string? Over { get; init; }

    /// <summary>The account <see cref="Rest"/> weighed it against, where that is near or pace.</summary>
    public string? RestOver { get; init; }
}

/// <summary>One account a start may use, as the walk found it (TOOL4f, D125 §3.3).</summary>
/// <param name="Account">The profile's name, or null for the tool's own configuration home.</param>
/// <param name="Readiness">Whether it may run, and if not, why.</param>
/// <param name="Cooling">Its cool-off, when that is why.</param>
/// <param name="Refusal">
/// The sentence a start held on this account alone would be refused with, the one it was refused with before rotation
/// existed: the hold, the refused key's, or the sign-in's.
/// </param>
public sealed record AccountState(string? Account, AccountReadiness Readiness, CoolingEntry? Cooling = null, string? Refusal = null)
{
    public bool IsReady => Readiness == AccountReadiness.Ready;
}

/// <summary>
/// A start that runs on another account of its scope's list than the one the scope begins at (TOOL4f, D125 §3.3): that
/// one was not ready, or kept for conversations, or the goal's walk ranked another ahead of it (TOOL6b, D130 §16.3).
/// Machine-local: it names accounts, so it goes into the conversation record and the machine log, never into the note,
/// which travels (§3.6).
/// </summary>
/// <param name="From">Where the scope begins: its default, else its list's first; never a pick and never the tool's own home.</param>
/// <param name="Cooling">Its cool-off, when that is why it was passed.</param>
/// <param name="Why">Why it was passed, as a clause a person reads on this machine; it names the account.</param>
public sealed record RotatedStart(string From, CoolingEntry? Cooling, string Why)
{
    /// <summary>The step that moved the start off it: the log's <c>why</c> (D130 §13 as §16 amends it).</summary>
    public WalkStep Step { get; init; } = WalkStep.Cooling;

    /// <summary>The workspace whose list it was, or null for this machine's: the log's <c>scope</c>.</summary>
    public string? Scope { get; init; }

    /// <summary>
    /// What the account it was moved off last said, as the log names it (TOOL6c): <c>refused</c>, <c>near</c> or
    /// <c>clear</c>, or null where it said nothing (<see cref="AccountReadings.Standing"/>).
    /// </summary>
    public string? FromSaid { get; init; }

    /// <summary>What the account it ran on last said, as <see cref="FromSaid"/> names it.</summary>
    public string? ToSaid { get; init; }
}

/// <summary>
/// Why the next start of a scope would take its account (TOOL6e, D130 §3–§4 as §16.3 amends them, D125 §3.7): a closed set
/// the screen translates, the walk's steps among them by the log's own names. Read without starting anything.
/// </summary>
public enum NextReason
{
    /// <summary>No default and no list name an account: the tool's own sign-in (D125 §3.7).</summary>
    Own,

    /// <summary>The scope names one account: a default and no list, or a list of one.</summary>
    Named,

    /// <summary>The list names others, and every other is held.</summary>
    OnlyReady,

    /// <summary>A conversation: the kept account first where it is the default, or the kept account taken last.</summary>
    Kept,

    /// <summary>The account it was weighed against said it is near its limit, so went last (§16.3 step 2).</summary>
    Near,

    /// <summary>It runs fewer of Daoris's sessions (step 3).</summary>
    Fewest,

    /// <summary>Its known weekly reset falls within the next day (step 4).</summary>
    Lapsing,

    /// <summary>It is further behind its week's pace (step 5).</summary>
    Pace,

    /// <summary>Daoris started on it less recently (step 6).</summary>
    LeastRecent,

    /// <summary>Daoris has not started on it yet (step 6, where it was never started).</summary>
    NotStarted,

    /// <summary>The list's order, begun at its default: nothing else told the two apart (step 7, or <c>order</c>'s walk).</summary>
    List,

    /// <summary>No account the scope may use is ready: the start waits (D125 §4).</summary>
    Waits,
}

/// <summary>What keeps the next start off an account of the agent's (TOOL6e), or that nothing does.</summary>
public enum NextHold
{
    /// <summary>Ready, and ranked after the account the next start takes.</summary>
    Ready,

    /// <summary>Ready, and its agent said it is near its limit, so it goes last (D130 §6).</summary>
    Near,

    /// <summary>Its cool-off has not passed (D125 §2): it waits for a time.</summary>
    Cooling,

    /// <summary>Its provider refused its credential (AGT3b): it waits for a person.</summary>
    Refused,

    /// <summary>The agent last said nobody is signed in there: it waits for a person.</summary>
    SignedOut,

    /// <summary>Kept for conversations, which driven work never starts on (§4.6).</summary>
    Kept,

    /// <summary>The list names it, and this machine has no such account.</summary>
    Missing,

    /// <summary>The scope does not use it: never carries its starts (D130 §3.1).</summary>
    Outside,
}

/// <summary>One account the next start does not take, and what holds it (TOOL6e).</summary>
/// <param name="Account">The profile's name, or null for the tool's own sign-in.</param>
/// <param name="Until">When it is offered again, where it is cooling.</param>
public sealed record AccountHeld(string? Account, NextHold Hold, DateTimeOffset? Until = null);

/// <summary>
/// Which account the next start of a scope would take, why, and what holds each other account of the agent's (TOOL6e):
/// the walk's answer asked without starting, counting or probing anything. Machine-local, as every account name is.
/// </summary>
/// <param name="Account">The account it takes, or null for the tool's own sign-in, and while it <see cref="NextReason.Waits"/>.</param>
/// <param name="Others">Every other account of the agent's, in the order the walk tried them, then the kept account a driven
/// start drops, then accounts the list names that this machine lacks, then accounts the scope does not use, by name.</param>
public sealed record NextStart(string? Account, NextReason Reason, IReadOnlyList<AccountHeld> Others)
{
    /// <summary>The account the reason weighed it against: where the list begins, or else the next ready account.</summary>
    public string? Over { get; init; }

    /// <summary>The moment the reason names: its week's reset (<see cref="NextReason.Lapsing"/>), or the first account offered again (<see cref="NextReason.Waits"/>).</summary>
    public DateTimeOffset? When { get; init; }
}

/// <summary>
/// The step of the goal's walk that chose a start's account, and what its record says first (TOOL6b, D130 §16.4).
/// Machine-local, as <see cref="RotatedStart"/> is: the clause may name accounts.
/// </summary>
/// <param name="Step">The step: the one that moved it off where its scope begins, or that ranked it ahead of the next.</param>
/// <param name="Clause">The step said for a person, and, where the first account was passed for itself, the step among the rest.</param>
public sealed record AccountChoice(WalkStep Step, string Clause);

/// <summary>
/// One step of the walk as a start's record says it (UX7d-1, D152's UX7d-1 note): the clause in English, as
/// <see cref="RotationWords.Clause"/> joins it, and the same step by code, a reason of <see cref="NoteCodes.Opening"/> with its
/// own values, so the page words the line in the reader's language. Machine-local: its values may name an account.
/// </summary>
/// <param name="Text">The clause in English.</param>
/// <param name="Why">Its reason, one of <see cref="OpeningWhy"/>; null where no reason says it, and the line is then kept with no parts.</param>
/// <param name="Values">The reason's own values: an account, a moment, a percent, a window, a workspace.</param>
public sealed record StepSaid(string Text, string? Why, IReadOnlyList<(string Name, object? Value)> Values);

/// <summary>
/// Which accounts a start may use, in the order it tries them, and which step chose the one it ran on (TOOL6b, D130 §16;
/// D125 §3.3 under <c>use: order</c>). Pure: what Daoris knows of each account is handed in, and whether each is ready is
/// the roster's question, asked in this order.
/// </summary>
/// <remarks>
/// <para><b>One scope, its list the whole set</b> (D130 §2 rules 1–2, §3.1): only a scope with a list is walked. A pick, a
/// scope that names one account, and the tool's own sign-in are that one account, and the roster asks nothing here.</para>
///
/// <para><b>The goal</b> (<c>use: goal</c>, the default): every listed account is used toward the most work, the fewest
/// stalls and no allowance left at a reset, with what Daoris knows of its own (§16.4) and what each account's agent last said
/// about its windows (TOOL6c, §5.2): near and pace. <b>Nothing counts accounts</b>: every rule reads the list it is handed,
/// and while every account is ready none runs more than ⌈K ÷ N⌉ of a cap of K (§7). Under <c>use: order</c> the first ready
/// account of the list carries every start, as D125 built it, and rotation moves work off a ready account only where its
/// agent said it is near and <i>switch before the limit</i> is on (§16.6).</para>
/// </remarks>
public static class AccountRotation
{
    /// <summary>
    /// How near a week's reset must be for its account to go first (D130 §16.3 step 4): a day holds about five five-hour
    /// windows, so before an account's last day what it has left can still be spent in any order. A constant, not a
    /// setting; TOOL4h's report says whether it is right.
    /// </summary>
    public static readonly TimeSpan LastDay = TimeSpan.FromDays(1);

    /// <summary>
    /// The accounts of a scope's list, in the order a start tries them (TOOL6b, D130 §16.3; D125 §3.3 under <c>order</c>).
    /// Pure: whether each is ready is the roster's question, asked in this order, and the first ready one runs.
    /// </summary>
    /// <remarks>
    /// <para>The sequence is the list begun where the scope begins (its default, else its first), wrapping: the account it
    /// begins at always, and every other only where the machine has it, since a start pointed at a directory nobody signed
    /// in to would fail where it should have walked on. Then the kept account (§4.6): a driven start drops it; a
    /// conversation takes it last, or first where it is also the scope's default, and under <c>order</c> leaves it in its
    /// place.</para>
    /// <para>Under <c>goal</c> each step after keep is stable, so the order handed on stands where a step has no reason to
    /// change it: an account its agent said is near goes last (with <i>switch before the limit</i> on), then fewest of
    /// Daoris's sessions running, a week resetting within <see cref="LastDay"/> (sooner first), furthest behind its week's
    /// pace (one that said nothing between behind and ahead), least recently started (never first, a start this look chose
    /// last), then the sequence. Under <c>order</c> the sequence is the walk, as D125 built it, with a near account last
    /// unless the switch is off (§16.6), and a conversation's kept account in its place.</para>
    /// </remarks>
    /// <param name="present">The agent's accounts on this machine.</param>
    public static IReadOnlyList<string> Order(
        RotationScope scope, StartKind kind, IReadOnlyCollection<string> present, IReadOnlyDictionary<string, AccountFacts> facts,
        DateTimeOffset now)
    {
        var sequence = Sequence(scope, present);
        if (sequence.Count == 0) return sequence;

        var keep = Keep(scope, sequence);
        var goal = scope.Use.Use == "goal";
        string? first = null, last = null;
        if (keep is not null && (goal || kind == StartKind.Driven))
        {
            sequence.RemoveAll(name => Same(name, keep));
            if (kind == StartKind.Conversation)
            {
                if (scope.Default is { } own && Same(own, keep)) first = keep;
                else last = keep;
            }
        }

        var position = sequence.Select((name, index) => (name, index)).ToDictionary(pair => pair.name, pair => pair.index, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> walked = goal || scope.Use.Early
            ? sequence.OrderBy(name => name, Comparer<string>.Create((a, b) => Rank(a, b, facts, now, scope.Use) is var (_, order) && order != 0 ? order : position[a].CompareTo(position[b])))
            : sequence;

        return [.. (first is null ? [] : new[] { first }), .. walked, .. (last is null ? [] : new[] { last })];
    }

    /// <summary>
    /// Which step chose the account at <paramref name="at"/> of <paramref name="tried"/> (TOOL6b, D130 §13 as §16 amends
    /// it): the log's <c>why</c>, and what the start's first line says.
    /// </summary>
    /// <remarks>
    /// <para>A start that ran somewhere other than where its scope begins was moved off that account by a step: it was
    /// kept for conversations, it was not ready (cooling, refused, signed out), or a step of the goal ranked the account it
    /// ran on ahead of it. Where the first account was passed for itself, the step that chose among the rest is said too,
    /// unless the list's order did.</para>
    /// <para>A start that ran where its scope begins was chosen by the step that ranked it ahead of the account it would
    /// have run on next, or by the list's order where nothing else told them apart or nothing else was ready.</para>
    /// </remarks>
    public static WalkChoice Chose(
        RotationScope scope, StartKind kind, IReadOnlyList<AccountState> tried, int at, IReadOnlyDictionary<string, AccountFacts> facts,
        DateTimeOffset now)
    {
        var ran = tried[at].Account!;
        var begins = scope.Begins;
        var keep = Keep(scope, scope.List);
        var goal = scope.Use.Use == "goal";
        var conversationKeeps = goal && kind == StartKind.Conversation && keep is not null;

        if (begins is null || Same(ran, begins))
        {
            if (conversationKeeps && Same(ran, keep!)) return new(WalkStep.Kept);
            return goal && Next(ran, null) is { } after ? Ahead(ran, after) : new(WalkStep.List);
        }

        var passed = tried.FirstOrDefault(state => Same(state.Account!, begins));
        var step = passed is null || (conversationKeeps && Same(begins, keep!))
            ? new WalkChoice(WalkStep.Kept)
            : passed.Readiness switch
            {
                AccountReadiness.Cooling => new WalkChoice(WalkStep.Cooling),
                AccountReadiness.Refused => new WalkChoice(WalkStep.Refused),
                AccountReadiness.SignedOut => new WalkChoice(WalkStep.SignedOut),
                _ => Ahead(ran, begins),
            };
        if (!goal || step.Step is not (WalkStep.Kept or WalkStep.Cooling or WalkStep.Refused or WalkStep.SignedOut)) return step;

        var rest = Next(ran, begins) is { } runnerUp ? Ahead(ran, runnerUp) : new WalkChoice(WalkStep.List);
        return rest.Step == WalkStep.List ? step : step with { Rest = rest.Step, RestOver = rest.Over };

        // The next ready account after the one it ran on, but the one it was moved off.
        string? Next(string after, string? but) =>
            tried.Skip(at + 1).FirstOrDefault(state => state.IsReady && (but is null || !Same(state.Account!, but)))?.Account;

        // The step that puts it ahead of another, naming that account where the step is about what its agent said.
        WalkChoice Ahead(string a, string b) => Rank(a, b, facts, now, scope.Use) is { Order: < 0 } ranked
            ? new WalkChoice(ranked.Step) { Over = ranked.Step is WalkStep.Near or WalkStep.Pace ? b : null }
            : new WalkChoice(WalkStep.List);
    }

    /// <summary>
    /// Which account the next start would take, the step that chose it, and what holds each other account (TOOL6e, D130
    /// §3–§4 as §16.3 amends them): what the agent's page in Agents says, so a person sees why a start runs where it does.
    /// </summary>
    /// <remarks>
    /// <para>The account is the first ready one of <paramref name="tried"/>, as a start takes it. The reason is weighed as
    /// <see cref="Chose"/> weighs it, so the screen and the start's own first line name one step: against where the list
    /// begins, when that account was ready and a step moved the start off it; otherwise against the next ready account, by
    /// the step that told the two apart or the list's order. An account not ready, or kept for conversations, is a hold and
    /// never the reason. With no account ready the start waits, until the first cool-off ends where one is cooling.</para>
    /// <para>A scope with no list is its one account, or the tool's own sign-in where it names none (D125 §3.7), and no step
    /// chose it. Every account of the agent's the scope does not use is said so, since none ever carries its starts.</para>
    /// </remarks>
    /// <param name="tried">The walk's order (<see cref="Order"/>), or a scope's one account, each as the roster finds it
    /// before any probe: cooling, refused, or signed out by the agent's last word.</param>
    /// <param name="present">The agent's accounts on this machine.</param>
    public static NextStart Next(
        RotationScope scope, StartKind kind, IReadOnlyList<AccountState> tried, IReadOnlyCollection<string> present,
        IReadOnlyDictionary<string, AccountFacts> facts, DateTimeOffset now)
    {
        var listed = scope.List.Count > 0;
        var keep = listed ? Keep(scope, scope.List) : null;
        var at = -1;
        for (var i = 0; i < tried.Count && at < 0; i++)
        {
            if (tried[i].IsReady) at = i;
        }

        var others = new List<AccountHeld>();
        for (var i = 0; i < tried.Count; i++)
        {
            if (i != at) others.Add(Held(tried[i]));
        }

        if (keep is not null && !tried.Any(state => state.Account is { } name && Same(name, keep))) others.Add(new(keep, NextHold.Kept));
        others.AddRange(scope.List
            .Where(name => !present.Contains(name, StringComparer.OrdinalIgnoreCase) && !tried.Any(state => state.Account is { } tries && Same(tries, name)))
            .Select(name => new AccountHeld(name, NextHold.Missing)));
        others.AddRange(present
            .Where(name => !scope.List.Contains(name, StringComparer.OrdinalIgnoreCase) && !(scope.Begins is { } one && Same(one, name)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new AccountHeld(name, NextHold.Outside)));

        if (at < 0)
        {
            return new NextStart(null, NextReason.Waits, others)
            {
                When = tried.Where(state => state.Cooling is not null).Select(state => (DateTimeOffset?)state.Cooling!.Until).Min(),
            };
        }

        var runs = tried[at].Account;
        if (!listed || runs is null) return new NextStart(runs, runs is null ? NextReason.Own : NextReason.Named, others);

        var conversationKeeps = kind == StartKind.Conversation && keep is not null;
        if (conversationKeeps && Same(runs, keep!)) return new NextStart(runs, NextReason.Kept, others);

        // Against where the list begins while a step moved the start off it; else against the next ready account.
        var begins = scope.Begins;
        var against = begins is not null && !Same(runs, begins) && !(conversationKeeps && Same(begins, keep!))
                      && tried.Any(state => state.IsReady && Same(state.Account!, begins))
            ? begins
            : tried.Skip(at + 1).FirstOrDefault(state => state.IsReady)?.Account;
        if (against is null) return new NextStart(runs, scope.List.Count > 1 ? NextReason.OnlyReady : NextReason.Named, others);
        if (conversationKeeps && Same(against, keep!)) return new NextStart(runs, NextReason.Kept, others) { Over = against };

        var own = facts.GetValueOrDefault(runs) ?? new AccountFacts();
        var reason = Rank(runs, against, facts, now, scope.Use) switch
        {
            { Order: >= 0 } => NextReason.List,
            { Step: WalkStep.Near } => NextReason.Near,
            { Step: WalkStep.Fewest } => NextReason.Fewest,
            { Step: WalkStep.Lapsing } => NextReason.Lapsing,
            { Step: WalkStep.Pace } => NextReason.Pace,
            { Step: WalkStep.LeastRecent } => Started(own).Tier == 0 ? NextReason.NotStarted : NextReason.LeastRecent,
            _ => NextReason.List,
        };
        return new NextStart(runs, reason, others)
        {
            Over = against,
            When = reason == NextReason.Lapsing ? own.WeekResets : null,
        };

        AccountHeld Held(AccountState state) => state.Readiness switch
        {
            AccountReadiness.Cooling => new(state.Account, NextHold.Cooling, state.Cooling?.Until),
            AccountReadiness.Refused => new(state.Account, NextHold.Refused),
            AccountReadiness.SignedOut => new(state.Account, NextHold.SignedOut),
            _ => new(state.Account,
                listed && scope.Use.Early && state.Account is { } name && AccountReadings.Near(facts.GetValueOrDefault(name)?.Said, scope.Use.Near)
                    ? NextHold.Near
                    : NextHold.Ready),
        };
    }

    /// <summary>The scope's list begun where it begins, wrapping: that account always, every other only where the machine has it.</summary>
    private static List<string> Sequence(RotationScope scope, IReadOnlyCollection<string> present)
    {
        var list = scope.List;
        if (list.Count == 0 || scope.Begins is not { } begins) return [];
        var at = Math.Max(0, list.ToList().FindIndex(name => Same(name, begins)));
        var sequence = new List<string> { list[at] };
        for (var step = 1; step < list.Count; step++)
        {
            var next = list[(at + step) % list.Count];
            if (present.Contains(next, StringComparer.OrdinalIgnoreCase)) sequence.Add(next);
        }

        return sequence;
    }

    /// <summary>
    /// The kept account a walk reads: the scope's, unless it would leave driven work none, which a door refuses and a file
    /// edited by hand is read past, the list winning as it does over a default outside it (§3.1, §4.6).
    /// </summary>
    private static string? Keep(RotationScope scope, IReadOnlyList<string> among) =>
        scope.Use.Keep is { } keep && among.Any(name => Same(name, keep)) && among.Any(name => !Same(name, keep)) ? keep : null;

    /// <summary>The account a scope keeps for conversations, as the walk reads it (§4.6); null where it keeps none (MSG1g).</summary>
    public static string? KeptOf(RotationScope scope) => Keep(scope, scope.List);

    /// <summary>
    /// The first step at which <paramref name="a"/> and <paramref name="b"/> differ, and which goes first there (negative:
    /// <paramref name="a"/>); <see cref="WalkStep.List"/> and zero where none does. Under <c>order</c> only near is a step.
    /// </summary>
    private static (WalkStep Step, int Order) Rank(
        string a, string b, IReadOnlyDictionary<string, AccountFacts> facts, DateTimeOffset now, RotationUse use)
    {
        var (x, y) = (facts.GetValueOrDefault(a) ?? new AccountFacts(), facts.GetValueOrDefault(b) ?? new AccountFacts());

        if (use.Early)
        {
            var (nearX, nearY) = (AccountReadings.Near(x.Said, use.Near), AccountReadings.Near(y.Said, use.Near));
            if (nearX != nearY) return (WalkStep.Near, nearX ? 1 : -1);
        }

        if (use.Use != "goal") return (WalkStep.List, 0);

        if (x.Running != y.Running) return (WalkStep.Fewest, x.Running.CompareTo(y.Running));

        var (lapsesX, lapsesY) = (Lapses(x, now), Lapses(y, now));
        if (lapsesX != lapsesY) return (WalkStep.Lapsing, lapsesX.CompareTo(lapsesY));

        // An account that said nothing about its week is neither behind nor ahead (§16.3 step 5): it ranks as on pace.
        var (behindX, behindY) = (AccountReadings.Behind(x.Said, now) ?? 0, AccountReadings.Behind(y.Said, now) ?? 0);
        if (behindX != behindY) return (WalkStep.Pace, behindY.CompareTo(behindX));

        var (startedX, startedY) = (Started(x), Started(y));
        if (startedX != startedY) return (WalkStep.LeastRecent, startedX.CompareTo(startedY));

        return (WalkStep.List, 0);
    }

    /// <summary>When its week resets, where that is within the last day; else the end of time, which ranks nothing.</summary>
    private static DateTimeOffset Lapses(AccountFacts facts, DateTimeOffset now) =>
        facts.WeekResets is { } reset && reset > now && reset - now <= LastDay ? reset : DateTimeOffset.MaxValue;

    /// <summary>How recently Daoris started on it: never, then by when, then a start this look chose, in the order chosen.</summary>
    private static (int Tier, long At) Started(AccountFacts facts) =>
        facts.Chosen is { } chosen ? (2, chosen)
        : facts.LastStarted is { } started ? (1, started.UtcTicks)
        : (0, 0);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// What a start says once its record is open (TOOL4f, D125 §3.3, §3.6, §5.4; TOOL6b, D130 §16.4): its record's first line,
/// naming the account it opened on and why, and <c>account.rotated</c> in the machine log when it ran somewhere other than
/// where its scope begins. One home for a driven start, a carry-on, an intake, a conversation and Ask Daoris's opening, so
/// none says it differently.
/// </summary>
public static class RotatedOpening
{
    /// <summary>The cut-off session a rotated carry-on carries on (D80), with its refused turn and context after a limit.</summary>
    /// <param name="Session">The cut-off session's record.</param>
    /// <param name="Turn">Its refused turn (the turns its record ended, plus one), where a limit cut it off; else null.</param>
    /// <param name="Used">Its context at its high-water, where a limit cut it off and its door reported one; else null.</param>
    public sealed record Carried(string Session, long? Turn, long? Used);

    /// <summary>
    /// Say it: under the goal, every start whose list offered a choice names the step that chose its account, then what each
    /// account of its list said about its windows, or that none has said what it has left (TOOL6c); under <c>order</c>, a
    /// start that rotated says why, as D125 built it, and what each account said where one has; otherwise nothing. The
    /// record's failure costs its line, never the start.
    /// </summary>
    internal static void Say(
        ServiceClient service, SessionEvents events, string sessionId, string adapter, HarnessSelection selection, Carried? carried)
    {
        if (selection.Profile is not { } to || Line(selection, carried) is not { } line) return;

        // The conversation record is the machine's (D76 §2: its events never sync and never cross HTTP), so this line may name
        // both accounts, in its English and in its parts' values (UX7d-1); the note, which travels, never does (§3.6).
        events.Keep(
            sessionId,
            new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null },
            say: null);
        if (selection.Rotated is { } moved)
        {
            service.AccountSaid(AccountLine.Rotated(
                sessionId, adapter, moved.From, to, carried?.Session, moved.Step, moved.Scope, said: selection.SaidLine is not null,
                fromSaid: moved.FromSaid, toSaid: moved.ToSaid));
        }
    }

    /// <summary>
    /// The record's first line (UX7d-1, D152's UX7d-1 note): its English, byte for byte what the record has always kept, and its
    /// lines by code where the walk handed its steps by code (<see cref="HarnessSelection.Steps"/>): the account it opened on
    /// with the first step's reason, the rest's step and a refused turn riding inside it, then what each account said. A line
    /// whose steps are not the ones its English says is kept with no parts, so the page draws the English whole rather than
    /// half of it worded. Null where the start says nothing.
    /// </summary>
    internal static Noted? Line(HarnessSelection selection, Carried? carried)
    {
        if (selection.Profile is not { } to) return null;

        string clause;
        IReadOnlyList<StepSaid>? steps;
        Noted? said;
        if (selection.Choice is { } choice)
        {
            (clause, steps) = (choice.Clause, selection.Steps);
            said = selection.AccountsSaid ?? Noted.Of(NoteCodes.OpeningUnsaid, RotationWords.Unsaid);
        }
        else if (selection.Rotated is { } rotated)
        {
            // Under `order` the line says the step that moved it off where its scope begins, never the rest's (D125 §3.3).
            (clause, steps) = (rotated.Why, selection.Steps is { Count: > 0 } all ? [all[0]] : null);
            said = selection.AccountsSaid;
        }
        else
        {
            return null;
        }

        var english = carried is { } cut
            ? RotationWords.CarriedOn(cut.Session, to, clause, cut.Turn, cut.Used)
            : RotationWords.Opened(to, clause);
        if (steps is not { Count: > 0 } || steps.Any(step => step.Why is null) || RotationWords.Text(steps) != clause)
        {
            return new Noted(said is null ? english : english + " " + said.Note, []);
        }

        (string Name, object? Value)[] lead = [("account", to), ("why", steps[0].Why), .. steps[0].Values];
        var line = carried is { } from
            ? Noted.Of(NoteCodes.OpeningCarriedOn, english, [("session", from.Session), .. lead])
            : Noted.Of(NoteCodes.OpeningOpened, english, lead);
        if (steps.Count > 1)
        {
            line = line.Also(NoteCodes.OpeningRest.Part(RotationWords.Rest(steps[1].Text), [("why", steps[1].Why), .. steps[1].Values]));
        }

        if (carried?.Turn is { } turn)
        {
            line = line.Also(carried.Used is { } used
                ? NoteCodes.OpeningTurnContext.Part(RotationWords.Refused(turn, used), ("turn", turn), ("tokens", used))
                : NoteCodes.OpeningTurnRefused.Part(RotationWords.Refused(turn, null), ("turn", turn)));
        }

        return said is null ? line : line.Then(" ", said);
    }

    /// <summary>
    /// A session's context at its high-water (D125 §3.6): what this machine's usage record kept of it, else the highest its
    /// own record's usage reports reached; null where neither says, never zero (TOOL3).
    /// </summary>
    internal static long? ContextOf(SessionUsage? usage, SessionEvents events, string session)
    {
        if (usage?.Of(session) is { } kept) return kept.Used;
        if (!SessionEvents.IsId(session)) return null;
        try
        {
            return events.After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Usage).Max(e => e.Used);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }
}

/// <summary>
/// What rotation says (TOOL4f, D125 §3.3, §3.6, §4): a wait over an order, a pick refused, and the opening line of a
/// rotated start's record. The driver's own sentences, rendered verbatim like every sentence it writes (D24); the
/// names follow the glossary (D116): an account, signed in, the tool's own sign-in.
/// </summary>
/// <remarks>
/// These name accounts, so each is said only where this machine reads it: a start's hold, a conversation's refusal, the
/// conversation record (D76). 🔴 None of them goes into a session's note, which travels, and whose scrubber knows only
/// the record's own account (§3.6).
/// <para>What a person reads names an account by the name they gave it (ACCT2b, D125's ACCT2b note), from the owner's names
/// read as the sentence is said, else by its id: a wait, a hold, a pick refused, what a wait adds. A command in a sentence
/// keeps the id, which a terminal takes whatever the account is called by then; and the record's opening line
/// (<see cref="Opened(string, string)"/>, <see cref="CarriedOn(string, string, string, long?, long?)"/>, <see cref="Clause"/>)
/// keeps the ids, so a record is read back to its account after a rename.</para>
/// </remarks>
public static class RotationWords
{
    /// <summary>
    /// Every account a start may use is not ready, and one at least is cooling (§4): the start waits for the first
    /// reset. Every account cooling is said as such; otherwise each account and why.
    /// </summary>
    /// <param name="names">The owner's names by id, read as the wait is said (ACCT2b); null says each by its id.</param>
    public static string Wait(
        string agent, IReadOnlyList<AccountState> states, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null)
    {
        var first = states.Where(state => state.Cooling is not null).Select(state => state.Cooling!).MinBy(cooling => cooling.Until)!;
        var when = $"the first ready, `{(first.Account is { } account ? AccountNames.Said(names, account) : null)}`, at "
                   + $"{CoolingWords.When(first.Until, zone)}, {CoolingWords.Why(first)}";
        var every = states.All(state => state.Readiness == AccountReadiness.Cooling)
            ? $"every `{agent}` account this start may use is cooling"
            : $"no `{agent}` account this start may use is ready: {string.Join(", ", states.Select(state => Clause(state, zone, names)))}";
        // TOOL6g: an account not signed in waits for a person, never for the reset, so its sign-in is said beside the wait.
        return $"{every}; {when}. Daoris starts nothing on them until then." + SignIn(agent, SignedOut(states), "A sign-in starts it sooner");
    }

    /// <summary>
    /// No account a start may use is ready and none is cooling, over a list of more than one (TOOL6g): nothing comes ready
    /// by itself, so each account is named with why, and every one not signed in with its sign-in.
    /// </summary>
    /// <param name="names">The owner's names by id, read as the hold is said (ACCT2b); null says each by its id.</param>
    public static string NoneReady(string agent, IReadOnlyList<AccountState> states, IReadOnlyDictionary<string, string>? names = null) =>
        $"no `{agent}` account this start may use is ready: {string.Join(", ", states.Select(state => Clause(state, TimeZoneInfo.Utc, names)))}."
        + SignIn(agent, SignedOut(states), "A sign-in starts it");

    /// <summary>The accounts of a walk not signed in, in the order it tried them.</summary>
    public static IReadOnlyList<string> SignedOut(IReadOnlyList<AccountState> states) =>
        [.. states.Where(state => state.Readiness == AccountReadiness.SignedOut && state.Account is not null).Select(state => state.Account!)];

    /// <summary>
    /// The sign-in for each account not signed in, after <paramref name="lead"/>, both doors named (D50): the terminal's for
    /// each account, and the screen's, the agent's page in the Agents place since UX6e (UX6e2, D150 §5.2). Empty where none is.
    /// Each id is spelled for any shell (ACCTQUOTE1b).
    /// </summary>
    private static string SignIn(string agent, IReadOnlyList<string> accounts, string lead) => accounts.Count == 0
        ? ""
        : $" {lead}: {string.Join(", ", accounts.Select(account => $"`daoris agent login {agent} --profile {ShellWord.Of(account, ShellWord.Account)}`"))}, "
          + "or Agents → the agent's page → Accounts.";

    /// <summary>
    /// A conversation the person started on an account they picked, which is cooling (§3.3): refused, since the person
    /// chose it, with the accounts that are ready by name.
    /// </summary>
    /// <param name="names">The owner's names by id, read as the refusal is said (ACCT2b); null says each by its id.</param>
    public static string Picked(
        CoolingEntry cooling, IReadOnlyList<string> ready, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null) =>
        $"{CoolingWords.Hold(cooling, zone, names)} "
        + (ready.Count == 0
            ? $"No other `{cooling.Agent}` account is ready."
            : $"Ready now: {string.Join(", ", ready.Select(name => $"`{AccountNames.Said(names, name)}`"))}.");

    /// <summary>Why an account was not ready, as a clause: the account named, and the time where it waits for one.</summary>
    public static string Why(string agent, AccountState state, TimeZoneInfo zone) => state.Readiness switch
    {
        AccountReadiness.Cooling => $"{CoolingWords.Who(state.Cooling!)} is cooling until {CoolingWords.When(state.Cooling!.Until, zone)}, {CoolingWords.Why(state.Cooling)}",
        AccountReadiness.Refused => $"{Who(agent, state.Account)} was refused by its provider",
        AccountReadiness.SignedOut => $"{Who(agent, state.Account)} is not signed in",
        _ => $"{Who(agent, state.Account)} is ready",
    };

    /// <summary>The first line of a rotated start's record (§3.3): which account it opened on, and why.</summary>
    public static string Opened(string to, RotatedStart rotated) => Opened(to, rotated.Why);

    /// <summary>The first line of a start's record: which account it opened on, and the clause that says why.</summary>
    public static string Opened(string to, string why) => $"opened on `{to}`: {why}.";

    /// <summary>
    /// The first line of a rotated carry-on's record (§3.6): the cut-off session it carries on, the account it runs on,
    /// why its default was not ready, and, after a limit, the turn that was refused and the context it had.
    /// </summary>
    /// <param name="turn">The cut-off session's refused turn (the turns its record ended, plus one), where a limit cut it off.</param>
    /// <param name="used">Its context at its high-water, where its door reported one.</param>
    public static string CarriedOn(string cutOff, string to, RotatedStart rotated, long? turn, long? used) =>
        CarriedOn(cutOff, to, rotated.Why, turn, used);

    /// <inheritdoc cref="CarriedOn(string, string, RotatedStart, long?, long?)"/>
    public static string CarriedOn(string cutOff, string to, string why, long? turn, long? used) =>
        $"carried on from session `{cutOff}` on `{to}`; {why}{(turn is { } number ? "; " + Refused(number, used) : "")}.";

    /// <summary>A carry-on's refused turn, and the context it had where its door reported one: the clause inside its first line.</summary>
    internal static string Refused(long turn, long? used) =>
        $"its turn {turn} was refused"
        + (used is { } context ? $" with {context.ToString("N0", CultureInfo.InvariantCulture)} tokens of context" : "");

    /// <summary>
    /// What every start the goal chose says after its step while no account of its list has said what it has left (§16.4):
    /// near and pace then wait for an agent's word (TOOL6c reads it where a door carries it, and <see cref="Said"/> says it).
    /// </summary>
    public const string Unsaid = "No account has said what it has left yet.";

    /// <summary>
    /// What each account of a start's list last said about its windows, as one sentence for the start's first line (TOOL6c,
    /// D130 §16.4): in the list's order, each with its age and what it said; null where none has said anything, so the
    /// start says <see cref="Unsaid"/> instead. An account that said nothing is said so: unknown, never empty or full.
    /// </summary>
    /// <param name="accounts">The accounts the start's walk could use, in the list's order.</param>
    public static string? Said(IReadOnlyList<string> accounts, IReadOnlyDictionary<string, AccountFacts> facts, RotationUse use, DateTimeOffset now) =>
        AccountsSaid(accounts, facts, use, now)?.Note;

    /// <summary>
    /// <see cref="Said"/>'s sentence with its lines by code (UX7d-1): a line per account, its name and when it said it or that
    /// it said nothing, and one per thing it said, each line's English inside the sentence, which is unchanged.
    /// </summary>
    public static Noted? AccountsSaid(IReadOnlyList<string> accounts, IReadOnlyDictionary<string, AccountFacts> facts, RotationUse use, DateTimeOffset now)
    {
        if (!accounts.Any(account => facts.GetValueOrDefault(account)?.Said is not null)) return null;
        var each = Joined([.. accounts.Select(account => Account(account, facts.GetValueOrDefault(account)?.Said, use, now))], "; ");
        return new Noted("What each account said: ", []).Then("", each).Then(".");
    }

    /// <summary>One account in <see cref="Said"/>: its name, how long ago, and what it said, in Daoris's words and by code.</summary>
    private static Noted Account(string name, AccountSaid? said, RotationUse use, DateTimeOffset now)
    {
        if (said is null) return Noted.Of(NoteCodes.OpeningSaidNothing, $"`{name}` nothing yet", ("account", name));

        var parts = new List<Noted>();
        if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.RefusedWord) is { } reached)
        {
            parts.Add(Noted.Of(NoteCodes.OpeningSaidReached, $"its {reached.Window} limit reached, by its own word", ("window", reached.Window)));
        }
        else if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.NearWord) is { } warned)
        {
            parts.Add(Noted.Of(NoteCodes.OpeningSaidNear, $"near its {warned.Window} limit, by its own word", ("window", warned.Window)));
        }

        if (said.Windows.Any(each => each.Credits)) parts.Add(Noted.Of(NoteCodes.OpeningSaidCredits, "drawing on usage credits"));
        var used = Ordered(said).Where(each => each.Used is not null)
            .Select(each => Noted.Of(
                NoteCodes.OpeningSaidUsed, $"{Percent(each.Used!.Value)} of its {each.Window} limit", ("used", Whole(each.Used!.Value)), ("window", each.Window)))
            .ToList();
        if (used.Count > 0) parts.Add(Joined(used, " and ").Then(" used"));
        if (AccountReadings.NearWindow(said, use.Near) is { By: NearBy.Number }) parts.Add(Noted.Of(NoteCodes.OpeningSaidNearAt, $"near at {use.Near}%", ("near", use.Near)));
        // A reading with no number, no warning and no credits is its agent's clear word alone.
        if (parts.Count == 0) parts.Add(Noted.Of(NoteCodes.OpeningSaidClear, "clear, by its own word"));
        var who = Noted.Of(NoteCodes.OpeningSaidAt, $"`{name}` {Age(said.Seen, now)}", ("account", name), ("seen", NoteCodes.Moment(said.Seen)));
        return Joined([who, .. parts], ", ");
    }

    /// <summary>Lines joined by <paramref name="glue"/>, as <see cref="string.Join(string, IEnumerable{string})"/> joins their English.</summary>
    private static Noted Joined(IReadOnlyList<Noted> lines, string glue) => lines.Skip(1).Aggregate(lines[0], (joined, next) => joined.Then(glue, next));

    /// <summary>The session window, then the week, then any other, as a person reads them.</summary>
    private static IEnumerable<WindowSaid> Ordered(AccountSaid said) =>
        said.Windows.OrderBy(each => each.Window.ToLowerInvariant() switch { "session" => 0, AccountWindows.Weekly => 1, _ => 2 })
            .ThenBy(each => each.Window, StringComparer.OrdinalIgnoreCase);

    /// <summary>A share as a whole percent: <i>88%</i>.</summary>
    internal static string Percent(double share) => $"{Whole(share).ToString(CultureInfo.InvariantCulture)}%";

    /// <summary>A share as the whole percent <see cref="Percent"/> says, as a value a part carries (UX7d-1): <i>88</i>.</summary>
    internal static int Whole(double share) => (int)Math.Round(share * 100, MidpointRounding.AwayFromZero);

    /// <summary>How long ago something was said: <i>just now</i>, <i>20 min ago</i>, <i>3 h ago</i>, <i>2 d ago</i>.</summary>
    internal static string Age(DateTimeOffset seen, DateTimeOffset now)
    {
        var minutes = (long)Math.Floor((now - seen).TotalMinutes);
        return minutes < 1 ? "just now"
            : minutes < 60 ? $"{minutes} min ago"
            : minutes < 1440 ? $"{minutes / 60} h ago"
            : $"{minutes / 1440} d ago";
    }

    /// <summary>
    /// Which step chose the account a start ran on, as a clause (§16.4): the step that moved it off where its scope begins
    /// or ranked it first, and, where the first account was passed for itself, the step that chose among the rest. Near and
    /// pace say what the account they weighed it against said (TOOL6c).
    /// </summary>
    /// <param name="workspace">The workspace whose list it was, or null for this machine's.</param>
    /// <param name="passed">The state the start found the scope's first account in, where it was walked past.</param>
    public static string Clause(
        WalkChoice choice, string agent, string ran, RotationScope scope, string? workspace, AccountState? passed,
        IReadOnlyDictionary<string, AccountFacts> facts, StartKind kind, DateTimeOffset now, TimeZoneInfo zone) =>
        Text(Steps(choice, agent, ran, scope, workspace, passed, facts, kind, now, zone));

    /// <summary>
    /// <see cref="Clause"/>'s steps one by one, each its English and its reason by code with its own values (UX7d-1, D152's
    /// UX7d-1 note): the step that chose the account, then, where the first account was passed for itself, the step among
    /// the rest. <see cref="Text"/> joins them into the clause.
    /// </summary>
    public static IReadOnlyList<StepSaid> Steps(
        WalkChoice choice, string agent, string ran, RotationScope scope, string? workspace, AccountState? passed,
        IReadOnlyDictionary<string, AccountFacts> facts, StartKind kind, DateTimeOffset now, TimeZoneInfo zone)
    {
        var lead = Step(choice.Step, choice.Over, agent, ran, scope, workspace, passed, facts, kind, now, zone);
        return choice.Rest is { } rest ? [lead, Step(rest, choice.RestOver, agent, ran, scope, workspace, passed, facts, kind, now, zone)] : [lead];
    }

    /// <summary>The clause <see cref="Steps"/> says: the first step, then the rest's.</summary>
    public static string Text(IReadOnlyList<StepSaid> steps) => steps.Count > 1 ? $"{steps[0].Text}; {Rest(steps[1].Text)}" : steps[0].Text;

    /// <summary>The rest's step as the clause says it.</summary>
    internal static string Rest(string step) => $"of the rest, {step}";

    private static StepSaid Step(
        WalkStep step, string? over, string agent, string ran, RotationScope scope, string? workspace, AccountState? passed,
        IReadOnlyDictionary<string, AccountFacts> facts, StartKind kind, DateTimeOffset now, TimeZoneInfo zone)
    {
        var own = facts.GetValueOrDefault(ran) ?? new AccountFacts();
        return step switch
        {
            WalkStep.Cooling or WalkStep.Refused or WalkStep.SignedOut => Passed(agent, passed!, zone),
            WalkStep.Kept when string.Equals(ran, scope.Begins, StringComparison.OrdinalIgnoreCase) => Reason("it is kept for conversations", OpeningWhy.KeptSelf),
            WalkStep.Kept when kind == StartKind.Conversation =>
                Reason($"`{scope.Begins}` is kept for conversations, which take it last", OpeningWhy.KeptLast, ("over", scope.Begins)),
            WalkStep.Kept => Reason($"`{scope.Begins}` is kept for conversations", OpeningWhy.Kept, ("over", scope.Begins)),
            WalkStep.Near => Near(over!, facts.GetValueOrDefault(over!)?.Said, scope.Use),
            WalkStep.Fewest => Reason("it runs the fewest of Daoris's sessions", OpeningWhy.Fewest),
            WalkStep.Lapsing => Reason(
                $"its week resets first, at {CoolingWords.When(own.WeekResets!.Value, zone)}", OpeningWhy.Lapsing, ("at", NoteCodes.Moment(own.WeekResets.Value))),
            WalkStep.Pace => Pace(own.Said, over!, facts.GetValueOrDefault(over!)?.Said, now),
            WalkStep.LeastRecent => own is { Chosen: null, LastStarted: null }
                ? Reason("Daoris has not started on it yet", OpeningWhy.NotStarted)
                : Reason("Daoris started on it least recently", OpeningWhy.LeastRecent),
            _ => Listed(workspace, scope.Default is { } named && string.Equals(named, ran, StringComparison.OrdinalIgnoreCase)),
        };
    }

    private static StepSaid Reason(string text, string? why, params (string Name, object? Value)[] values) => new(text, why, values);

    /// <summary>
    /// The account the scope begins at, walked past (§3.3): <see cref="Why(string, AccountState, TimeZoneInfo)"/>'s clause, with
    /// whose account, which, and for a cool-off its reset and why it lasts until then. A scope never begins at the tool's own
    /// sign-in, and a ready account is never passed, so neither has a reason, and a line saying one keeps no parts.
    /// </summary>
    private static StepSaid Passed(string agent, AccountState state, TimeZoneInfo zone)
    {
        var text = Why(agent, state, zone);
        return state switch
        {
            { Readiness: AccountReadiness.Cooling, Cooling: { Account: { } account } cooling } => Reason(
                text, OpeningWhy.Cooling,
                ("agent", cooling.Agent), ("over", account), ("until", NoteCodes.Moment(cooling.Until)), ("cooling", CoolingWhy.Of(cooling))),
            { Readiness: AccountReadiness.Refused, Account: { } account } => Reason(text, OpeningWhy.Refused, ("agent", agent), ("over", account)),
            { Readiness: AccountReadiness.SignedOut, Account: { } account } => Reason(text, OpeningWhy.SignedOut, ("agent", agent), ("over", account)),
            _ => Reason(text, null),
        };
    }

    /// <summary>Why an account went last (§6): its agent's word, its credits, or a window's use at or over <i>near</i>.</summary>
    private static StepSaid Near(string account, AccountSaid? said, RotationUse use) => AccountReadings.NearWindow(said, use.Near) switch
    {
        { By: NearBy.Refused } reached => Reason(
            $"`{account}` said its {reached.Window.Window} limit is reached", OpeningWhy.NearReached, ("over", account), ("window", reached.Window.Window)),
        { By: NearBy.Word } warned => Reason(
            $"`{account}` said it is near its {warned.Window.Window} limit", OpeningWhy.NearWord, ("over", account), ("window", warned.Window.Window)),
        { By: NearBy.Credits } => Reason($"`{account}` said it is drawing on usage credits", OpeningWhy.NearCredits, ("over", account)),
        { } full => Reason(
            $"`{account}` has used {Percent(full.Window.Used!.Value)} of its {full.Window.Window} limit, at or over the {use.Near}% that counts as near",
            OpeningWhy.NearUsed, ("over", account), ("used", Whole(full.Window.Used!.Value)), ("window", full.Window.Window), ("near", use.Near)),
        null => Reason($"`{account}` is near its limit", OpeningWhy.Near, ("over", account)),
    };

    /// <summary>
    /// Why the account it ran on ranked ahead by its week's pace (§16.3 step 5): it is behind, where it said so; otherwise
    /// the account it was weighed against is ahead.
    /// </summary>
    private static StepSaid Pace(AccountSaid? own, string over, AccountSaid? other, DateTimeOffset now) =>
        AccountReadings.Behind(own, now) is > 0
            ? Reason($"it is furthest behind its week's pace, {Week(own!, now)}", OpeningWhy.Behind, Paced(own!, now))
            : Reason($"`{over}` is ahead of its week's pace, {Week(other!, now)}", OpeningWhy.Ahead, [("over", over), .. Paced(other!, now)]);

    /// <summary>A week's pace in words: <i>14% of its weekly limit used with 43% of its week gone</i>.</summary>
    private static string Week(AccountSaid said, DateTimeOffset now) =>
        $"{Percent(said.Of(AccountWindows.Weekly)!.Used!.Value)} of its weekly limit used with {Percent(AccountReadings.Gone(said, now)!.Value)} of its week gone";

    /// <summary><see cref="Week"/>'s two percents as values.</summary>
    private static (string Name, object? Value)[] Paced(AccountSaid said, DateTimeOffset now) =>
        [("used", Whole(said.Of(AccountWindows.Weekly)!.Used!.Value)), ("gone", Whole(AccountReadings.Gone(said, now)!.Value))];

    /// <summary>The scope's default or its list's first, a workspace's by name or this machine's.</summary>
    private static StepSaid Listed(string? workspace, bool named)
    {
        var text = named ? $"it is {Whose(workspace)} default" : $"it comes first in {Whose(workspace)} list";
        return workspace is { } name
            ? Reason(text, named ? OpeningWhy.Default : OpeningWhy.First, ("workspace", name))
            : Reason(text, named ? OpeningWhy.DefaultHere : OpeningWhy.FirstHere);
    }

    /// <summary>A scope's possessive: a workspace by name, or this machine.</summary>
    private static string Whose(string? workspace) => workspace is { } name ? $"`{name}`'s" : "this machine's";

    /// <summary>
    /// What a driven start's wait adds where the one account its kept-for-conversations rule passed is ready (§4.6).
    /// </summary>
    /// <param name="names">The owner's names by id, read as the wait is said (ACCT2b); null says the id.</param>
    public static string KeptAside(string kept, IReadOnlyDictionary<string, string>? names = null) =>
        $"`{AccountNames.Said(names, kept)}` is kept for conversations.";

    /// <summary>
    /// What a wait adds where the scope names accounts of its own and others are ready (§3.3): the accounts it does not
    /// list that are not cooling and not refused, and the door that adds the first, which the person answers and Daoris
    /// never does.
    /// </summary>
    /// <param name="listed">The accounts the scope may use: its list, or its one account where it names no list.</param>
    /// <param name="outside">The agent's other accounts, neither cooling nor refused, in name order.</param>
    /// <param name="names">
    /// The owner's names by id, read as the wait is said (ACCT2b): the door keeps the ids, each spelled for any shell, as its
    /// workspace is (ACCTQUOTE1b).
    /// </param>
    public static string Outside(
        string agent, string? workspace, IReadOnlyList<string> listed, IReadOnlyList<string> outside,
        IReadOnlyDictionary<string, string>? names = null)
    {
        var who = workspace is { } name ? $"`{name}`" : "this machine's starts";
        var door = $"daoris agent profile order {agent} {ShellWord.Words([.. listed, outside[0]], ShellWord.Account)}"
                   + (workspace is { } scoped ? $" --workspace {ShellWord.Of(scoped, ShellWord.Workspace)}" : "");
        return $"Not cooling, and not among the accounts {who} may use: {string.Join(", ", outside.Select(account => $"`{AccountNames.Said(names, account)}`"))} — "
               + $"Daoris starts nothing on them unless a list names them; `{door}` adds `{AccountNames.Said(names, outside[0])}`.";
    }

    /// <summary>One account in a wait's list: its name and why, short.</summary>
    private static string Clause(AccountState state, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names)
    {
        var account = state.Account is { } id ? AccountNames.Said(names, id) : null;
        return state.Readiness switch
        {
            AccountReadiness.Cooling => $"`{account}` is cooling until {CoolingWords.When(state.Cooling!.Until, zone)}",
            AccountReadiness.Refused => $"`{account}` was refused by its provider",
            AccountReadiness.SignedOut => $"`{account}` is not signed in",
            _ => $"`{account}` is ready",
        };
    }

    private static string Who(string agent, string? account) =>
        account is { } named ? $"the `{agent}` account `{named}`" : $"`{agent}`'s own sign-in";
}
