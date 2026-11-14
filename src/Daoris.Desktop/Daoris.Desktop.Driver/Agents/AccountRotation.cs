namespace Daoris.Driver;

/// <summary>
/// Which accounts a start may use, in the order it tries them, and which step chose the one it ran on. Pure: what Daoris knows of each account is handed in, and whether each is ready is
/// the roster's question, asked in this order.
/// </summary>
/// <remarks>
/// <para><b>One scope, its list the whole set</b>: only a scope with a list is walked. A pick, a
/// scope that names one account, and the tool's own sign-in are that one account, and the roster asks nothing here.</para>
///
/// <para><b>The goal</b> (<c>use: goal</c>, the default): every listed account is used toward the most work, the fewest
/// stalls and no allowance left at a reset, with what Daoris knows of its own and what each account's agent last said
/// about its windows, read against the scope's usage curve: near and room. <b>Nothing counts
/// accounts</b>: every rule reads the list it is handed, and while every account is ready none runs more than ⌈K ÷ N⌉ of a
/// cap of K. Under <c>use: order</c> the first ready account of the list carries every start, and
/// rotation moves work off a ready account only where it is near, under every switch.</para>
/// </remarks>
public static class AccountRotation
{
    /// <summary>
    /// An account its own reading puts at 100% of a window still ahead of its reset: cooling until the latest such reset, as
    /// its agent said, so no walk picks it while another can run, and a start with none waits for it. Held in memory: the reading is the record.
    /// </summary>
    public static CoolingEntry? Spent(string owner, string? account, AccountFacts? fact, DateTimeOffset now) =>
        account is not null
        && fact?.Said?.Windows.Where(window => window.Used is { } used && RotationWords.Whole(used) >= 100 && window.Reset > now)
            .MaxBy(window => window.Reset) is { } full
            ? new CoolingEntry(owner, account, full.Reset, Stated: true, full.Window, full.Seen, full.Session)
            : null;

    /// <summary>
    /// How near a week's reset must be for what would lapse there to rank its account first:
    /// <see cref="UsageCurves.LastDay"/>, which measures it.
    /// </summary>
    public static readonly TimeSpan LastDay = UsageCurves.LastDay;

    /// <summary>
    /// The accounts of a scope's list, in the order a start tries them.
    /// Pure: whether each is ready is the roster's question, asked in this order, and the first ready one runs.
    /// </summary>
    /// <remarks>
    /// <para>The sequence is the list begun where the scope begins (its default, else its first), wrapping: the account it
    /// begins at always, and every other only where the machine has it, since a start pointed at a directory nobody signed
    /// in to would fail where it should have walked on. Then the kept account: a driven start drops it; a
    /// conversation takes it last, or first where it is also the scope's default, and under <c>order</c> leaves it in its
    /// place.</para>
    /// <para>Under <c>goal</c> each step after keep is stable, so the order handed on stands where a step has no reason to
    /// change it: an account near (its limit reached, credits, or over its curve) goes last, the least over
    /// first; then within a week's <see cref="LastDay"/> the most of it that would lapse per hour to its reset
    /// (<see cref="UsageCurves.Lapsing"/>; the last day is under no curve); then the band of most room under its curve on
    /// its tightest window, fewest of Daoris's sessions running, the most room itself (one that said no number between those
    /// with room and those over; under <c>switch: off</c> furthest behind its week's pace), least recently started (never first, a
    /// start this look chose last), then the sequence. Under <c>order</c> the sequence is the walk, with a
    /// near account last, and a conversation's kept account in its place. Near is read under every switch: <c>off</c> reads
    /// the smart curve, since a start still passes an account over it.</para>
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
        IEnumerable<string> walked =
            sequence.OrderBy(name => name, Comparer<string>.Create((a, b) => Rank(a, b, facts, now, scope.Use) is var (_, order) && order != 0 ? order : position[a].CompareTo(position[b])));

        return [.. (first is null ? [] : new[] { first }), .. walked, .. (last is null ? [] : new[] { last })];
    }

    /// <summary>
    /// Which step chose the account at <paramref name="at"/> of <paramref name="tried"/>: the log's <c>why</c>, and what the start's first line says.
    /// </summary>
    /// <remarks>
    /// <para>A start that ran somewhere other than where its scope begins is moved off that account by a step: it is
    /// kept for conversations, it is not ready (cooling, refused, signed out), or a step of the goal ranks the account it
    /// ran on ahead of it. Where the first account is passed for itself, the step that chose among the rest is said too,
    /// unless the list's order did.</para>
    /// <para>A start that ran where its scope begins is chosen by the step that ranks it ahead of the account it would
    /// have run on next, or by the list's order where nothing else tells them apart or nothing else is ready.</para>
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
                _ when passed.Locked => new WalkChoice(WalkStep.Locked),
                _ => Ahead(ran, begins),
            };
        if (!goal || step.Step is not (WalkStep.Kept or WalkStep.Cooling or WalkStep.Refused or WalkStep.SignedOut or WalkStep.Locked)) return step;

        var rest = Next(ran, begins) is { } runnerUp ? Ahead(ran, runnerUp) : new WalkChoice(WalkStep.List);
        return rest.Step == WalkStep.List ? step : step with { Rest = rest.Step, RestOver = rest.Over };

        // The next ready account after the one it ran on, other than the one it moved off.
        string? Next(string after, string? but) =>
            tried.Skip(at + 1).FirstOrDefault(state => state.IsReady && (but is null || !Same(state.Account!, but)))?.Account;

        // The step that puts it ahead of another, naming that account where the step is about what its agent said.
        WalkChoice Ahead(string a, string b) => Rank(a, b, facts, now, scope.Use) is { Order: < 0 } ranked
            ? new WalkChoice(ranked.Step) { Over = ranked.Step is WalkStep.Near or WalkStep.Room or WalkStep.Headroom or WalkStep.Pace ? b : null }
            : new WalkChoice(WalkStep.List);
    }

    /// <summary>
    /// Which account the next start would take, the step that chose it, and what holds each other account:
    /// what the agent's page in Agents says, so a person sees why a start runs where it does.
    /// </summary>
    /// <remarks>
    /// <para>The account is the first ready one of <paramref name="tried"/>, as a start takes it. The reason is weighed as
    /// <see cref="Chose"/> weighs it, so the screen and the start's own first line name one step: against where the list
    /// begins, when that account is ready and a step moved the start off it; otherwise against the next ready account, by
    /// the step that told the two apart or the list's order. An account not ready, or kept for conversations, is a hold and
    /// never the reason. With no account ready the start waits, until the first cool-off ends where one is cooling.</para>
    /// <para>A scope with no list is its one account, or the tool's own sign-in where it names none, and no step
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
            // Between two near accounts the one with less room goes last: the page says the same of it as of any near account.
            { Step: WalkStep.Near or WalkStep.Room } => NextReason.Near,
            { Step: WalkStep.Headroom } => NextReason.Pace,
            { Step: WalkStep.Fewest } => NextReason.Fewest,
            { Step: WalkStep.Lapsing } => NextReason.Lapsing,
            { Step: WalkStep.Pace } => NextReason.Pace,
            { Step: WalkStep.LeastRecent } => Started(own).Tier == 0 ? NextReason.NotStarted : NextReason.LeastRecent,
            _ => NextReason.List,
        };
        // What would lapse, as the start's line says it (Lapses), from the one reading that ranked it.
        var lapse = reason == NextReason.Lapsing ? UsageCurves.Lapsing(scope.Use, own.Said, now)?.Window : null;
        return new NextStart(runs, reason, others)
        {
            Over = against,
            When = lapse?.Reset,
            Window = lapse?.Window,
            Unused = lapse?.Used is { } used ? RotationWords.Whole(1 - used) : null,
            Seen = lapse?.Seen,
        };

        AccountHeld Held(AccountState state) => state.Readiness switch
        {
            AccountReadiness.Cooling => new(state.Account, NextHold.Cooling, state.Cooling?.Until),
            AccountReadiness.Refused => new(state.Account, NextHold.Refused),
            AccountReadiness.SignedOut => new(state.Account, NextHold.SignedOut),
            _ => new(state.Account,
                listed && state.Account is { } name && AccountReadings.Near(facts.GetValueOrDefault(name)?.Said, scope.Use, now)
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
    /// edited by hand is read past, the list winning as it does over a default outside it.
    /// </summary>
    private static string? Keep(RotationScope scope, IReadOnlyList<string> among) =>
        scope.Use.Keep is { } keep && among.Any(name => Same(name, keep)) && among.Any(name => !Same(name, keep)) ? keep : null;

    /// <summary>The account a scope keeps for conversations, as the walk reads it; null where it keeps none.</summary>
    public static string? KeptOf(RotationScope scope) => Keep(scope, scope.List);

    /// <summary>
    /// The first step at which <paramref name="a"/> and <paramref name="b"/> differ, and which goes first there (negative:
    /// <paramref name="a"/>); <see cref="WalkStep.List"/> and zero where none does. Under <c>order</c> only near is a step.
    /// Every step compares a key of each account alone, so the ranking is a total preorder and a sort over it well defined.
    /// </summary>
    internal static (WalkStep Step, int Order) Rank(
        string a, string b, IReadOnlyDictionary<string, AccountFacts> facts, DateTimeOffset now, RotationUse use)
    {
        var (x, y) = (facts.GetValueOrDefault(a) ?? new AccountFacts(), facts.GetValueOrDefault(b) ?? new AccountFacts());

        // Step 2, over its curve last, with a limit reached and credits: read under every switch, `off` reading the smart curve, since a start still passes an account over it.
        var (nearX, nearY) = (AccountReadings.Near(x.Said, use, now), AccountReadings.Near(y.Said, use, now));
        if (nearX != nearY) return (WalkStep.Near, nearX ? 1 : -1);

        // Both near under the goal: the one least over its curve first, as a start left on a 99% account would otherwise
        // pass one at 60%. Equal, or neither known, the later steps decide.
        if (nearX && use.Use == "goal"
            && Room(x.Said, use, now).CompareTo(Room(y.Said, use, now)) is var room and not 0) return (WalkStep.Room, room);

        if (use.Use != "goal") return (WalkStep.List, 0);

        // What would lapse, before any room or session count: within its week's last day, the most room per hour to its
        // reset first, since another account's wider room is there tomorrow and this one's is lost at the reset;
        // nothing that is not known earns any.
        var (lapseX, lapseY) = (UsageCurves.Lapsing(use, x.Said, now)?.Rate ?? 0, UsageCurves.Lapsing(use, y.Said, now)?.Rate ?? 0);
        if (lapseX != lapseY) return (WalkStep.Lapsing, lapseY.CompareTo(lapseX));

        // Room before sessions run: a session started near the end of a window meets its limit within minutes. Not under `off`, which ranks by pace.
        if (use.Switch != "off" && RoomLeft.BandOf(use, x.Said, now) is var bandX && RoomLeft.BandOf(use, y.Said, now) is var bandY && bandX != bandY)
        {
            return (WalkStep.Headroom, bandY.CompareTo(bandX));
        }

        if (x.Running != y.Running) return (WalkStep.Fewest, x.Running.CompareTo(y.Running));

        // Step 5. An account that says no number ranks as one on its curve, between those with room and those over, and as
        // on pace under `off` (absent is never zero).
        var (spareX, spareY) = use.Switch == "off"
            ? (AccountReadings.Behind(x.Said, now) ?? 0, AccountReadings.Behind(y.Said, now) ?? 0)
            : (UsageCurves.TightestSaid(use, x.Said, now)?.Room ?? 0, UsageCurves.TightestSaid(use, y.Said, now)?.Room ?? 0);
        if (spareX != spareY) return (WalkStep.Pace, spareY.CompareTo(spareX));

        var (startedX, startedY) = (Started(x), Started(y));
        if (startedX != startedY) return (WalkStep.LeastRecent, startedX.CompareTo(startedY));

        return (WalkStep.List, 0);
    }

    /// <summary>
    /// How far over its curve a near account is, as the walk ranks it:
    /// one with a number below 100% by the points over its curve on its tightest window, the least over first, then one that
    /// said none (credits only), then one its word says reached or at 100%, which stays last whatever room its number leaves.
    /// </summary>
    private static (int Tier, double Over) Room(AccountSaid? said, RotationUse use, DateTimeOffset now) =>
        AccountReadings.NearWindow(said, use, now) is { By: NearBy.Refused } || AccountReadings.MostUsed(said) >= 1 ? (2, 0)
        : UsageCurves.TightestSaid(use, said, now) is { } tightest ? (0, -tightest.Room)
        : (1, 0);

    /// <summary>How recently Daoris started on it: never, then by when, then a start this look chose, in the order chosen.</summary>
    private static (int Tier, long At) Started(AccountFacts facts) =>
        facts.Chosen is { } chosen ? (2, chosen)
        : facts.LastStarted is { } started ? (1, started.UtcTicks)
        : (0, 0);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
