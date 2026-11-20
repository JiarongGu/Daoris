using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// Whose each row of <i>What needs you</i> is (the window design §5.2): the owner's, as one of the six kinds of wait only the owner
/// settles, or the agents', which a role of theirs takes. One census over every kind <see cref="NeedsAPerson"/> and its parts make,
/// each with its doctrine reason; a conditional kind names both its sides and what tells a row's. <c>AttentionOwnershipTests</c> reads
/// the loop's sources and fails a kind made with no class here.
/// </summary>
/// <remarks>
/// <para><b>Pushed on every row</b> (<see cref="AttentionStateSlice.Judge"/>): <c>class</c>, <see cref="OwnersClass"/> or
/// <see cref="AgentsClass"/>, and on the owner's, <c>waitKind</c>. The bell, the taskbar, the window's title, the places and Overview
/// count the owner's rows alone; the agents' are listed apart, under <i>With the agents</i>, uncounted and without amber, until a role
/// of theirs answers them.</para>
///
/// <para><b>Never hidden.</b> A row of a kind the census does not hold is the owner's, with no kind of wait: counted rather than lost,
/// since a census can only fail at a build.</para>
/// </remarks>
public static class AttentionOwnership
{
    /// <summary>The owner's class: one of the six kinds of wait, counted.</summary>
    public const string OwnersClass = "owner";

    /// <summary>The agents' class: a role of theirs takes it; listed, never counted as the owner's.</summary>
    public const string AgentsClass = "agents";

    /// <summary>The six kinds only the owner settles, as a row carries them.</summary>
    public static class Wait
    {
        /// <summary>A direction or a target only the owner can settle.</summary>
        public const string Question = "question";

        /// <summary>An act the repository's declaration keeps the owner's: a production write, a push outside, deleting their data, a folder's trust.</summary>
        public const string GoAhead = "go-ahead";

        /// <summary>A release to a stage the owner keeps, or a set-up shown for their look.</summary>
        public const string Release = "release";

        /// <summary>An account or a remote signed out while work needs it.</summary>
        public const string SignIn = "sign-in";

        /// <summary>A per-use account near the budget the owner set, or its prepaid balance run out.</summary>
        public const string Money = "money";

        /// <summary>A terminal's press asking to act as the owner, which only the person key's door confirms.</summary>
        public const string ConfirmItWasYou = "confirm";
    }

    /// <summary>The agents' roles that take a kind (§5.2): who answers it once one is appointed.</summary>
    public static class Taker
    {
        public const string Supervisor = "supervisor";
        public const string Steward = "steward";
        public const string Reviewer = "reviewer";

        /// <summary>The role the repository's declaration gives an act it does not keep the owner's.</summary>
        public const string Approver = "approver";

        /// <summary>The role a run's step names, where another than the owner holds it (an acceptor's step to the acceptor).</summary>
        public const string Holder = "holder";
    }

    /// <summary>
    /// One kind's class, with its reason. <see cref="Wait"/> is the owner's kind of wait where the owner settles a row of it,
    /// <see cref="Takes"/> the agents' role that takes it otherwise; a conditional kind has both, <see cref="OwnersWhen"/> telling a
    /// row's side and <see cref="Otherwise"/> why the other side is the agents'.
    /// </summary>
    public sealed record Class(string Kind, string Reason)
    {
        public string? Wait { get; init; }
        public string? Takes { get; init; }
        public Func<JsonObject, bool>? OwnersWhen { get; init; }
        public string? Otherwise { get; init; }
    }

    /// <summary>Every row of the kind is the owner's side today: no row carries what would give it to the other.</summary>
    private static bool EveryRow(JsonObject _) => true;

    /// <summary>No row of the kind is the owner's side today: no setting the owner holds marks one theirs yet.</summary>
    private static bool NoRow(JsonObject _) => false;

    /// <summary>Every kind of <i>What needs you</i>, classed as the window design's §5.2 says.</summary>
    public static readonly IReadOnlyList<Class> Census =
    [
        // ---- the owner's ----
        new("signed-out", "An account of Daoris's is signed out, or its key refused, and work needs it: sign-ins and credentials are the owner's (doctrine rule 1).") { Wait = Wait.SignIn },
        new("git-held", "A remote cannot authenticate, or its host's key is unknown or changed: what signs in to it is the owner's credential (doctrine rule 1).") { Wait = Wait.SignIn },
        new("confirmation", "A terminal's press asks to act as the owner: only the owner proves it was them (D156's door).") { Wait = Wait.ConfirmItWasYou },
        new("intake", "An ask's intake asks about the ask's own words: direction and targets are the owner's (doctrine rule 1).") { Wait = Wait.Question },
        new("departure", "A done departed from a requirement the owner set: whether the target moves is theirs.") { Wait = Wait.Question },
        new("plan-question", "A question the ask's delivery plan asks that only the owner settles: a direction or a target.") { Wait = Wait.Question },
        new("design-question", "A design pass's closed question on direction, whose answer drafts a decision.") { Wait = Wait.Question },
        new("unanswerable", "A quest is addressed to a repository this deployment does not hold: where the work goes is the owner's direction.") { Wait = Wait.Question },
        new("memory", "Ends and replacements of what the owner's knowledge keeps, beyond the leave they set, and a stopped session's staged rows: deleting or replacing their records is theirs.") { Wait = Wait.GoAhead },

        // ---- conditional: both sides named ----
        new("go-ahead", "An act the repository's declaration keeps the owner's (a production write, a push outside, deleting their data): a session asks one only for an act its declaration does not let run unasked.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Approver, OwnersWhen = EveryRow,
            Otherwise = "An act the declaration gives an approver role goes to that role (Give this act to…, UISE10); no row carries one yet.",
        },
        new("git-ask", "A push or a fetch whose class the owner's git access keeps on ask: a push outside is the owner's go-ahead.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Approver, OwnersWhen = EveryRow,
            Otherwise = "A remote act the declaration gives an approver role goes to that role; no row carries one yet.",
        },
        new("trust", "A folder the repository's declaration does not decide waits on the owner's trust before an agent works in it.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Approver, OwnersWhen = EveryRow,
            Otherwise = "A folder the declaration decides is trusted by it, or goes to its approver role; no row carries one yet.",
        },
        new("space", "A volume stays below its floor once Daoris freed what it can make again: what is left is the owner's data, and deleting it is theirs.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Approver, OwnersWhen = EveryRow,
            Otherwise = "Freeing what the declaration lets go goes to its approver role; no row carries one yet.",
        },
        new("step", "A run's step waits on a role the owner holds (only such a step is a row): a release to a stage they keep.")
        {
            Wait = Wait.Release, Takes = Taker.Holder, OwnersWhen = EveryRow,
            Otherwise = "A step whose role another holds goes to that role, an acceptor's step to the acceptor, and waits in its session's dock.",
        },
        new("set-up", "A set-up shown for the owner's look: the owner holds every set-up step's look today.")
        {
            Wait = Wait.Release, Takes = Taker.Holder, OwnersWhen = EveryRow,
            Otherwise = "A set-up step whose look another role holds goes to that role.",
        },
        new("integration", "A candidate held for the owner's decide, a superseded commit's answer, a landing's card, or a declaration owed their acceptance: the integration line's role is theirs.")
        {
            Wait = Wait.Release, Takes = Taker.Holder, OwnersWhen = EveryRow,
            Otherwise = "A line whose role another holds goes to that role.",
        },
        new("account-wait", "A start waits on accounts only a sign-in clears (signed out, a key refused, or no account): sign-ins are the owner's.")
        {
            Wait = Wait.SignIn, Takes = Taker.Supervisor, OwnersWhen = NeedsASignIn,
            Otherwise = "A start waiting out a cool-off or an account not read yet clears by itself or by another account: the supervisor's.",
        },
        new("answer", "The owner's own answers-alone rule held a third answer with nobody between: a setting the owner holds marks it theirs.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = row => row["answer"] is JsonObject answer && answer["rule"] is JsonObject,
            Otherwise = "A hold marked before the rules names no rule of the owner's: the supervisor's.",
        },
        new("work", "A unit's clean-up waits where nothing records whose it is and nothing names it the agents': it may be the owner's own work, and deleting their data is theirs.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Steward, OwnersWhen = MayBeTheOwners,
            Otherwise = "A session's tree or branch, a standing daoris/ branch, a held attempt, a record that does not read or a close in part is the agents' own bookkeeping: the steward's.",
        },
        new("policy-hold", "A hold on a per-use account's budget is the owner's money (doctrine rule 1).")
        {
            Wait = Wait.Money, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A role's policy asking, or its bound of moves, is the supervisor's; no hold names a budget yet (UIRP28).",
        },
        new("permission", "A tool call held under asking is the owner's only where a setting the owner holds marks it so.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "Every held call is the supervisor's to answer while no setting of the owner's marks one theirs (Give this act to…, UISE10).",
        },
        new("review", "Finished work to accept is the owner's only where a setting the owner holds marks it so.")
        {
            Wait = Wait.Release, Takes = Taker.Reviewer, OwnersWhen = NoRow,
            Otherwise = "Agents review agents: a run's accept is the reviewer's while no setting of the owner's marks it theirs.",
        },
        new("rule", "An agent's proposal to widen what agents may do is the owner's only where a setting the owner holds marks it so.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "Widening within the declaration is the supervisor's while no setting of the owner's marks it theirs.",
        },

        // ---- the agents' ----
        new("parked", "A session parked to ask: its question is the supervisor's to answer; one only the owner settles reaches them as a Question (doctrine rule 1).") { Takes = Taker.Supervisor },
        new("left", "Work left unfinished is reported as gave up, never asked (doctrine rule 1, D336 §2).") { Takes = Taker.Supervisor },
        new("parked-quest", "A quest parked after its failed sessions is reported as gave up, never asked (doctrine rule 1, D336 §2).") { Takes = Taker.Supervisor },
        new("unclosed", "A quest its finished session left taken: closing it or going on is the supervisor's.") { Takes = Taker.Supervisor },
        new("steward-stopped", "The steward's stop holds a quest for a Try again, the steward's to give.") { Takes = Taker.Steward },
        new("request-declined", "A run held at a request its receiver declined: passing it over is the supervisor's.") { Takes = Taker.Supervisor },
        new("proposal", "An ask's split into quests to publish is the agents' (D341).") { Takes = Taker.Supervisor },
        new("opinion", "A second opinion that waits: agents review agents.") { Takes = Taker.Reviewer },
        new("second-check", "A second check's flag on one of Ask Daoris's acts: agents review agents.") { Takes = Taker.Reviewer },
        new("helper", "An agent's ask for a helper: the supervisor's to allow.") { Takes = Taker.Supervisor },
        new("hand-over", "An agent's ask to hand its session over: the supervisor's to allow.") { Takes = Taker.Supervisor },
        new("helper-ended", "A session whose helper ended unanswered and whose move back was refused: the supervisor's to go on with.") { Takes = Taker.Supervisor },
        new("quiet", "A quiet session: whether it goes on or ends is the supervisor's.") { Takes = Taker.Supervisor },
    ];

    private static readonly Dictionary<string, Class> ByKind = Census.ToDictionary(each => each.Kind, StringComparer.Ordinal);

    /// <summary>A row's class and, on the owner's, its kind of wait; a kind the census does not hold is the owner's, with none.</summary>
    public static (string Class, string? Wait) Of(JsonObject row)
    {
        if (Text(row, "kind") is not { } kind || !ByKind.TryGetValue(kind, out var known)) return (OwnersClass, null);
        var owners = known switch
        {
            { Wait: null } => false,
            { Takes: null } => true,
            _ => known.OwnersWhen!(row),
        };
        return owners ? (OwnersClass, known.Wait) : (AgentsClass, null);
    }

    /// <summary><paramref name="row"/> with its class written on it, and its kind of wait where it is the owner's.</summary>
    public static JsonObject Classed(JsonObject row)
    {
        var (side, wait) = Of(row);
        row["class"] = side;
        if (wait is not null) row["waitKind"] = wait;
        return row;
    }

    /// <summary>A start an account holds that only a sign-in clears: one named signed out or its key refused, or no account at all.</summary>
    private static bool NeedsASignIn(JsonObject row) =>
        row["account"] is JsonObject account
        && (account["noAccount"] is JsonValue none && none.TryGetValue<bool>(out var noAccount) && noAccount
            || Objects(account["named"]).Any(named => Text(named, "state") is "out" or "refused"));

    /// <summary>
    /// A unit's clean-up that may hold the owner's own work: nothing records its owner, and neither its title nor an entry's branch is a
    /// <c>daoris/</c> branch, nor an entry the look found as Daoris's unrecorded one. A held attempt, a record that does not read and a
    /// close in part are the agents' runs.
    /// </summary>
    private static bool MayBeTheOwners(JsonObject row)
    {
        if (row["blocked"] is not JsonObject blocked || blocked["unknown"] is not JsonValue unknown || !unknown.TryGetValue<bool>(out var yes) || !yes) return false;
        static bool Daoris(string? branch) => branch?.StartsWith("daoris/", StringComparison.Ordinal) == true;
        var entries = Objects(blocked["entries"]).ToList();
        return !Daoris(Text(row, "title"))
            && !entries.Any(entry => Text(entry, "code") == InventoryCode.Unrecorded || Daoris(Text(entry["facts"] as JsonObject, "branch")));
    }
}
