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
/// A start that runs on another account of the person's order, because the account its default named was not ready
/// (TOOL4f, D125 §3.3). Machine-local: it names accounts, so it goes into the conversation record and the machine log,
/// never into the note, which travels (§3.6).
/// </summary>
/// <param name="From">The account the resolution named: a default, never a pick and never the tool's own home.</param>
/// <param name="Cooling">Its cool-off, when that is why it was not ready.</param>
/// <param name="Why">Why it was not ready, as a clause a person reads on this machine; it names the account.</param>
public sealed record RotatedStart(string From, CoolingEntry? Cooling, string Why);

/// <summary>
/// Which accounts a start may use, in the order it tries them (TOOL4f, D125 §3.3): the account the resolution names
/// first, and, only where a default named it and the applicable order lists it, the rest of that order after it,
/// wrapping. Pure: whether each is ready is the roster's question, asked in this order.
/// </summary>
/// <remarks>
/// <para><b>Rotation never moves work off a ready account</b>: the walk takes the first ready account, and the resolved
/// one is first. So once its reset passes, new starts run on it again, and nothing sticks to the account a start
/// rotated to.</para>
///
/// <para><b>What never rotates</b> (§3.4) is what this list leaves alone: a person's pick, the tool's own home (it is
/// never in an order, since an order names accounts, and work resolved to it is never moved), and an account the order
/// does not list. With no order the list is the one account, which is today's behaviour, byte for byte.</para>
/// </remarks>
public static class AccountRotation
{
    /// <param name="resolved">The account the resolution named: pick, workspace, machine, or null for the tool's own home.</param>
    /// <param name="from">Which rung named it.</param>
    /// <param name="order">The applicable order (<see cref="HarnessSettings.ResolveRotationFrom"/>), empty for none.</param>
    /// <param name="accounts">
    /// The agent's accounts on this machine (<see cref="HarnessSettings.Profiles"/>). An order naming one that is not
    /// here, which a door refuses but a hand edit could write, is never rotated into: a start pointed at a directory
    /// nobody signed in to would fail where it should have waited.
    /// </param>
    public static IReadOnlyList<string?> Candidates(
        string? resolved, ChoiceFrom from, IReadOnlyList<string> order, IReadOnlyCollection<string> accounts)
    {
        if (resolved is null || from is not (ChoiceFrom.Workspace or ChoiceFrom.Machine)) return [resolved];

        var at = -1;
        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], resolved, StringComparison.OrdinalIgnoreCase))
            {
                at = i;
                break;
            }
        }

        if (at < 0) return [resolved];

        var walk = new List<string?> { resolved };
        for (var step = 1; step < order.Count; step++)
        {
            var next = order[(at + step) % order.Count];
            if (accounts.Contains(next, StringComparer.OrdinalIgnoreCase)) walk.Add(next);
        }

        return walk;
    }
}

/// <summary>
/// What a rotated start says once its record is open (TOOL4f, D125 §3.3, §3.6, §5.4): its record's first line, naming
/// the account it opened on and why its default was not ready, and <c>account.rotated</c> in the machine log. One home
/// for a driven start, a carry-on, an intake, a conversation and Ask Daoris's opening, so none says it differently.
/// </summary>
public static class RotatedOpening
{
    /// <summary>The cut-off session a rotated carry-on carries on (D80), with its refused turn and context after a limit.</summary>
    /// <param name="Session">The cut-off session's record.</param>
    /// <param name="Turn">Its refused turn (the turns its record ended, plus one), where a limit cut it off; else null.</param>
    /// <param name="Used">Its context at its high-water, where a limit cut it off and its door reported one; else null.</param>
    public sealed record Carried(string Session, long? Turn, long? Used);

    /// <summary>Say it, or nothing for a start that did not rotate. The record's failure costs its line, never the start.</summary>
    internal static void Say(
        ServiceClient service, SessionEvents events, string sessionId, string adapter, HarnessSelection selection, Carried? carried)
    {
        if (selection.Rotated is not { } rotated || selection.Profile is not { } to) return;

        // The conversation record is the machine's (D76), so this line may name both accounts; the note, which
        // travels, never does (§3.6).
        events.Keep(
            sessionId,
            new SessionEvent
            {
                Kind = SessionEventKind.Note,
                Text = carried is { } cut
                    ? RotationWords.CarriedOn(cut.Session, to, rotated, cut.Turn, cut.Used)
                    : RotationWords.Opened(to, rotated),
            },
            say: null);
        service.AccountSaid(AccountLine.Rotated(sessionId, adapter, rotated.From, to, carried?.Session));
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
    public static string Opened(string to, RotatedStart rotated) => $"opened on `{to}`: {rotated.Why}.";

    /// <summary>
    /// The first line of a rotated carry-on's record (§3.6): the cut-off session it carries on, the account it runs on,
    /// why its default was not ready, and, after a limit, the turn that was refused and the context it had.
    /// </summary>
    /// <param name="turn">The cut-off session's refused turn (the turns its record ended, plus one), where a limit cut it off.</param>
    /// <param name="used">Its context at its high-water, where its door reported one.</param>
    public static string CarriedOn(string cutOff, string to, RotatedStart rotated, long? turn, long? used)
    {
        var refused = turn is { } number
            ? $"; its turn {number} was refused"
              + (used is { } context ? $" with {context.ToString("N0", CultureInfo.InvariantCulture)} tokens of context" : "")
            : "";
        return $"carried on from session `{cutOff}` on `{to}`; {rotated.Why}{refused}.";
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
