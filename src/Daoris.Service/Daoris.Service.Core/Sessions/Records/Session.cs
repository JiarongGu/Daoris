using System.Text.Json;

namespace Daoris.Knowledge;

/// <param name="Id">Short random handle — an attempt, so never content-derived: a retry is a new record.</param>
/// <param name="Quest">
/// The quest this session is started to serve — <b>null for a chat</b>, which may serve none.
/// A driven session always has one: it is what the driver planned from.
/// </param>
/// <param name="Repository">The repository it runs in — the quest's receiver, denormalized for listing.</param>
/// <param name="Door">Which door spawned it. A name for the record, never a model.</param>
/// <param name="State">Where it is.</param>
/// <param name="Note">What the driver observed, when a state needs explaining.</param>
/// <param name="Evidence">The outcome bundle the person reviews — gate results, landed commits.</param>
/// <param name="Transcript">A machine-local path to the captured run. Diagnostic, never the record.</param>
/// <param name="Created">When the driver queued it.</param>
/// <param name="Updated">When its state last moved.</param>
/// <param name="Workspace">
/// The circle this record travels within — taken from its quest, never passed alongside it, so
/// the two can never disagree. The driver itself is per machine and crosses workspaces:
/// the workspace decides where a record travels, not which machine may work it.
/// </param>
/// <param name="Kind">
/// Driven or chat. It changes almost nothing — the same record, the same observed lifecycle,
/// the same one-session-per-repository lock — which is the point: a conversation is a session, not a
/// second kind of thing with its own rules to keep in step.
/// </param>
/// <param name="DoorVersion">
/// The harness version observed at spawn — "which tool produced this", answerable later. The
/// same authorship instinct as version-stamping a release, applied to the tool that did the work. It
/// travels: it is a fact about a tool, not about a machine.
/// </param>
/// <param name="Profile">
/// Which named credential profile the session ran as — a NAME, never a path and never
/// anything from inside the profile.
/// </param>
/// <param name="Tree">
/// The working tree this session holds — <b>the unit of exclusion</b>, and what the ledger keys
/// the lock on. Null means the repository's main tree, whose path this deployment may not know: a
/// record kept without one, or one mirrored from another machine.
/// </param>
/// <remarks>
/// <b>The profile name and the tree are machine-local, and are guarded exactly as the transcript
/// is</b>. The profile answers "which account did this run as" to the machine that ran it;
/// the tree is a filesystem path, which is the sharpest reason of the three. Both are wiring: WHERE and WHO are the
/// machine's and the account's. The version is a fact
/// about a tool and travels; these do not.
/// </remarks>
public sealed record Session(
    string Id,
    string? Quest,
    string Repository,
    string Door,
    SessionState State,
    string? Note,
    string? Evidence,
    string? Transcript,
    DateTimeOffset Created,
    DateTimeOffset Updated,
    string Workspace = Workspaces.Default,
    SessionKind Kind = SessionKind.Driven,
    string? DoorVersion = null,
    string? Profile = null,
    string? Tree = null,
    string? BaseCommit = null)
{
    /// <summary>
    /// Whose record this is, when it is not this machine's: the key the remote knows its machine by.
    /// Null for this machine's own; a record that has one is the team's — its id is
    /// `origin/id`, it is read-only here, and it holds no tree on this machine.
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>
    /// The ask this session is opened to answer — an INTAKE, a conversation Daoris opened
    /// for an ask in a room it owns rather than in any repository. Null for every other session.
    /// </summary>
    /// <remarks>
    /// The kind stays <see cref="SessionKind.Chat"/>: an intake serves no quest and is never planned
    /// from one, which is exactly what a build that predates it already reads a chat as. A new kind
    /// would read as DRIVEN there — the one reading that is wrong.
    /// </remarks>
    public string? Ask { get; init; }

    /// <summary>
    /// The second opinion this session reads another session's work for, as its reviewer — null
    /// for every other session.
    /// </summary>
    /// <remarks>
    /// <para>The kind stays <see cref="SessionKind.Chat"/>, serving no quest, for the intake's reason above: a build from
    /// before reads it as a conversation nothing plans from, which is what it is.</para>
    ///
    /// <para><b>This machine's own, like the opinion it names</b>: no wire carries it, so a teammate reads a
    /// conversation, and a caller off this machine is never answered it.</para>
    /// </remarks>
    public string? Opinion { get; init; }

    /// <summary>
    /// The session this one answers, as its helper — null for every other session: the asker's
    /// id, in the same repository. A helper is read-only and takes one turn, so it is never resumed and takes no words.
    /// </summary>
    /// <remarks>
    /// <b>This machine's own, as <see cref="Opinion"/> is</b>: no wire carries it, and a caller off this machine is
    /// never answered it. The kind stays <see cref="SessionKind.Chat"/>, serving no quest, for the reviewer's reason above.
    /// </remarks>
    public string? Serves { get; init; }

    /// <summary>
    /// The piece this record writes, as a partner: the ask's id under the home's <c>partners</c> box, beside
    /// <see cref="Serves"/>, which names its lead. Null for every other record, a helper's included.
    /// </summary>
    /// <remarks><b>This machine's own, as <see cref="Serves"/> is</b>: no wire carries it, and a caller off this machine is never answered it.</remarks>
    public string? Piece { get; init; }

    /// <summary>The attempt a supervisor's turn decides for, read before every other mark; this machine's own, as <see cref="Serves"/> is.</summary>
    public string? Decides { get; init; }

    /// <summary>
    /// The uncommitted paths of its own its last turn left in its git place, relative to the place's root, or null: while it is
    /// parked or waiting with them it holds the place (<see cref="SessionPlaceLease"/>). This machine's own, as <see cref="Serves"/> is.
    /// </summary>
    public IReadOnlyList<string>? HoldsPlace { get; init; }

    /// <summary>
    /// The session this one continues: a conversation handed over, a quest's session carried on
    /// after a hand-over or onto another account after a limit — null for every other session, and for a record from before.
    /// </summary>
    /// <remarks>
    /// <b>This machine's own, as <see cref="Opinion"/> is</b>: no wire carries it, and a caller off this machine is
    /// never answered it. Written by the driver at the open, which is the half that knows, and judged by none here.
    /// </remarks>
    public string? From { get; init; }

    /// <summary>Why it continues <see cref="From"/>, by the driver's code (<c>press</c>, <c>near</c>, <c>limit</c>, <c>agent</c>, <c>curve</c>); null with it.</summary>
    public string? FromWhy { get; init; }

    /// <summary>Who asked for it to continue <see cref="From"/>, by the driver's code (<c>person</c>, <c>daoris</c>, <c>agent</c>); null with it.</summary>
    public string? FromBy { get; init; }

    /// <summary>
    /// The access mode an Ask Daoris record's run actually gets: <c>restricted</c> or <c>full</c>, the
    /// effective mode, kept apart from the mode the person chose, which a switch reaches only at its next start. Written at the open
    /// and again where a conversation going on runs under another; a help record from before reads <c>restricted</c>, what every
    /// start before the modes ran under, and every other record has none.
    /// </summary>
    /// <remarks>
    /// <b>This machine's own, as <see cref="Opinion"/> is</b>: answered to a caller on it only, and no feed carries it, since a
    /// help record never travels (<see cref="SessionLedger.IsHelp"/>). Moving it writes no revision.
    /// </remarks>
    public string? HelpMode { get; init; }

    /// <summary>The kind of work this start serves. Machine-local, absent on older and mirrored records.</summary>
    public string? Actor { get; init; }
    /// <summary>The choice taken.</summary>
    public string? Choice { get; init; }
    /// <summary>The tagged model and effort the start asked for, machine-local.</summary>
    public JsonElement? Asked { get; init; }
    /// <summary>The composer's last acknowledged change, machine-local.</summary>
    public JsonElement? Override { get; init; }
    /// <summary>Reported model names and dated acknowledgements, machine-local.</summary>
    public JsonElement? Ran { get; init; }
    /// <summary>STAFFPOLICY1c2: why the role's policy chose this start's choice (<c>rule</c>, <c>by</c>, and <c>condition</c> and <c>from</c> for a move), machine-local; absent under no policy.</summary>
    public JsonElement? Chosen { get; init; }

    /// <summary>
    /// The access setup a driven session started under: each pause with its value, as the driver resolved it
    /// at the start (<see cref="SessionAccess"/>). Written at the open and never again; null for a record told none, every other kind
    /// among them, and for a record from before the setup, which ran as today's behaviour.
    /// </summary>
    /// <remarks>
    /// <b>This machine's own, as <see cref="Opinion"/> is</b>: no wire carries it, and a caller off this machine is never
    /// answered it.
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Access { get; init; }

    /// <summary>
    /// The downloaded versions the record's starts get, each
    /// <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>, in order: the driver names them before each spawn, a go-on's added to the first's,
    /// and the downloads' rule keeps each while the session runs. Null for a record told none, a record from before the field among them,
    /// which keeps the start-time rule.
    /// </summary>
    /// <remarks>
    /// <b>This machine's own, as <see cref="Opinion"/> is</b>: no wire carries it, and a caller off this machine is never
    /// answered it. It names versions, never a path.
    /// </remarks>
    public IReadOnlyList<string>? Resolved { get; init; }

    /// <summary>
    /// The line of work this record belongs to, <c>&lt;origin&gt;/&lt;repository&gt;</c>: written by the ledger
    /// when the session opens, and the one key the records that continue one line of work share. Null for a record from before it, which
    /// every reader is answered the rule's key for (<see cref="SessionEntries"/>), never written back.
    /// </summary>
    /// <remarks>
    /// It <b>travels</b> with the record, unlike <see cref="From"/>: it names a line of work and a repository, no path and no account.
    /// </remarks>
    public string? Entry { get; init; }

    /// <summary>
    /// Whether this session took its own quest, through its own connector. A session
    /// ending with its quest still taken is then waiting on the person, not standing down for
    /// somebody else — which the quest's state alone cannot tell.
    /// </summary>
    public bool Took { get; init; }

    /// <summary>
    /// The quest a conversation took: a chat serves none of its own, so <see cref="Took"/> never says which. It travels
    /// with the record, since it names a quest and no path; null for a chat from before the field, which keys its quests as it did.
    /// </summary>
    public string? TookQuest { get; init; }

    /// <summary>
    /// The person's words waiting for this record to go on with them, in the order said: an answer to
    /// a park, or words to a session that ended, which reopen it. A second word joins the first and
    /// never replaces it. Kept until taken, by the resumed run's first prompt or the session a fallback handed them to;
    /// parking again clears them, so a session that asks again is answered anew.
    /// </summary>
    /// <remarks>
    /// <b>This machine's own, like the transcript</b>: answered only to a caller on it, never on the wire to a
    /// remote, and keeping them writes no revision, since nothing that travels changed.
    /// </remarks>
    public IReadOnlyList<SaidWord> Said { get; init; } = [];

    /// <summary>Whether any of <see cref="Said"/> is the person's: an answered park, or words to an ended session (never another agent's or Daoris's own).</summary>
    public bool PersonSaid => Said.Any(word => word.Persons);

    /// <summary>
    /// A <see cref="SessionState.Stopped"/> record that is not the person's stop: the orphan
    /// sweep found nothing running it, or the driver shut down under it. A take ended so is carried on
    /// like a failed one; a person's stop is carried on only once they release it, and only of a take
    /// a session here made. False for everything else, and for every record from before the
    /// field.
    /// </summary>
    public bool Interrupted { get; init; }

    /// <summary>
    /// A <see cref="SessionState.Failed"/> record whose turn an account's limit refused,
    /// as the driver read it from the door's failure. The quest waits for the reset or rotates rather than
    /// spending a strike on it, which is the driver's to decide. False for everything else, and for every
    /// record from before the field, which is the old reading: a failure like any other.
    /// record from before the field: a failure like any other.
    /// <remarks>
    /// <b>It names no account</b>, so unlike <see cref="Profile"/> it is served to every caller and travels
    /// with the record: a teammate reading a quest's sessions sees that one is cut off by a limit, never
    /// whose.
    /// </remarks>
    public bool Limit { get; init; }

    /// <summary>
    /// Whether this machine's record went up to a workspace's remote: the team holds a copy, and a
    /// session record does not travel as a deletion, so a delete here would leave theirs. Kept once said, through every
    /// later move, since the copy stays. This machine's own fact: it never travels.
    /// </summary>
    public bool Pushed { get; init; }

    /// <summary>
    /// The note's parts, each line's code and values beside its English: a JSON
    /// list as <see cref="Knowledge.NoteParts"/> keeps it. Null for a record from before parts, and for a note written without
    /// them, whose note then stands as kept.
    /// </summary>
    /// <remarks>It travels with the note, every string in it cleaned as the note is (<see cref="Knowledge.NoteParts.ForAnotherMachine"/>).</remarks>
    public string? NoteParts { get; init; }

    /// <summary>Whether this session still holds its repository. Parked counts: the person is the flow control.</summary>
    public bool Active => State is SessionState.Queued or SessionState.Starting
        or SessionState.Working or SessionState.AwaitingPerson;

    /// <summary>This state's public spelling — what every door prints and accepts back.</summary>
    public string StateName => Spell(State);

    /// <summary>The design's spelling: kebab-case on the wire, enum names in the store.</summary>
    public static string Spell(SessionState state) => state switch
    {
        SessionState.AwaitingPerson => "awaiting-person",
        SessionState.StoodDown => "stood-down",
        _ => state.ToString().ToLowerInvariant(),
    };

    /// <summary>The reverse of <see cref="Spell"/>: what every door accepts back, tolerantly.</summary>
    public static bool TryParse(string value, out SessionState state) =>
        Enum.TryParse(value.Replace("-", "", StringComparison.Ordinal), ignoreCase: true, out state)
        && Enum.IsDefined(state);
}
