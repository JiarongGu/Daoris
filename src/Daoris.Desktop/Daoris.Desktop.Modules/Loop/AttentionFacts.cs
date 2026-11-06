using System.Text.Json.Nodes;

namespace Daoris.Desktop;

/// <summary>
/// What <i>What needs you</i> is judged from (PUSH1d4, D182 point 6): each input in the page's shapes, as the other slices and the routes
/// already write them, so the judge and its tests read one shape.
/// </summary>
/// <remarks>Every list defaults to empty: an input that holds nothing judges no row, as the page's defaults did.</remarks>
public sealed record AttentionFacts
{
    /// <summary>Every session record, closed ones and every workspace's included, as the sessions slice holds them.</summary>
    public IReadOnlyList<JsonObject> Sessions { get; init; } = [];

    /// <summary>
    /// The sessions a supervisor's decision answers for now (SUPERVISOR1a-i, D208; the supervisor design §4.2), from the decisions'
    /// memory: held or released, the park is not a wait on the person, since the overrule is optional and said as a notice.
    /// </summary>
    public IReadOnlySet<string> Supervised { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Every quest, as the quests slice holds them.</summary>
    public IReadOnlyList<JsonObject> Quests { get; init; } = [];

    /// <summary>The registry, each row's <c>repository</c>, <c>adopted</c>, <c>addressable</c> and <c>workspace</c>.</summary>
    public IReadOnlyList<JsonObject> Registry { get; init; } = [];

    /// <summary>Every ask, as the asks slice holds them, each with its go-aheads.</summary>
    public IReadOnlyList<JsonObject> Asks { get; init; } = [];

    /// <summary>The folders the last look held for the agent's trust (D73), as the tick hands them.</summary>
    public IReadOnlyList<JsonObject> Untrusted { get; init; } = [];

    /// <summary>What agents proposed about the rules (PERM2), as <c>RULES</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Proposals { get; init; } = [];

    /// <summary>What the last look considered of each open quest, as the tick hands it.</summary>
    public IReadOnlyList<JsonObject> Considered { get; init; } = [];

    /// <summary>Each session's place in Sessions' list, as the groups slice holds its rows.</summary>
    public IReadOnlyList<JsonObject> Groups { get; init; } = [];

    /// <summary>The second opinions that wait on the person, as <c>OPINION_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Opinions { get; init; } = [];

    /// <summary>The agents' asks for a helper that wait on the person's yes, as <c>HELPER_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Helpers { get; init; } = [];

    /// <summary>The sessions the watcher brings to the person (SESSWATCH1a), as its memory holds them.</summary>
    public IReadOnlyList<JsonObject> Quiet { get; init; } = [];

    /// <summary>
    /// The sessions that waited on their own helper whose move back to the person the service refused (HELPERWAIT1c), as the look kept
    /// them (<see cref="AttentionStateSlice.BackWaits"/>): each session, its quest, the service's words and since when.
    /// </summary>
    public IReadOnlyList<JsonObject> Backs { get; init; } = [];

    /// <summary>The agents' asks to hand their sessions over that wait on the person's yes or no (NOTIFY2e), as the look holds them.</summary>
    public IReadOnlyList<JsonObject> HandOvers { get; init; } = [];

    /// <summary>Each unit of work's milestone, as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Works { get; init; } = [];

    /// <summary>The runs held at a declined request (INTEGRATE1o part o3), as the runtime last read them: each run, its node, the declined quest and where it went.</summary>
    public IReadOnlyList<JsonObject> Requests { get; init; } = [];

    /// <summary>The runs waiting on a person's task (NODEKIT1 HUMAN), as the runtime last governed them: each run, its node and the presses it waits for.</summary>
    public IReadOnlyList<JsonObject> Tasks { get; init; } = [];

    /// <summary>
    /// The integration's waits on the owner (INTEGRATE1h), as <see cref="AttentionStateSlice.IntegrationWaits"/> reads them: a candidate held for
    /// a decide, a superseded commit awaiting its answer, a landing whose integration waits for the owner's card, a declaration owed the
    /// owner's acceptance; each with its repository, workspace and since when.
    /// </summary>
    public IReadOnlyList<JsonObject> Integration { get; init; } = [];

    /// <summary>Every plugin step's attempt held for the person, as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Held { get; init; } = [];

    /// <summary>Every node record that does not read (FABRIC-PERFORM1j), as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Unreadable { get; init; } = [];

    /// <summary>Every closure graph's end (FABRIC-CLOSE1j), as the work slice holds them.</summary>
    public IReadOnlyList<JsonObject> Ends { get; init; } = [];

    /// <summary>The tool calls the sessions hold for the person's answer, as <c>PERMISSION_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Permissions { get; init; } = [];

    /// <summary>The answers the answers-alone bound holds for the person (COWORK1e), as <c>ANSWER_WAITS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Answers { get; init; } = [];

    /// <summary>
    /// The sessions whose memory changes wait on the person (KNOWCENTRAL2e), as the service reads them: each session, the ends and replacements
    /// awaiting its approver, a stopped session's staged rows, and since when.
    /// </summary>
    public IReadOnlyList<JsonObject> Memory { get; init; } = [];

    /// <summary>The roster's doors, each as <c>HARNESSES</c> answers it: each account's last reading with its time (ROSTER1).</summary>
    public IReadOnlyList<JsonObject> Doors { get; init; } = [];

    /// <summary>How each agent's accounts are used, as <c>ACCOUNTS</c> answers it (<c>{agents}</c>); null where it has not answered.</summary>
    public JsonObject? Use { get; init; }

    /// <summary>The starts the last look held on their accounts, an intake's among them, as the tick hands them (UX6d).</summary>
    public IReadOnlyList<JsonObject> Waits { get; init; } = [];

    /// <summary>The presses a terminal sent that wait for the window's Confirm (CONFIRMSEEN1), as <c>PERSON_CONFIRMATIONS</c> answers them.</summary>
    public IReadOnlyList<JsonObject> Confirmations { get; init; } = [];

    /// <summary>
    /// Each volume still below its free-space floor once the floor's acts ran (KEEP1f), as the look's pass said it
    /// (<see cref="AttentionStateSlice.SpaceWaits"/>): its root, free space, size and floor, what the floor freed and what the press could.
    /// </summary>
    public IReadOnlyList<JsonObject> Space { get; init; } = [];

    /// <summary>
    /// Each remote Daoris holds since it could not authenticate (GITAUTH1a, <see cref="AttentionStateSlice.GitWaits"/>): its repository,
    /// workspace, remote, host, class, what it holds, what the transport said and since when.
    /// </summary>
    public IReadOnlyList<JsonObject> Git { get; init; } = [];

    /// <summary>
    /// Each git go-ahead a door holds while its act waits (GITAUTH1f, <see cref="AttentionStateSlice.GitAskWaits"/>): its id, repository,
    /// workspace, remote, host, class, session, since when, and whether its door has gone.
    /// </summary>
    public IReadOnlyList<JsonObject> GitAsks { get; init; } = [];
}
