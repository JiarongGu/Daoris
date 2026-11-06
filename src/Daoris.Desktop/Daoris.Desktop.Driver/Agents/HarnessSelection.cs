namespace Daoris.Driver;

/// <param name="Refusal">Why this spawn must not happen — null when it may.</param>
/// <param name="Profile">The profile it runs as, or null for the harness's own configuration home.</param>
/// <param name="ProfileHome">Where that profile lives. Machine-local: it goes into no record and over no wire.</param>
/// <param name="Version">The harness version observed, for the record.</param>
/// <param name="Binary">
/// The managed executable this spawn runs (TOOL2/D57), or <b>null for whatever is on <c>PATH</c></b>
/// — which is the unpinned case and therefore the usual one. Machine-local like the profile home:
/// it is a path, so it goes into no record and over no wire.
/// </param>
/// <param name="ClaudeExecutable">
/// The managed <c>claude</c> the ACP adapter's Agent SDK should run (ACP2, §1a), or null to let it
/// find its own. It reads <b>the pipe door's pin</b> deliberately: the CLI and the account belong to
/// <c>claude-code</c>, and the ACP adapter is a separate package that merely runs it — so a person
/// who pinned <c>claude</c> gets that <c>claude</c> over either door, which is the point of pinning.
/// </param>
/// <param name="Environment">
/// What the account itself puts on the spawn — for an account that is an API key (AGT3), the key in
/// the tool's own variable. Machine-local like the profile home: it goes into no record and over no
/// wire. Null for a sign-in or the tool's own home.
/// </param>
public sealed record HarnessSelection(
    string? Refusal, string? Profile = null, string? ProfileHome = null, string? Version = null,
    string? Binary = null, string? ClaudeExecutable = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public bool Allowed => Refusal is null;

    /// <summary>
    /// The account's cool-off when that is why this spawn must not happen (TOOL4d, D125 §4), and null otherwise: a
    /// hold that waits for a time, where every other refusal waits for a person.
    /// </summary>
    /// <remarks>
    /// Held over an order (TOOL4f), it is the cool-off of the account that is ready first, which is when the start may
    /// run again; for a pick refused, the pick's own.
    /// </remarks>
    public CoolingEntry? Cooling { get; init; }

    /// <summary>
    /// The account the start's scope begins at and why the start runs elsewhere (TOOL4f, D125 §3.3; TOOL6b, D130 §13 as
    /// §16 amends it); null when it runs where its scope begins.
    /// </summary>
    public RotatedStart? Rotated { get; init; }

    /// <summary>
    /// The step of the goal's walk that chose the account, where a list offered a choice (TOOL6b, D130 §16.4): what the
    /// start's record says first. Null under <c>use: order</c>, for a pick, and where the scope offered one account.
    /// </summary>
    public AccountChoice? Choice { get; init; }

    /// <summary>
    /// The steps of the walk that chose the account, by code (UX7d-1, D152's UX7d-1 note): the first, and the rest's where the
    /// first account was passed for itself, each its English and its reason with its own values. <see cref="Choice"/>'s
    /// clause is all of them joined and <see cref="Rotated"/>'s the first. Null for a pick and a scope with no list.
    /// Machine-local: it names accounts.
    /// </summary>
    public IReadOnlyList<StepSaid>? Steps { get; init; }

    /// <summary>
    /// What each account of the scope's list last said about its windows, as the start's first line says it (TOOL6c, D130
    /// §16.4), with its lines by code (UX7d-1); null where none has said anything, and for a pick or a scope with no list.
    /// Machine-local: it names accounts.
    /// </summary>
    public Noted? AccountsSaid { get; init; }

    /// <summary><see cref="AccountsSaid"/>'s English: the sentence the start's first line ends with.</summary>
    public string? SaidLine => AccountsSaid?.Note;

    /// <summary>
    /// Why the one account a refused start asked for was not ready (MSG1g): cooling, refused, or signed out as the agent said;
    /// null where the start was refused for anything else (no agent, a pin nobody installed), or ran.
    /// </summary>
    public AccountReadiness? NotReady { get; init; }

    /// <summary>
    /// The accounts a held start passed because they are not signed in (TOOL6g), whether it then waits on a cooling one or on
    /// none; null where it passed none, or ran. What the hold's sentence names with each sign-in, as facts for the page and
    /// the log.
    /// </summary>
    public SignedOutAccounts? SignedOut { get; init; }

    /// <summary>
    /// Why the agent itself cannot run, where that is why this start must not happen (XAGENT1b, the second-agent design §3.3):
    /// nothing of it installed, or a pin with nothing installed at it (D57). Null where the agent may run, or the start was
    /// held for an account (<see cref="NotReady"/>). A fact beside the sentence, so a reader judges no words.
    /// </summary>
    public AgentAbsence? Absent { get; init; }

    /// <summary>
    /// <see cref="Refusal"/> by code (ROSTERREFUSE1), its English the same to the byte: the kind of refusal, and the account by its
    /// owner and id where it names one, never by the name the person gave it. Null where the refusal is not one a held note can
    /// carry by code (a wait over a list, a pick refused, a provider's refusal, an agent with its own tooling, a refusal with
    /// more said after it); the note then carries its words. A fact beside the sentence, so a reader judges no words.
    /// </summary>
    public Noted? RefusalNote { get; init; }

    /// <summary>
    /// The accounts a held start passed because their readings do not offer the model asked (D205 §3.3, MODELROLE1c2:
    /// <c>not-offered</c> or <c>refused</c>, by account); null where it passed none, or ran. Not a cool-off and never counted.
    /// </summary>
    public IReadOnlyList<string>? NotOffered { get; init; }

    /// <summary>
    /// Its provider may not work at the start's place (AGENTPROVIDER1 row u, <see cref="ProviderReach"/>): held before any account is asked,
    /// so nothing is counted and no context or credential is sent. A fact beside the sentence, so a reader judges no words.
    /// </summary>
    public bool NotOpened { get; init; }

    /// <summary>
    /// The start's count, as the walk numbered it when it chose the account (D205 §4.1, round 4): what <c>Release</c> takes back; 0 where
    /// the start counted nothing.
    /// </summary>
    public long Number { get; init; }

    /// <summary>
    /// The role's policy binding the start took (STAFFPOLICY1c2, Roles' <c>StaffBinding</c>, opaque here since Agents sits below Roles), carried
    /// with the selection so an agent with no account count (<see cref="Number"/> 0) still writes its start's row at the open; null where the
    /// plan arranged nothing.
    /// </summary>
    public object? Staffed { get; init; }
}
