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

/// <summary>What a start is, for the walk (TOOL6b, D130 §4.6, §16.3): driven work never starts on a kept account.</summary>
public enum StartKind
{
    /// <summary>A driven start, a carry-on, a resume, an intake or a set-up.</summary>
    Driven,

    /// <summary>A conversation or Ask Daoris's opening.</summary>
    Conversation,
}

/// <summary>The step of the walk that chose a start's account (TOOL6b, D130 §13 as §16 amends it): the log's <c>why</c>.</summary>
public enum WalkStep { Cooling, Refused, SignedOut, Kept, Fewest, Lapsing, LeastRecent, List }

/// <summary>What Daoris knows of one account without its agent's word (TOOL6b, D130 §16.4).</summary>
public sealed record AccountFacts(int Running = 0, DateTimeOffset? LastStarted = null, long? Chosen = null, DateTimeOffset? WeekResets = null);

/// <summary>The step that chose a start's account, and the one that chose it among the rest where the first passed one.</summary>
public sealed record WalkChoice(WalkStep Step, WalkStep? Rest = null);

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
}

/// <summary>
/// The step of the goal's walk that chose a start's account, and what its record says first (TOOL6b, D130 §16.4).
/// Machine-local, as <see cref="RotatedStart"/> is: the clause may name accounts.
/// </summary>
/// <param name="Step">The step: the one that moved it off where its scope begins, or that ranked it ahead of the next.</param>
/// <param name="Clause">The step said for a person, and, where the first account was passed for itself, the step among the rest.</param>
public sealed record AccountChoice(WalkStep Step, string Clause);

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
/// stalls and no allowance left at a reset, with what Daoris knows without the agent's word (§16.4). <b>Nothing counts
/// accounts</b>: every rule reads the list it is handed, and while every account is ready none runs more than ⌈K ÷ N⌉ of a
/// cap of K (§7). Under <c>use: order</c> the first ready account of the list carries every start, as D125 built it, and
/// rotation never moves work off a ready account.</para>
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
    /// change it: fewest of Daoris's sessions running, a week resetting within <see cref="LastDay"/> (sooner first), least
    /// recently started (never first, a start this look chose last), then the sequence. Steps 2 and 5, near and pace,
    /// read the agent's own word, which no door carries yet (§16.4): TOOL6c slots them in. Under <c>order</c> the sequence
    /// is the walk, as D125 built it.</para>
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
        if (keep is not null)
        {
            sequence.RemoveAll(name => Same(name, keep));
            if (kind == StartKind.Conversation)
            {
                if (!goal) return Sequence(scope, present);
                if (scope.Default is { } own && Same(own, keep)) first = keep;
                else last = keep;
            }
        }

        var position = sequence.Select((name, index) => (name, index)).ToDictionary(pair => pair.name, pair => pair.index, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> walked = goal
            ? sequence.OrderBy(name => name, Comparer<string>.Create((a, b) => Rank(a, b, facts, now) is var (_, order) && order != 0 ? order : position[a].CompareTo(position[b])))
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
            return new(goal && Next(ran, null) is { } after ? Ahead(ran, after, facts, now) : WalkStep.List);
        }

        var passed = tried.FirstOrDefault(state => Same(state.Account!, begins));
        var step = passed is null || (conversationKeeps && Same(begins, keep!))
            ? WalkStep.Kept
            : passed.Readiness switch
            {
                AccountReadiness.Cooling => WalkStep.Cooling,
                AccountReadiness.Refused => WalkStep.Refused,
                AccountReadiness.SignedOut => WalkStep.SignedOut,
                _ => Ahead(ran, begins, facts, now),
            };
        if (!goal || step is not (WalkStep.Kept or WalkStep.Cooling or WalkStep.Refused or WalkStep.SignedOut)) return new(step);

        var rest = Next(ran, begins) is { } runnerUp ? Ahead(ran, runnerUp, facts, now) : WalkStep.List;
        return new(step, rest == WalkStep.List ? null : rest);

        // The next ready account after the one it ran on, but the one it was moved off.
        string? Next(string after, string? but) =>
            tried.Skip(at + 1).FirstOrDefault(state => state.IsReady && (but is null || !Same(state.Account!, but)))?.Account;
    }

    /// <summary>The step of the goal that puts <paramref name="a"/> ahead of <paramref name="b"/>; the list's order where none does.</summary>
    private static WalkStep Ahead(string a, string b, IReadOnlyDictionary<string, AccountFacts> facts, DateTimeOffset now) =>
        Rank(a, b, facts, now) is { Order: < 0 } ranked ? ranked.Step : WalkStep.List;

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

    /// <summary>
    /// The first step of the goal at which <paramref name="a"/> and <paramref name="b"/> differ, and which goes first there
    /// (negative: <paramref name="a"/>); <see cref="WalkStep.List"/> and zero where none does.
    /// </summary>
    private static (WalkStep Step, int Order) Rank(string a, string b, IReadOnlyDictionary<string, AccountFacts> facts, DateTimeOffset now)
    {
        var (x, y) = (facts.GetValueOrDefault(a) ?? new AccountFacts(), facts.GetValueOrDefault(b) ?? new AccountFacts());

        if (x.Running != y.Running) return (WalkStep.Fewest, x.Running.CompareTo(y.Running));

        var (lapsesX, lapsesY) = (Lapses(x, now), Lapses(y, now));
        if (lapsesX != lapsesY) return (WalkStep.Lapsing, lapsesX.CompareTo(lapsesY));

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
    /// Say it: under the goal, every start whose list offered a choice names the step that chose its account and that no
    /// account has said what it has left; under <c>order</c>, a start that rotated says why, as D125 built it; otherwise
    /// nothing. The record's failure costs its line, never the start.
    /// </summary>
    internal static void Say(
        ServiceClient service, SessionEvents events, string sessionId, string adapter, HarnessSelection selection, Carried? carried)
    {
        if (selection.Profile is not { } to) return;

        var line = (selection.Choice, selection.Rotated) switch
        {
            ({ } choice, _) => (carried is { } cut
                ? RotationWords.CarriedOn(cut.Session, to, choice.Clause, cut.Turn, cut.Used)
                : RotationWords.Opened(to, choice.Clause)) + " " + RotationWords.Unsaid,
            (null, { } rotated) => carried is { } cut
                ? RotationWords.CarriedOn(cut.Session, to, rotated, cut.Turn, cut.Used)
                : RotationWords.Opened(to, rotated),
            _ => null,
        };
        if (line is null) return;

        // The conversation record is the machine's (D76), so this line may name both accounts; the note, which
        // travels, never does (§3.6).
        events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line }, say: null);
        if (selection.Rotated is { } moved)
        {
            service.AccountSaid(AccountLine.Rotated(sessionId, adapter, moved.From, to, carried?.Session, moved.Step, moved.Scope));
        }
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
/// </remarks>
public static class RotationWords
{
    /// <summary>
    /// Every account a start may use is not ready, and one at least is cooling (§4): the start waits for the first
    /// reset. Every account cooling is said as such; otherwise each account and why.
    /// </summary>
    public static string Wait(string agent, IReadOnlyList<AccountState> states, TimeZoneInfo zone)
    {
        var first = states.Where(state => state.Cooling is not null).Select(state => state.Cooling!).MinBy(cooling => cooling.Until)!;
        var when = $"the first ready, `{first.Account}`, at {CoolingWords.When(first.Until, zone)}, {CoolingWords.Why(first)}";
        var every = states.All(state => state.Readiness == AccountReadiness.Cooling)
            ? $"every `{agent}` account this start may use is cooling"
            : $"no `{agent}` account this start may use is ready: {string.Join(", ", states.Select(state => Clause(state, zone)))}";
        return $"{every}; {when}. Daoris starts nothing on them until then.";
    }

    /// <summary>
    /// A conversation the person started on an account they picked, which is cooling (§3.3): refused, since the person
    /// chose it, with the accounts that are ready by name.
    /// </summary>
    public static string Picked(CoolingEntry cooling, IReadOnlyList<string> ready, TimeZoneInfo zone) =>
        $"{CoolingWords.Hold(cooling, zone)} "
        + (ready.Count == 0
            ? $"No other `{cooling.Agent}` account is ready."
            : $"Ready now: {string.Join(", ", ready.Select(name => $"`{name}`"))}.");

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
    public static string CarriedOn(string cutOff, string to, string why, long? turn, long? used)
    {
        var refused = turn is { } number
            ? $"; its turn {number} was refused"
              + (used is { } context ? $" with {context.ToString("N0", CultureInfo.InvariantCulture)} tokens of context" : "")
            : "";
        return $"carried on from session `{cutOff}` on `{to}`; {why}{refused}.";
    }

    /// <summary>
    /// What every start the goal chose says after its step (§16.4): no door of Daoris's carries the agent's word about its
    /// windows yet, so near and pace wait for it (TOOL6c reads it, and then says this only while it holds).
    /// </summary>
    public const string Unsaid = "No account has said what it has left yet.";

    /// <summary>
    /// Which step chose the account a start ran on, as a clause (§16.4): the step that moved it off where its scope begins
    /// or ranked it first, and, where the first account was passed for itself, the step that chose among the rest.
    /// </summary>
    /// <param name="workspace">The workspace whose list it was, or null for this machine's.</param>
    /// <param name="passed">The state the start found the scope's first account in, where it was walked past.</param>
    public static string Clause(
        WalkChoice choice, string agent, string ran, RotationScope scope, string? workspace, AccountState? passed,
        AccountFacts facts, StartKind kind, TimeZoneInfo zone)
    {
        var lead = Step(choice.Step, agent, ran, scope, workspace, passed, facts, kind, zone);
        return choice.Rest is { } rest ? $"{lead}; of the rest, {Step(rest, agent, ran, scope, workspace, passed, facts, kind, zone)}" : lead;
    }

    private static string Step(
        WalkStep step, string agent, string ran, RotationScope scope, string? workspace, AccountState? passed, AccountFacts facts,
        StartKind kind, TimeZoneInfo zone) => step switch
    {
        WalkStep.Cooling or WalkStep.Refused or WalkStep.SignedOut => Why(agent, passed!, zone),
        WalkStep.Kept when string.Equals(ran, scope.Begins, StringComparison.OrdinalIgnoreCase) => "it is kept for conversations",
        WalkStep.Kept when kind == StartKind.Conversation => $"`{scope.Begins}` is kept for conversations, which take it last",
        WalkStep.Kept => $"`{scope.Begins}` is kept for conversations",
        WalkStep.Fewest => "it runs the fewest of Daoris's sessions",
        WalkStep.Lapsing => $"its week resets first, at {CoolingWords.When(facts.WeekResets!.Value, zone)}",
        WalkStep.LeastRecent => facts is { Chosen: null, LastStarted: null } ? "Daoris has not started on it yet" : "Daoris started on it least recently",
        _ => scope.Default is { } own && string.Equals(own, ran, StringComparison.OrdinalIgnoreCase)
            ? $"it is {Whose(workspace)} default"
            : $"it comes first in {Whose(workspace)} list",
    };

    /// <summary>A scope's possessive: a workspace by name, or this machine.</summary>
    private static string Whose(string? workspace) => workspace is { } name ? $"`{name}`'s" : "this machine's";

    /// <summary>
    /// What a driven start's wait adds where the one account its kept-for-conversations rule passed is ready (§4.6).
    /// </summary>
    public static string KeptAside(string kept) => $"`{kept}` is kept for conversations.";

    /// <summary>
    /// What a wait adds where the scope names accounts of its own and others are ready (§3.3): the accounts it does not
    /// list that are not cooling and not refused, and the door that adds the first, which the person answers and Daoris
    /// never does.
    /// </summary>
    /// <param name="listed">The accounts the scope may use: its list, or its one account where it names no list.</param>
    /// <param name="outside">The agent's other accounts, neither cooling nor refused, in name order.</param>
    public static string Outside(string agent, string? workspace, IReadOnlyList<string> listed, IReadOnlyList<string> outside)
    {
        var who = workspace is { } name ? $"`{name}`" : "this machine's starts";
        var door = $"daoris agent profile order {agent} {string.Join(' ', [.. listed, outside[0]])}"
                   + (workspace is { } scoped ? $" --workspace {scoped}" : "");
        return $"Not cooling, and not among the accounts {who} may use: {string.Join(", ", outside.Select(account => $"`{account}`"))} — "
               + $"Daoris starts nothing on them unless a list names them; `{door}` adds `{outside[0]}`.";
    }

    /// <summary>One account in a wait's list: its name and why, short.</summary>
    private static string Clause(AccountState state, TimeZoneInfo zone) => state.Readiness switch
    {
        AccountReadiness.Cooling => $"`{state.Account}` is cooling until {CoolingWords.When(state.Cooling!.Until, zone)}",
        AccountReadiness.Refused => $"`{state.Account}` was refused by its provider",
        AccountReadiness.SignedOut => $"`{state.Account}` is not signed in",
        _ => $"`{state.Account}` is ready",
    };

    private static string Who(string agent, string? account) =>
        account is { } named ? $"the `{agent}` account `{named}`" : $"`{agent}`'s own sign-in";
}
