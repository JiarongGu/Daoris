using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// Whose each row of <i>What needs you</i> is (the window design §5.2): the owner's, as one of the six kinds of wait only the owner
/// settles, or the agents', which a role of theirs takes. One census over every kind <see cref="NeedsAPerson"/> and its parts make,
/// each with its doctrine reason; a kind with an agents' side names what tells a row's side and the role that takes it.
/// <c>AttentionOwnershipTests</c> reads every source that makes rows and fails a kind made with no class here.
/// </summary>
/// <remarks>
/// <para><b>An agents' kind is counted as the owner's until a role is appointed to take it.</b> Daoris is not made to block progress:
/// a row the agents' side holds is theirs only while the actor configuration appoints a role that takes it, read at each judgement from
/// the roles as they stand (<see cref="AttentionTakers"/>, <see cref="Class.TakenWhen"/>); with none appointed, or a kind no role's shape
/// takes yet, it is the owner's, counted with its kind of wait, and says that no role takes it yet (<c>untaken</c>).</para>
///
/// <para><b>Pushed on every row</b> (<see cref="AttentionStateSlice.Judge"/>): <c>class</c>, <see cref="OwnersClass"/> or
/// <see cref="AgentsClass"/>; on the owner's, <c>waitKind</c>; and on an agents' kind the owner holds for want of a role,
/// <c>untaken</c>. The bell, the taskbar, the window's title, the places, Overview, the toasts and <c>daoris notices</c> count the owner's
/// rows alone; the agents' are listed apart, under <i>With the agents</i>.</para>
///
/// <para><b>Never hidden.</b> A row of a kind the census does not hold is the owner's, with no kind of wait: counted rather than lost,
/// since a census can only fail at a build.</para>
/// </remarks>
public static class AttentionOwnership
{
    /// <summary>The owner's class: one of the six kinds of wait, counted.</summary>
    public const string OwnersClass = "owner";

    /// <summary>The agents' class: a role appointed now takes it; listed, never counted as the owner's.</summary>
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

        /// <summary>No role: a wait that ends at its own time or reading (an account's cool-off), which nobody need answer.</summary>
        public const string Clock = "clock";
    }

    /// <summary>
    /// One kind's class, with its reason. <see cref="Wait"/> is the owner's kind of wait for a row of it the owner holds; <see cref="Takes"/>
    /// the agents' role that takes it otherwise, where it has an agents' side: <see cref="OwnersWhen"/> telling the side a row's own facts
    /// give it, <see cref="Otherwise"/> why the other side is the agents', and <see cref="TakenWhen"/> whether a role is appointed now to
    /// take a row of that side, null where no role's shape takes it yet, so every row of it is the owner's.
    /// </summary>
    public sealed record Class(string Kind, string Reason)
    {
        public required string Wait { get; init; }
        public string? Takes { get; init; }
        public Func<JsonObject, bool>? OwnersWhen { get; init; }
        public string? Otherwise { get; init; }
        public Func<JsonObject, AttentionTakers, bool>? TakenWhen { get; init; }

        /// <summary>The owner's kind of wait for <paramref name="row"/>, where its facts name another than <see cref="Wait"/>.</summary>
        public Func<JsonObject, string?>? WaitOf { get; init; }
    }

    /// <summary>A row's side: its class, its kind of wait on the owner's, and whether it is an agents' kind no role takes now.</summary>
    public readonly record struct Side(string Class, string? Wait, bool Untaken = false);

    /// <summary>Every row of the kind is the owner's side today: no row carries what would give it to the other.</summary>
    private static bool EveryRow(JsonObject _) => true;

    /// <summary>No row of the kind is the owner's by its own facts: whose it is turns on whether a role is appointed to take it.</summary>
    private static bool NoRow(JsonObject _) => false;

    /// <summary>A role appointed to take the kind: a supervisor admitted for one of <paramref name="acts"/> of <paramref name="kind"/> at the row's place.</summary>
    private static Func<JsonObject, AttentionTakers, bool> Supervising(string kind, params string[] acts) =>
        (row, takers) => takers.Supervises(kind, PlaceOf(row), acts);

    /// <summary>A role appointed to take the kind: a holder holds the steward.</summary>
    private static bool Stewarded(JsonObject _, AttentionTakers takers) => takers.Steward;

    /// <summary>Nobody need take it: it ends at its own time or reading.</summary>
    private static bool ByItself(JsonObject _, AttentionTakers __) => true;

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

        // ---- the owner's on every row today, an approver's or a holder's side named ----
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

        // ---- the owner's by a row's own facts, else the agents' while a role is appointed to take it ----
        new("account-wait", "A start waits on accounts that only a sign-in clears: signed out, a key refused, expired or revoked, an error, or no account at all; a sign-in is the owner's.")
        {
            Wait = Wait.SignIn, Takes = Taker.Clock, OwnersWhen = NeedsASignIn, TakenWhen = ByItself,
            Otherwise = "A start waiting out a cool-off, or on an account not read yet, clears at its own time or reading, which nobody need answer.",
        },
        new("answer", "The owner's own answers-alone rule held a third answer with nobody between: a setting the owner holds marks it theirs.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = row => row["answer"] is JsonObject answer && answer["rule"] is JsonObject,
            TakenWhen = Supervising(SupervisorKinds.Course, SupervisorKinds.GoOn),
            Otherwise = "A hold marked before the rules names no rule of the owner's: the course supervisor's go-on, where one is appointed.",
        },
        new("work", "A unit's clean-up waits where nothing names it the agents': it may be the owner's own branch or work, and deleting their data is theirs.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Steward, OwnersWhen = MayBeTheOwners, TakenWhen = Stewarded,
            Otherwise = "A session's tree or branch, a standing daoris/ branch, a held attempt, a record that does not read or a close in part is the agents' own bookkeeping: the steward's, where a holder holds it.",
        },
        new("policy-hold", "A hold on a per-use account or a budget is the owner's money (doctrine rule 1); any other, while no limit supervisor is appointed.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NamesMoney, WaitOf = row => NamesMoney(row) ? Wait.Money : null,
            TakenWhen = Supervising(SupervisorKinds.Limit, SupervisorKinds.Move),
            Otherwise = "A role's policy asking before a move, or its bound of moves, is the limit supervisor's move, where one is appointed.",
        },
        new("permission", "A tool call held under asking waits on whoever the actor configuration appoints to allow it, and on the owner while none is.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NoRow, TakenWhen = Supervising(SupervisorKinds.Allow, "once"),
            Otherwise = "A held call is the allow supervisor's to answer where one is appointed at its session's repository.",
        },
        new("review", "Finished work to accept waits on whoever the actor configuration appoints to accept it, and on the owner while none is.")
        {
            Wait = Wait.Release, Takes = Taker.Supervisor, OwnersWhen = NoRow, TakenWhen = Supervising(SupervisorKinds.Landing, "accept"),
            Otherwise = "Agents review agents: a run's accept is the landing supervisor's where one is appointed at its repository.",
        },
        new("rule", "An agent's proposal to widen what agents may do waits on whoever the actor configuration appoints to allow it, and on the owner while none is.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NoRow, TakenWhen = Supervising(SupervisorKinds.Allow, "standing"),
            Otherwise = "Widening within the declaration is the allow supervisor's standing allow where one is appointed at the rule's reach.",
        },
        new("parked", "A session parked to ask: its question waits on whoever the actor configuration appoints to answer it, and on the owner while none is.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow, TakenWhen = Supervising(SupervisorKinds.Question, SupervisorKinds.Answer),
            Otherwise = "Its question is the question supervisor's to answer where one is appointed; one only the owner settles reaches them as a Question (doctrine rule 1).",
        },
        new("parked-quest", "A quest parked after its failed sessions waits on a go-on, and on the owner while no role is appointed to give it.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow, TakenWhen = Supervising(SupervisorKinds.Course, SupervisorKinds.GoOn),
            Otherwise = "Its go-on is the course supervisor's where one is appointed; what it cannot finish is reported as gave up (doctrine rule 1, D336 §2).",
        },
        new("opinion", "A second opinion nobody is free to give waits on a re-cast, a relax or a wait, and on the owner while no role is appointed to decide it.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            TakenWhen = Supervising(SupervisorKinds.Vacancy, SupervisorKinds.Recast, SupervisorKinds.Relax, SupervisorKinds.Wait),
            Otherwise = "Agents review agents: the vacancy is the reporting line's supervisor's where one is appointed.",
        },
        new("steward-stopped", "The steward's stop holds a quest for a Try again, and on the owner while no holder holds the steward.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Steward, OwnersWhen = NoRow, TakenWhen = Stewarded,
            Otherwise = "The Try again is the steward's to give where a holder holds it.",
        },

        // ---- the agents' by their kind, the owner's until a role is appointed to take them: no role's shape takes them yet ----
        new("left", "Work left unfinished is reported as gave up, never asked (doctrine rule 1, D336 §2): whether it goes on is the owner's while no role takes it.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A supervisor that takes left work goes on with it, once one is appointed (UISE9).",
        },
        new("unclosed", "A quest its finished session left taken: closing it or going on is the owner's while no role takes it.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A supervisor that takes unclosed quests closes them or goes on, once one is appointed (UISE9).",
        },
        new("request-declined", "A run held at a request its receiver declined: passing it over is the owner's while no role takes it.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A supervisor that takes declined requests passes them over, once one is appointed (UISE9).",
        },
        new("proposal", "An ask's split into quests to publish: where the work goes is the owner's while no role takes it.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "Publishing an ask's split is the agents' (D341), once a role that takes it is appointed (UISE9).",
        },
        new("second-check", "A second check's flag on one of Ask Daoris's acts, which acted for the owner: the owner's to look at while no role takes it.")
        {
            Wait = Wait.Question, Takes = Taker.Reviewer, OwnersWhen = NoRow,
            Otherwise = "Agents review agents: a reviewer that takes the flags reads them, once one is appointed (UISE9).",
        },
        new("helper", "An agent's ask for a helper: allowing it is the owner's while no role takes it.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A supervisor that takes helpers' asks allows them, once one is appointed (UISE9).",
        },
        new("hand-over", "An agent's ask to hand its session over: allowing it is the owner's while no role takes it.")
        {
            Wait = Wait.GoAhead, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A supervisor that takes hand-overs allows them, once one is appointed (UISE9).",
        },
        new("helper-ended", "A session whose helper ended unanswered and whose move back was refused: going on is the owner's while no role takes it.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A supervisor that takes such sessions goes on with them, once one is appointed (UISE9).",
        },
        new("quiet", "A quiet session: whether it goes on or ends is the owner's while no role takes it.")
        {
            Wait = Wait.Question, Takes = Taker.Supervisor, OwnersWhen = NoRow,
            Otherwise = "A supervisor that takes quiet sessions decides them, once one is appointed (UISE9).",
        },
    ];

    private static readonly Dictionary<string, Class> ByKind = Census.ToDictionary(each => each.Kind, StringComparer.Ordinal);

    /// <summary>
    /// A row's side as <paramref name="takers"/> appoint roles now: the owner's where its kind has no agents' side or its facts make it
    /// theirs; the agents' where a role is appointed to take it; else the owner's, as no role's yet. A kind the census does not hold is the
    /// owner's, with no kind of wait.
    /// </summary>
    public static Side Of(JsonObject row, AttentionTakers takers)
    {
        if (Text(row, "kind") is not { } kind || !ByKind.TryGetValue(kind, out var known)) return new(OwnersClass, null);
        var wait = known.WaitOf?.Invoke(row) ?? known.Wait;
        if (known.Takes is null || known.OwnersWhen?.Invoke(row) != false) return new(OwnersClass, wait);
        return known.TakenWhen?.Invoke(row, takers) == true ? new(AgentsClass, null) : new(OwnersClass, wait, Untaken: true);
    }

    /// <summary>
    /// <paramref name="row"/> with its class written on it, its kind of wait where it is the owner's, and <c>untaken</c> where it is an
    /// agents' kind no role takes now.
    /// </summary>
    public static JsonObject Classed(JsonObject row, AttentionTakers takers)
    {
        var side = Of(row, takers);
        row["class"] = side.Class;
        if (side.Wait is not null) row["waitKind"] = side.Wait;
        if (side.Untaken) row["untaken"] = true;
        return row;
    }

    /// <summary>
    /// Where a row waits, as a supervisor is placed: its workspace and its repository (its <c>where</c>) where it names them, else the
    /// machine. The admission places a repository named alone in the workspace the store keeps it in.
    /// </summary>
    private static RolePlace PlaceOf(JsonObject row) =>
        new(Workspace: Text(row, "workspace") is { Length: > 0 } workspace ? workspace : null,
            Repository: Text(row, "where") is { Length: > 0 } repository ? repository : null);

    /// <summary>The states of a named account that clear at their own time or reading: a cool-off, and an account not read yet.</summary>
    private static readonly HashSet<string> ClearsByItself = new(StringComparer.Ordinal) { "cooling", "unknown" };

    /// <summary>
    /// A start an account holds that only a sign-in clears: an allow-list, so every state but a cool-off and an account not read yet (signed
    /// out, refused, keyed, expired, revoked, an error, one this build does not know) is the owner's, as is no account, none named, or none read.
    /// </summary>
    private static bool NeedsASignIn(JsonObject row)
    {
        if (row["account"] is not JsonObject account) return true;
        if (account["noAccount"] is JsonValue none && none.TryGetValue<bool>(out var noAccount) && noAccount) return true;
        var named = Objects(account["named"]).ToList();
        return named.Count == 0 || named.Any(each => Text(each, "state") is not { } state || !ClearsByItself.Contains(state));
    }

    /// <summary>
    /// A policy hold whose verdict names money: its consideration's fact names a budget or a per-use account. No verdict carries either today,
    /// so such a hold is the limit supervisor's where one is appointed, and the owner's otherwise.
    /// </summary>
    private static bool NamesMoney(JsonObject row) =>
        row["sitting"] is JsonObject sitting && sitting["said"] is JsonObject said
        && (Text(said, "budget") is not null || said["perUse"] is JsonValue perUse && perUse.TryGetValue<bool>(out var yes) && yes);

    /// <summary>
    /// A unit's clean-up that may hold the owner's own work. A held attempt, a record that does not read and a close in part are the agents'
    /// runs. A unit nothing records is the owner's unless its title, an entry's branch or the look names it Daoris's (a <c>daoris/</c>
    /// branch, an entry Daoris left unrecorded). A unit whose owner is recorded is the agents' where it is a session's (its session recorded)
    /// or a <c>daoris/</c> branch, and the owner's otherwise: a branch of the person's own that no session of Daoris's made.
    /// </summary>
    private static bool MayBeTheOwners(JsonObject row)
    {
        if (row["blocked"] is not JsonObject blocked) return false;
        static bool Daoris(string? branch) => branch?.StartsWith("daoris/", StringComparison.Ordinal) == true;
        var entries = Objects(blocked["entries"]).ToList();
        var daoris = Daoris(Text(row, "title")) || entries.Any(entry => Daoris(Text(entry["facts"] as JsonObject, "branch")));
        var unknown = blocked["unknown"] is JsonValue value && value.TryGetValue<bool>(out var yes) && yes;
        return unknown
            ? !daoris && !entries.Any(entry => Text(entry, "code") == InventoryCode.Unrecorded)
            : !daoris && Text(row, "session") is not { Length: > 0 };
    }
}
