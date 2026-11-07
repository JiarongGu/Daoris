using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What rotation says: a wait over an order, a pick refused, and the opening line of a
/// rotated start's record. The driver's own sentences, rendered verbatim like every sentence it writes; the
/// names follow the glossary: an account, signed in, the tool's own sign-in.
/// </summary>
/// <remarks>
/// These name accounts, so each is said only where this machine reads it: a start's hold, a conversation's refusal, the
/// conversation record. 🔴 None of them goes into a session's note, which travels, and whose scrubber knows only
/// the record's own account.
/// <para>What a person reads names an account by the name they gave it, from the owner's names
/// read as the sentence is said, else by its id: a wait, a hold, a pick refused, what a wait adds. A command in a sentence
/// keeps the id, which a terminal takes whatever the account is called by then; and the record's opening line
/// (<see cref="Opened(string, string)"/>, <see cref="CarriedOn(string, string, string, long?, long?)"/>, <see cref="Clause"/>)
/// keeps the ids, so a record is read back to its account after a rename.</para>
/// </remarks>
public static class RotationWords
{
    /// <summary>
    /// Every account a start may use is not ready, and one at least is cooling: the start waits for the first
    /// reset. Every account cooling is said as such; otherwise each account and why.
    /// </summary>
    /// <param name="names">The owner's names by id, read as the wait is said; null says each by its id.</param>
    public static string Wait(
        string agent, IReadOnlyList<AccountState> states, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null)
    {
        var first = LineAccount.Earliest(ResetSet.Walk, states)!;
        var when = $"the first ready, `{(first.Account is { } account ? AccountNames.Said(names, account) : null)}`, at "
                   + $"{CoolingWords.When(first.Until, zone)}, {CoolingWords.Why(first)}";
        var every = states.All(state => state.Readiness == AccountReadiness.Cooling)
            ? $"every `{agent}` account this start may use is cooling"
            : $"no `{agent}` account this start may use is ready: {string.Join(", ", states.Select(state => Clause(state, zone, names)))}";
        // An account not signed in waits for a person, never for the reset, so its sign-in is said beside the wait.
        return $"{every}; {when}. Daoris starts nothing on them until then." + SignIn(agent, SignedOut(states), "A sign-in starts it sooner");
    }

    /// <summary>
    /// <see cref="Wait"/> by code, its English the same to the byte: the lead, a clause for each account by its id
    /// (the page names it), the first reset, and the sign-in that would start it sooner. Null where an account the wait lists
    /// has no id (the tool's own sign-in), which a clause cannot say: the sentence then keeps its words.
    /// </summary>
    public static Noted? WaitOf(
        string agent, IReadOnlyList<AccountState> states, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null)
    {
        var first = LineAccount.Earliest(ResetSet.Walk, states)!;
        if (first.Account is not { } firstAccount) return null;

        var every = states.All(state => state.Readiness == AccountReadiness.Cooling);
        var lead = every
            ? Noted.Of(HeldNoteCodes.HeldWaitEvery, $"every `{agent}` account this start may use is cooling", ("agent", agent))
            : ClausesOf(HeldNoteCodes.HeldWaitSome, $"no `{agent}` account this start may use is ready:", agent, states, zone, names);
        if (lead is null) return null;

        var said = $"the first ready, `{AccountNames.Said(names, firstAccount)}`, at {CoolingWords.When(first.Until, zone)}, "
                   + $"{CoolingWords.Why(first)}. Daoris starts nothing on them until then.";
        var wait = lead.Then("; ", Noted.Of(
            HeldNoteCodes.HeldWaitFirst, said, ("account", firstAccount), ("until", NoteCodes.Moment(first.Until)), ("why", CoolingWhy.Of(first))));
        return SignInOf(wait, agent, SignedOut(states), "A sign-in starts it sooner", HeldNoteCodes.HeldSignInSooner);
    }

    /// <summary>
    /// No account a start may use is ready and none is cooling, over a list of more than one: nothing comes ready
    /// by itself, so each account is named with why, and every one not signed in with its sign-in.
    /// </summary>
    /// <param name="names">The owner's names by id, read as the hold is said; null says each by its id.</param>
    public static string NoneReady(string agent, IReadOnlyList<AccountState> states, IReadOnlyDictionary<string, string>? names = null) =>
        $"no `{agent}` account this start may use is ready: {string.Join(", ", states.Select(state => Clause(state, TimeZoneInfo.Utc, names)))}."
        + SignIn(agent, SignedOut(states), "A sign-in starts it");

    /// <summary><see cref="NoneReady"/> by code, its English the same to the byte; null where an account has no id.</summary>
    public static Noted? NoneReadyOf(string agent, IReadOnlyList<AccountState> states, IReadOnlyDictionary<string, string>? names = null)
    {
        var none = ClausesOf(HeldNoteCodes.HeldWaitSome, $"no `{agent}` account this start may use is ready:", agent, states, TimeZoneInfo.Utc, names);
        return none is null ? null : SignInOf(none.Then("."), agent, SignedOut(states), "A sign-in starts it", HeldNoteCodes.HeldSignIn);
    }

    /// <summary>The lead, then each account's clause joined as the English joins them; null where an account has no id.</summary>
    private static Noted? ClausesOf(
        NoteCode lead, string text, string agent, IReadOnlyList<AccountState> states, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names)
    {
        var noted = Noted.Of(lead, text, ("agent", agent));
        for (var at = 0; at < states.Count; at++)
        {
            if (states[at].Account is not { } id) return null;

            var clause = Clause(states[at], zone, names);
            var line = states[at].Readiness switch
            {
                AccountReadiness.Cooling => Noted.Of(HeldNoteCodes.HeldClauseCooling, clause, ("account", id), ("until", NoteCodes.Moment(states[at].Cooling!.Until))),
                AccountReadiness.Refused => Noted.Of(HeldNoteCodes.HeldClauseRefused, clause, ("account", id)),
                AccountReadiness.SignedOut => Noted.Of(HeldNoteCodes.HeldClauseSignedOut, clause, ("account", id)),
                _ => Noted.Of(HeldNoteCodes.HeldClauseReady, clause, ("account", id)),
            };
            noted = noted.Then(at == 0 ? " " : ", ", line);
        }

        return noted;
    }

    /// <summary><see cref="SignIn"/> as a line after <paramref name="wait"/>: each command is a value of its list.</summary>
    internal static Noted SignInOf(Noted wait, string agent, IReadOnlyList<string> accounts, string lead, NoteCode code) => accounts.Count == 0
        ? wait
        : wait.Then(" ", Noted.Of(
            code, SignIn(agent, accounts, lead)[1..],
            ("logins", (IReadOnlyList<string>)[.. accounts.Select(account => $"daoris agent login {agent} --account {ShellWord.Of(account, ShellWord.Account)}")])));

    /// <summary>
    /// A start whose scope names no account, with no Daoris account of the agent's signed in: it waits
    /// for the person's sign-in, said once as any wait is said, and never falls back to the person's own tool home. One
    /// coded line, the agent its value; both doors to a sign-in named, the window's and the terminal's.
    /// </summary>
    public static Noted NoAccountOf(string agent) => Noted.Of(
        HeldNoteCodes.HeldNoAccount,
        $"No `{agent}` account of Daoris's own is signed in, and Daoris never starts its sessions in your own `{agent}` sign-in — "
        + $"sign one in for Daoris on the agent's page under Agents, or with `daoris agent login {agent} --new`. The start waits until then.",
        ("owner", agent));

    /// <summary>The accounts of a walk not signed in, in the order it tried them.</summary>
    public static IReadOnlyList<string> SignedOut(IReadOnlyList<AccountState> states) =>
        [.. states.Where(state => state.Readiness == AccountReadiness.SignedOut && state.Account is not null).Select(state => state.Account!)];

    /// <summary>
    /// The sign-in for each account not signed in, after <paramref name="lead"/>, both doors named: the terminal's for
    /// each account, and the screen's, the agent's page in the Agents place. Empty where none is.
    /// Each id is spelled for any shell.
    /// </summary>
    private static string SignIn(string agent, IReadOnlyList<string> accounts, string lead) => accounts.Count == 0
        ? ""
        : $" {lead}: {string.Join(", ", accounts.Select(account => $"`daoris agent login {agent} --account {ShellWord.Of(account, ShellWord.Account)}`"))}, "
          + "or Agents → the agent's page → Accounts.";

    /// <summary>
    /// A conversation the person started on an account they picked, which is cooling: refused, since the person
    /// chose it, with the accounts that are ready by name.
    /// </summary>
    /// <param name="names">The owner's names by id, read as the refusal is said; null says each by its id.</param>
    public static string Picked(
        CoolingEntry cooling, IReadOnlyList<string> ready, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null) =>
        PickedOf(cooling, ready, zone, names).Note;

    /// <summary><see cref="Picked"/> by code: the hold's own parts, then who is ready as a list the page names.</summary>
    public static Noted PickedOf(
        CoolingEntry cooling, IReadOnlyList<string> ready, TimeZoneInfo zone, IReadOnlyDictionary<string, string>? names = null) =>
        CoolingWords.HoldOf(cooling, zone, names).Then(" ", ready.Count == 0
            ? Noted.Of(HeldNoteCodes.HeldPickedNone, $"No other `{cooling.Agent}` account is ready.", ("agent", cooling.Agent))
            : Noted.Of(
                HeldNoteCodes.HeldPickedReady, $"Ready now: {string.Join(", ", ready.Select(name => $"`{AccountNames.Said(names, name)}`"))}.",
                ("accounts", (IReadOnlyList<string>)[.. ready])));

    /// <summary>Why an account is not ready, as a clause: the account named, and the time where it waits for one.</summary>
    public static string Why(string agent, AccountState state, TimeZoneInfo zone) => state.Readiness switch
    {
        AccountReadiness.Cooling => $"{CoolingWords.Who(state.Cooling!)} is cooling until {CoolingWords.When(state.Cooling!.Until, zone)}, {CoolingWords.Why(state.Cooling)}",
        AccountReadiness.Refused => $"{Who(agent, state.Account)} was refused by its provider",
        AccountReadiness.SignedOut => $"{Who(agent, state.Account)} is not signed in",
        _ => $"{Who(agent, state.Account)} is ready",
    };

    /// <summary>The first line of a rotated start's record: which account it opened on, and why.</summary>
    public static string Opened(string to, RotatedStart rotated) => Opened(to, rotated.Why);

    /// <summary>The first line of a start's record: which account it opened on, and the clause that says why.</summary>
    public static string Opened(string to, string why) => $"opened on `{to}`: {why}.";

    /// <summary>
    /// The first line of a rotated carry-on's record: the cut-off session it carries on, the account it runs on,
    /// why its default is not ready, and, after a limit, the turn that is refused and the context it has.
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
    /// What every start the goal chose says after its step while no account of its list has said what it has left:
    /// near and pace then wait for an agent's word (read where a door carries it, and <see cref="Said"/> says it).
    /// </summary>
    public const string Unsaid = "No account has said what it has left yet.";

    /// <summary>
    /// What each account of a start's list last said about its windows, as one sentence for the start's first line:
    /// in the list's order, each with its age and what it said; null where none has said anything, so the
    /// start says <see cref="Unsaid"/> instead. An account that said nothing is said so: unknown, never empty or full.
    /// </summary>
    /// <param name="accounts">The accounts the start's walk could use, in the list's order.</param>
    public static string? Said(IReadOnlyList<string> accounts, IReadOnlyDictionary<string, AccountFacts> facts, RotationUse use, DateTimeOffset now) =>
        AccountsSaid(accounts, facts, use, now)?.Note;

    /// <summary>
    /// <see cref="Said"/>'s sentence with its lines by code: a line per account, its name and when it said it or that
    /// it said nothing, and one per thing it said, each line's English inside the sentence, which is unchanged.
    /// </summary>
    public static Noted? AccountsSaid(IReadOnlyList<string> accounts, IReadOnlyDictionary<string, AccountFacts> facts, RotationUse use, DateTimeOffset now)
    {
        if (!accounts.Any(account => facts.GetValueOrDefault(account)?.Said is not null)) return null;
        var each = Joined([.. accounts.Select(account => Account(account, facts.GetValueOrDefault(account)?.Said, use, now, facts.GetValueOrDefault(account)?.Projected))], "; ");
        return new Noted("What each account said: ", []).Then("", each).Then(".");
    }

    /// <summary>
    /// One account in <see cref="Said"/>: its name, how long ago, and what it said, in Daoris's words and by code; and where
    /// Daoris's own turns since moved its projection, each window's projected use, said as projected.
    /// </summary>
    private static Noted Account(string name, AccountSaid? said, RotationUse use, DateTimeOffset now, AccountProjection? projected = null)
    {
        if (said is null) return Noted.Of(StartNoteCodes.OpeningSaidNothing, $"`{name}` nothing yet", ("account", name));

        var parts = new List<Noted>();
        if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.RefusedWord) is { } reached)
        {
            parts.Add(Noted.Of(StartNoteCodes.OpeningSaidReached, $"its {reached.Window} limit reached, by its own word", ("window", reached.Window)));
        }
        else if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.WarnedWord) is { } warned)
        {
            // Its own pace warning, in its own words, never near: `opening.said-near` said it so before.
            parts.Add(Noted.Of(
                StartNoteCodes.OpeningSaidWarned, $"ahead of its pace on its {warned.Window} window, by its own word", ("window", warned.Window)));
        }

        if (said.Windows.Any(each => each.Credits)) parts.Add(Noted.Of(StartNoteCodes.OpeningSaidCredits, "drawing on usage credits"));
        var used = Ordered(said).Where(each => each.Used is not null)
            .Select(each => Noted.Of(
                StartNoteCodes.OpeningSaidUsed, $"{Percent(each.Used!.Value)} of its {each.Window} limit", ("used", Whole(each.Used!.Value)), ("window", each.Window)))
            .ToList();
        if (used.Count > 0) parts.Add(Joined(used, " and ").Then(" used"));
        // Over its curve on its tightest window, and the curve's value there now: `opening.said-near-at`,
        // *near at 90%*, said it by the scope's one number before.
        if (UsageCurves.TightestSaid(use, said, now) is { Room: < 0 } over)
        {
            parts.Add(Noted.Of(
                StartNoteCodes.OpeningSaidOver, $"over its {over.Window.Window} curve's {Points(over.Curve)}%", ("window", over.Window.Window), ("curve", Points(over.Curve))));
        }
        // What it is projected to have used by now, where Daoris's turns since moved it a whole percent.
        foreach (var window in (projected?.Windows ?? []).Where(each => each.How == ProjectedBy.Measured && Whole(each.Projected) > Whole(each.Read))
                     .OrderBy(each => each.Window.ToLowerInvariant() switch { "session" => 0, AccountWindows.Weekly => 1, _ => 2 }))
        {
            parts.Add(Noted.Of(
                StartNoteCodes.OpeningSaidProjected, $"about {Percent(window.Projected)} of its {window.Window} limit by now, projected from Daoris's turns since",
                ("projected", Whole(window.Projected)), ("window", window.Window)));
        }
        // A reading with no number, no warning and no credits is its agent's clear word alone.
        if (parts.Count == 0) parts.Add(Noted.Of(StartNoteCodes.OpeningSaidClear, "clear, by its own word"));
        var who = Noted.Of(StartNoteCodes.OpeningSaidAt, $"`{name}` {Age(said.Seen, now)}", ("account", name), ("seen", NoteCodes.Moment(said.Seen)));
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

    /// <summary>A share as the whole percent <see cref="Percent"/> says, as a value a part carries: <i>88</i>.</summary>
    internal static int Whole(double share) => (int)Math.Round(share * 100, MidpointRounding.AwayFromZero);

    /// <summary>A curve's value or a room, already in percent points, as a whole number a part carries: <i>63</i>.</summary>
    internal static int Points(double points) => UsageCurves.Points(points);

    /// <summary>How long ago something is said: <i>just now</i>, <i>20 min ago</i>, <i>3 h ago</i>, <i>2 d ago</i>.</summary>
    internal static string Age(DateTimeOffset seen, DateTimeOffset now)
    {
        var minutes = (long)Math.Floor((now - seen).TotalMinutes);
        return minutes < 1 ? "just now"
            : minutes < 60 ? $"{minutes} min ago"
            : minutes < 1440 ? $"{minutes / 60} h ago"
            : $"{minutes / 1440} d ago";
    }

    /// <summary>
    /// Which step chose the account a start ran on, as a clause: the step that moved it off where its scope begins
    /// or ranked it first, and, where the first account is passed for itself, the step that chose among the rest. Near and
    /// pace say what the account they weighed it against said.
    /// </summary>
    /// <param name="workspace">The workspace whose list it is, or null for this machine's.</param>
    /// <param name="passed">The state the start finds the scope's first account in, where it is walked past.</param>
    public static string Clause(
        WalkChoice choice, string agent, string ran, RotationScope scope, string? workspace, AccountState? passed,
        IReadOnlyDictionary<string, AccountFacts> facts, StartKind kind, DateTimeOffset now, TimeZoneInfo zone) =>
        Text(Steps(choice, agent, ran, scope, workspace, passed, facts, kind, now, zone));

    /// <summary>
    /// <see cref="Clause"/>'s steps one by one, each its English and its reason by code with its own values:
    /// the step that chose the account, then, where the first account is passed for itself, the step among
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
            WalkStep.Cooling or WalkStep.Refused or WalkStep.SignedOut or WalkStep.Locked => Passed(agent, passed!, zone),
            WalkStep.Kept when string.Equals(ran, scope.Begins, StringComparison.OrdinalIgnoreCase) => Reason("it is kept for conversations", OpeningWhy.KeptSelf),
            WalkStep.Kept when kind == StartKind.Conversation =>
                Reason($"`{scope.Begins}` is kept for conversations, which take it last", OpeningWhy.KeptLast, ("over", scope.Begins)),
            WalkStep.Kept => Reason($"`{scope.Begins}` is kept for conversations", OpeningWhy.Kept, ("over", scope.Begins)),
            // Both near, it the least over: the other's own reason for going last, its numbers against its curve.
            WalkStep.Near or WalkStep.Room => Near(over!, facts.GetValueOrDefault(over!)?.Said, scope.Use, now),
            WalkStep.Fewest => Reason("it runs the fewest of Daoris's sessions", OpeningWhy.Fewest),
            WalkStep.Lapsing => Lapses(own.Said, scope.Use, now),
            WalkStep.Pace when scope.Use.Switch == "off" => Pace(own.Said, over!, facts.GetValueOrDefault(over!)?.Said, now),
            WalkStep.Pace => MostRoom(own.Said, over!, facts.GetValueOrDefault(over!)?.Said, scope.Use, now),
            WalkStep.LeastRecent => own is { Chosen: null, LastStarted: null }
                ? Reason("Daoris has not started on it yet", OpeningWhy.NotStarted)
                : Reason("Daoris started on it least recently", OpeningWhy.LeastRecent),
            _ => Listed(workspace, scope.Default is { } named && string.Equals(named, ran, StringComparison.OrdinalIgnoreCase)),
        };
    }

    private static StepSaid Reason(string text, string? why, params (string Name, object? Value)[] values) => new(text, why, values);

    /// <summary>
    /// The account the scope begins at, walked past: <see cref="Why(string, AccountState, TimeZoneInfo)"/>'s clause, with
    /// whose account, which, and for a cool-off its reset and why it lasts until then. A scope never begins at the tool's own
    /// sign-in, and a ready account is never passed, so neither has a reason, and a line saying one keeps no parts.
    /// </summary>
    private static StepSaid Passed(string agent, AccountState state, TimeZoneInfo zone)
    {
        // Passed for its lock it is still ready, so `Why` would call it ready.
        if (state is { Readiness: AccountReadiness.Ready, Locked: true, Account: { } locked })
        {
            return Reason(
                $"{Who(agent, locked)} is being asked about by another question", OpeningWhy.Locked, ("agent", agent), ("over", locked));
        }

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

    /// <summary>
    /// Why an account went last: its agent's word that a limit is reached, its credits, or its use over
    /// its curve on its tightest window, said with the curve's value there now; never its pace warning, which
    /// is not near.
    /// </summary>
    private static StepSaid Near(string account, AccountSaid? said, RotationUse use, DateTimeOffset now) => AccountReadings.NearWindow(said, use, now) switch
    {
        { By: NearBy.Refused } reached => Reason(
            $"`{account}` said its {reached.Window.Window} limit is reached", OpeningWhy.NearReached, ("over", account), ("window", reached.Window.Window)),
        { By: NearBy.Credits } => Reason($"`{account}` said it is drawing on usage credits", OpeningWhy.NearCredits, ("over", account)),
        { } => OverCurve(account, UsageCurves.TightestSaid(use, said, now)!.Value),
        null => Reason($"`{account}` is near its limit", OpeningWhy.Near, ("over", account)),
    };

    /// <summary>An account over its curve, said by its tightest window: what it used there and the curve's value there now.</summary>
    private static StepSaid OverCurve(string account, (WindowSaid Window, double Curve, double Room) over) => Reason(
        $"`{account}` has used {Percent(over.Window.Used!.Value)} of its {over.Window.Window} limit, over its curve's {Points(over.Curve)}%",
        OpeningWhy.OverCurve, ("over", account), ("window", over.Window.Window), ("used", Whole(over.Window.Used!.Value)), ("curve", Points(over.Curve)));

    /// <summary>
    /// Why the account it ran on ranked ahead by its room (step 5 under <c>smart</c> and <c>curve</c>):
    /// the most room under its curve on its tightest window, where it has room to say; otherwise the account it is weighed
    /// against is over its curve, or near by its word, and that is said.
    /// </summary>
    private static StepSaid MostRoom(AccountSaid? own, string over, AccountSaid? other, RotationUse use, DateTimeOffset now) =>
        UsageCurves.TightestSaid(use, own, now) is { Room: > 0 } mine
            ? Reason(
                $"it has the most room under its curve, {Points(mine.Room)} points on its {mine.Window.Window} window",
                OpeningWhy.MostRoom, ("room", Points(mine.Room)), ("window", mine.Window.Window))
            : Near(over, other, use, now);

    /// <summary>
    /// Why the account it ran on ranked ahead by what would lapse (step 4): the window that won, how much of it is
    /// unused, when it resets, and how old that reading is. A step this account's reading cannot say keeps the line's English with no parts.
    /// </summary>
    private static StepSaid Lapses(AccountSaid? own, RotationUse use, DateTimeOffset now) =>
        UsageCurves.Lapsing(use, own, now) is { Window: { Used: { } used } week }
            ? Reason(
                $"its {week.Window} window resets in {Until(week.Reset - now)} with {Percent(1 - used)} of it unused, read {Age(week.Seen, now)}",
                OpeningWhy.Lapses,
                ("window", week.Window), ("unused", Whole(1 - used)), ("at", NoteCodes.Moment(week.Reset)), ("seen", NoteCodes.Moment(week.Seen)))
            : Reason("its week resets first", null);

    /// <summary>How long until a moment, as a reset is said: <i>40 min</i>, <i>8 h</i> (the nearest hour).</summary>
    private static string Until(TimeSpan left)
    {
        var minutes = (long)Math.Floor(left.TotalMinutes);
        return minutes < 1 ? "less than a minute"
            : minutes < 60 ? $"{minutes} min"
            : $"{(long)Math.Round(left.TotalHours, MidpointRounding.AwayFromZero)} h";
    }

    /// <summary>
    /// Why the account it ran on ranked ahead by its week's pace (step 5, under <c>switch: off</c>): it is behind, where
    /// it said so; otherwise the account it is weighed against is ahead.
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

    /// <summary>
    /// The order's first: the set every workspace's start reads, which has no
    /// default beside its order, so its first is said as that, never as this machine's list or default. A workspace's own default
    /// or list's first, which no start reads since the fold, keeps its words for a scope that still names one.
    /// </summary>
    private static StepSaid Listed(string? workspace, bool named) => workspace is { } name
        ? Reason(named ? $"it is `{name}`'s default" : $"it comes first in `{name}`'s list", named ? OpeningWhy.Default : OpeningWhy.First, ("workspace", name))
        : Reason("it comes first in the order of Daoris's accounts", OpeningWhy.FirstOrder);

    /// <summary>
    /// What a driven start's wait adds where the one account its kept-for-conversations rule passed is ready.
    /// </summary>
    /// <param name="names">The owner's names by id, read as the wait is said; null says the id.</param>
    public static string KeptAside(string kept, IReadOnlyDictionary<string, string>? names = null) =>
        KeptAsideOf(kept, names).Note;

    /// <summary><see cref="KeptAside"/> by code: the account by its id, which the page names.</summary>
    public static Noted KeptAsideOf(string kept, IReadOnlyDictionary<string, string>? names = null) =>
        Noted.Of(HeldNoteCodes.HeldKeptAside, $"`{AccountNames.Said(names, kept)}` is kept for conversations.", ("account", kept));

    /// <summary>
    /// What a wait adds where the scope names accounts of its own and others are ready: the accounts it does not
    /// list that are not cooling and not refused, and the door that adds the first, which the person answers and Daoris
    /// never does.
    /// </summary>
    /// <param name="listed">The accounts the scope may use: its list, or its one account where it names no list.</param>
    /// <param name="outside">The agent's other accounts, neither cooling nor refused, in name order.</param>
    /// <param name="names">
    /// The owner's names by id, read as the wait is said: the door keeps the ids, each spelled for any shell, as its
    /// workspace is.
    /// </param>
    public static string Outside(
        string agent, string? workspace, IReadOnlyList<string> listed, IReadOnlyList<string> outside,
        IReadOnlyDictionary<string, string>? names = null) =>
        OutsideOf(agent, workspace, listed, outside, names).Note;

    /// <summary>
    /// <see cref="Outside"/> by code, its English the same to the byte: the accounts a list the page names, the
    /// first the one the door adds, and the door's command, composed of ids and a workspace's name as a shell spells them.
    /// </summary>
    public static Noted OutsideOf(
        string agent, string? workspace, IReadOnlyList<string> listed, IReadOnlyList<string> outside,
        IReadOnlyDictionary<string, string>? names = null)
    {
        var who = workspace is { } name ? $"`{name}`" : "this machine's starts";
        var door = $"daoris agent account order {agent} {ShellWord.Words([.. listed, outside[0]], ShellWord.Account)}"
                   + (workspace is { } scoped ? $" --workspace {ShellWord.Of(scoped, ShellWord.Workspace)}" : "");
        var said = $"Not cooling, and not among the accounts {who} may use: {string.Join(", ", outside.Select(account => $"`{AccountNames.Said(names, account)}`"))} — "
                   + $"Daoris starts nothing on them unless a list names them; `{door}` adds `{AccountNames.Said(names, outside[0])}`.";
        var accounts = (IReadOnlyList<string>)[.. outside];
        return workspace is { } named
            ? Noted.Of(HeldNoteCodes.HeldOutsideWorkspace, said, ("workspace", named), ("accounts", accounts), ("first", outside[0]), ("door", door))
            : Noted.Of(HeldNoteCodes.HeldOutsideMachine, said, ("accounts", accounts), ("first", outside[0]), ("door", door));
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
