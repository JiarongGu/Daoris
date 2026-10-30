namespace Daoris.Driver;

/// <summary>
/// One request to integrate a branch into its repository's line (INTEGRATE1e part e1; the integration design §4.6): the requester, the rule
/// and its policy as they stood, and the frozen tip. A recorded landing's carries the unit, session and quest its record binds it to and the
/// gate proof the record keeps; a records branch's carries none of them, since it is no unit's and no run's. Kept as a row of the desktop
/// store's <c>integration_requests</c> (part e2, <see cref="IntegrationStore"/>), one open per repository and branch.
/// </summary>
/// <param name="Id">The request's own id.</param>
/// <param name="Tip">The frozen tip: the commit the request integrates, and nothing made after it (C11).</param>
/// <param name="Route">One of <see cref="IntegrationRoutes"/>.</param>
/// <param name="Line">The line by name it integrates into: the rule's own, or the repository's.</param>
/// <param name="Policy">The rule's <c>integrate</c> as it stood, every absent key read through the design's <i>Absent</i> column.</param>
/// <param name="At">When it was admitted.</param>
public sealed record IntegrationRequest(
    string Id, string Repository, string Workspace, string Branch, string Tip, string Route, string Line, IntegrationRequester Requester,
    LandingIntegrate Policy, DateTimeOffset At)
{
    /// <summary>The branch work unit whose work the tip carries last, as the record binds it; null for a records branch, or where the record names none.</summary>
    public string? Unit { get; init; }

    /// <summary>The session whose work last moved the landing, as its record says; null for a records branch.</summary>
    public string? Session { get; init; }

    /// <summary>The quest that session served, or the one the landing is named for; null for a records branch.</summary>
    public string? Quest { get; init; }

    /// <summary>When the landing it was taken from was made, its identity in the record with its branch and session; null for a records branch.</summary>
    public DateTimeOffset? LandedAt { get; init; }

    /// <summary>The landing's gate proof as its record keeps it for the tip, never judged again; null for a records branch, which carries no workflow gate.</summary>
    public IntegrationProof? Proof { get; init; }

    /// <summary>Where the effective rule came from as it stood (<see cref="LandingSource"/>).</summary>
    public string? RuleSource { get; init; }

    /// <summary>When it stopped being open (part e2): settled, handed back or dropped; null while it is open, as the door makes it.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>One of <see cref="IntegrationRequestCloses"/>, or a newer build's word, kept as written; null while it is open.</summary>
    public string? ClosedWhy { get; init; }
}

/// <summary>
/// The gate proof a recorded landing carries into its request (§4.6): who accepted the tip, and the review, the second opinion and the named
/// workflow version that let it go, each as the record holds it, null where it holds none.
/// </summary>
/// <param name="AcceptedBy">One of <see cref="Daoris.Driver.AcceptedBy"/>.</param>
public sealed record IntegrationProof(string AcceptedBy)
{
    public LandingReview? Review { get; init; }

    public LandingOpinion? Opinion { get; init; }

    public LandingWorkflow? Workflow { get; init; }
}

/// <summary>Who asks (§4.6): the person and their door, Daoris at its sweep, or the steward with its act's id and appointment generation.</summary>
/// <param name="Kind"><see cref="Person"/>, <see cref="Daoris"/> or <see cref="Steward"/>.</param>
/// <param name="Door">One of <see cref="IntegrationDoors"/>.</param>
public sealed record IntegrationRequester(string Kind, string Door)
{
    public const string Person = "person";
    public const string Daoris = "daoris";
    public const string Steward = "steward";

    /// <summary>The steward's act that asked; null for every other requester.</summary>
    public string? Act { get; init; }

    /// <summary>The generation of the steward's appointment the act was made under; null for every other requester.</summary>
    public long? Generation { get; init; }

    /// <summary>The owner's card, a Person route (D156): a landing named by its branch, or a records branch.</summary>
    public static IntegrationRequester Card { get; } = new(Person, IntegrationDoors.Card);

    /// <summary>Daoris's come-up sweep under <c>request: done</c>.</summary>
    public static IntegrationRequester Sweep { get; } = new(Daoris, IntegrationDoors.Sweep);

    /// <summary>The steward's <c>request</c> act under <c>request: steward</c>.</summary>
    public static IntegrationRequester StewardAct(string act, long generation) => new(Steward, IntegrationDoors.Act) { Act = act, Generation = generation };

    /// <summary>A unit's <c>integrate</c> node's start (INTEGRATE1n part n2): Daoris's, on the landing's own provenance.</summary>
    public static IntegrationRequester NodeStart { get; } = new(Daoris, IntegrationDoors.Node);

    /// <summary>The person's press under a merge rule (INTEGRATE1g, §4.2's named legacy exception): their Accept or their merge.</summary>
    public static IntegrationRequester Press { get; } = new(Person, IntegrationDoors.Press);
}

/// <summary>The doors a request comes through (§4.6).</summary>
public static class IntegrationDoors
{
    /// <summary>The owner's card: <c>daoris-driver integrate --request</c>, with <c>--landed</c> for a recorded landing (row h).</summary>
    public const string Card = "card";

    /// <summary>The come-up sweep.</summary>
    public const string Sweep = "sweep";

    /// <summary>The steward's act (row i).</summary>
    public const string Act = "act";

    /// <summary>The unit's <c>integrate</c> node's start (INTEGRATE1n part n2).</summary>
    public const string Node = "node";

    /// <summary>The person's press under a merge rule, carried by the transport (INTEGRATE1g).</summary>
    public const string Press = "press";
}

/// <summary>The routes into the line (§4.6): a unit's own landing by its provenance, a recorded landing, or a records branch.</summary>
public static class IntegrationRoutes
{
    /// <summary>
    /// A unit's own landing (INTEGRATE1n part n2, §17.4): a standing entry the unit's <c>integrate</c> node asks for, at the tip it froze,
    /// admitted by the acceptance's provenance the entry keeps, and by its record, exact tip, gate proof and no double as a recorded one.
    /// </summary>
    public const string Node = "node";

    /// <summary>An existing recorded landing: a standing entry of the machine's landings.</summary>
    public const string Recorded = "recorded";

    /// <summary>A records branch: no unit, no run, no workflow gate.</summary>
    public const string Records = "records";

    /// <summary>
    /// A merge rule's press, the named legacy exception (INTEGRATE1g, §4.2): the session's branch at the commit its press merges, under
    /// <see cref="IntegrationRequests.Legacy"/>, merged with today's message and its repository's hooks, and the checkout only fast-forwarded.
    /// </summary>
    public const string Merge = "merge";
}

/// <summary>
/// One ask of the door: the repository with its workspace and registered checkout, the branch, the route, who asks, and, where the caller
/// binds them, the unit and the tip it froze.
/// </summary>
/// <param name="Root">The repository's registered checkout, where git is asked; no session's tree is needed.</param>
public sealed record IntegrationAsk(string Repository, string? Workspace, string Root, string Branch, string Route, IntegrationRequester Requester)
{
    /// <summary>The unit the caller asks for, which the landing must carry; null asks for the landing as its record binds it.</summary>
    public string? Unit { get; init; }

    /// <summary>The tip the caller froze, which must be the record's (or a records branch's own); null takes the record's.</summary>
    public string? Tip { get; init; }

    /// <summary>
    /// The <c>request</c> a version's own <c>integrate</c> step asks with (INTEGRATE1n part n2: the node's option, carried on the node
    /// route's ask); null reads the landing's provenance, or the rule's.
    /// </summary>
    public string? Request { get; init; }

    /// <summary>The <c>conflicts</c> the node's unit takes, the node's option; null reads the rule's.</summary>
    public string? Conflicts { get; init; }
}

/// <summary>What the door answered: admitted with its request, or refused in a sentence with its code (<see cref="IntegrationRefusals"/>).</summary>
public sealed record IntegrationAdmission(bool Admitted, string Message)
{
    public IntegrationRequest? Request { get; init; }

    /// <summary>One of <see cref="IntegrationRefusals"/>; null where admitted.</summary>
    public string? Refusal { get; init; }
}

/// <summary>
/// Why the door refused (§4.6), a code each, kept in Workplace until the note codes take the integration's words (part e2 and on). The
/// recorded-landing door's four conditions are <see cref="NoRecord"/> and <see cref="NotUnit"/> (its record), <see cref="Moved"/>,
/// <see cref="Gone"/> and <see cref="Unread"/> (its exact tip), <see cref="NoProof"/> (its gate proof), <see cref="Open"/> and
/// <see cref="Held"/> (no double).
/// </summary>
public static class IntegrationRefusals
{
    /// <summary>The repository's effective rule carries no <c>integrate</c>.</summary>
    public const string NoIntegrate = "no-integrate";

    /// <summary>The requester's door is not one the rule opens, or the route is none the door knows.</summary>
    public const string Door = "door";

    /// <summary>No standing landing of that branch is recorded: none at all, or only a trace.</summary>
    public const string NoRecord = "no-record";

    /// <summary>The landing carries no such unit.</summary>
    public const string NotUnit = "not-unit";

    /// <summary>The record does not say who accepted its tip, so it holds no gate proof to read.</summary>
    public const string NoProof = "no-proof";

    /// <summary>The branch stands at another commit than the record's, or than the tip asked for.</summary>
    public const string Moved = "moved";

    /// <summary>git says the branch is missing.</summary>
    public const string Gone = "gone";

    /// <summary>git could not read the branch, or could not say whether the line holds it.</summary>
    public const string Unread = "unread";

    /// <summary>A request is open for the branch already.</summary>
    public const string Open = "open";

    /// <summary>The line holds the tip already.</summary>
    public const string Held = "held";

    /// <summary>The repository has no line here.</summary>
    public const string NoLine = "no-line";

    /// <summary>A session's branch at the records-branch door.</summary>
    public const string SessionBranch = "session-branch";

    /// <summary>A branch a landing recorded, standing or a trace, at the records-branch door.</summary>
    public const string LandingBranch = "landing-branch";

    /// <summary>The line itself, asked as a records branch.</summary>
    public const string IsLine = "line";

    /// <summary>The landing's record says neither that <c>integrate</c> stood at its acceptance nor a <c>request</c> it can read (§17.4).</summary>
    public const string NoRequest = "no-request";

    /// <summary>An automatic landing under <c>request: owner</c>: its landing is no request, the owner's card is.</summary>
    public const string AwaitsOwner = "awaits-owner";

    /// <summary>A landing under <c>request: steward</c>: no landing is the request, the steward's act or the owner's card is.</summary>
    public const string AwaitsSteward = "awaits-steward";
}
