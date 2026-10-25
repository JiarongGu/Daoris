using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The session this machine last ran on a quest, and the tree it ran in (D79, D80).</summary>
/// <param name="Session">Its record's id.</param>
/// <param name="Tree">Where it ran — its own tree where the repository opted in, the root otherwise; null when unsaid.</param>
/// <param name="State">How its record ended — `failed` is a cut-off, which a later session carries on (D80).</param>
/// <param name="Note">What its record said about that ending — the words a session carrying on is told.</param>
/// <param name="Repository">Where it ran — whether a chain's next step can build on its tree (CHAIN2).</param>
/// <param name="Answer">The person's answer when it parked to ask them (STANDDOWN2) — what a carry-on is handed.</param>
/// <param name="Interrupted">
/// A `stopped` ending that was not the person's (D104): the orphan sweep's, or the driver's shutdown — a
/// cut-off, carried on like a `failed` one.
/// </param>
public sealed record PriorSession(
    string Session, string? Tree, string State = "", string? Note = null, string? Repository = null,
    string? Answer = null, bool Interrupted = false)
{
    public string? Actor { get; init; }
    public string? Choice { get; init; }
    public ActorAsked? Asked { get; init; }
    public System.Text.Json.JsonElement? Override { get; init; }
    public ActorRan? Ran { get; init; }
    /// <summary>
    /// Whether it ended because the person stopped it (D104): <c>stopped</c>, and not by the sweep or a shutdown. Such a stop
    /// holds its quest until the person releases it (SESSUX1b, D126 §3.3).
    /// </summary>
    public bool PersonStopped => string.Equals(State, "stopped", StringComparison.OrdinalIgnoreCase) && !Interrupted;

    /// <summary>
    /// Whether that stop was the steward's (STEWARDROLE1b, D209 §5.1), by its note's code, never its English: a sibling of the person's
    /// stop that holds its quest the same way, for the steward's or the owner's <i>Try again</i>, and is said as the steward's.
    /// </summary>
    public bool StewardStopped => PersonStopped && NoteParts?.Any(part =>
        part.Code == RecordNotes.EndedStoppedSteward || part.Code == RecordNotes.StoppedCheckpointStoppedSteward) == true;

    /// <summary>
    /// The account it ran on, as its record names it, or null for the tool's own home (D49 §4): what a carry-on is
    /// compared with, to say whether it runs on another account (TOOL4f, D125 §3.5). Served on loopback alone, which is
    /// where this machine's driver reads its own records.
    /// </summary>
    public string? Profile { get; init; }

    /// <summary>Its record says an account's limit made its failure (TOOL4c): what a carry-on is told after one (TOOL4f).</summary>
    public bool Limit { get; init; }

    /// <summary>
    /// It ended <c>choice-not-offered</c> (D205 §3.5(b), (c)), read by its end code: its agent refused the choice at the open, so no
    /// conversation began there, and a conversation's go-on is a fresh start of its actor (MODELROLE1d2).
    /// </summary>
    public bool NotOffered { get; init; }

    /// <summary>The adapter its record opened on (ANSWER1a, D131 §1): an answer resumes its conversation only on the same one.</summary>
    public string? Adapter { get; init; }

    /// <summary>
    /// The access mode an Ask Daoris record last ran under (HELPMODE1a2, D183 point 3), as the service answers it on loopback; null for
    /// every other record and from a host before the field, which <see cref="HelpModes.Recorded"/> reads as restricted.
    /// </summary>
    public string? HelpMode { get; init; }

    /// <summary>
    /// The access setup a driven record started under (AUTONOMY1a, D184 point 1), each pause with its value, as the service answers it on
    /// loopback: what a resume or a carry-on compares its own with, to say the change, its permission prompts
    /// (AUTONOMY1b, <see cref="DrivenAccess.ChangedSince"/>) and its go-aheads (AUTONOMY1c, <see cref="Continuations.GoAheadsChanged"/>).
    /// Null from a record before the setting and from a host before the field, which <see cref="DrivenAccess.Ran"/> reads as today's.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Access { get; init; }

    /// <summary>The harness version its record opened on: a resumed run says so where the version moved since (D131 §1).</summary>
    public string? HarnessVersion { get; init; }

    /// <summary>
    /// The commit its tree stood at when it opened (SURF6): a resumed run's evidence is counted from it, so the review's
    /// range is the whole session's (ANSWER1a, D131 §3). Served on loopback, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>
    /// A park the person answered, still parked (D131 §1): its own record goes on, and its harness conversation resumes
    /// where it can. An answer a service from before ANSWER1b took has already ended the record, and is a carry-on.
    /// </summary>
    public bool AnsweredPark => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase) && Answer is not null;

    /// <summary>
    /// The person's words waiting on its record for it to go on with (MSG1a, D137 §2.4), in the order said; null where the
    /// host answers no <c>said</c>, one from before MSG1a, and empty where nothing waits.
    /// </summary>
    public IReadOnlyList<SaidWordView>? Said { get; init; }

    /// <summary>The ask an intake answers (D65 §1b), or null for every other record: an intake never goes on (D137 §2.2).</summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The work the record was opened for, as the service answers it on loopback (FOUNDATION1c, D212; <see cref="ParticipantWork"/>); null
    /// for a teammate's mirror, which has none here, and from a host before the field, whatever its marks say. Read by nothing yet: the
    /// readers that tell the work build it from the marks (<see cref="WorkMarks"/>), which every host answers.
    /// </summary>
    public string? Work { get; init; }

    /// <summary>
    /// <c>driven</c> or <c>chat</c>, as the record says: an ended chat the person wrote to goes on through the chat runner
    /// (MSG1c), never the planner, which plans a quest's sessions.
    /// </summary>
    public string Kind { get; init; } = "driven";

    /// <summary>
    /// Its note's lines, each by its code with its values (LANG1a, D142 point 2): what a line the driver adds to its note carries
    /// on. Null for a record from before parts, whose note is then carried whole.
    /// </summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>Its note as composed: its parts, or its English whole as one part from before them.</summary>
    public Noted AsNoted() => Noted.From(Note, NoteParts);

    /// <summary>Waiting on the person (D83): <c>awaiting-person</c>, the one state a record goes on from without leaving an ended one.</summary>
    public bool Parked => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase);

    /// <summary>A record that came down from the team, keyed <c>origin/id</c> (D47 §6): never this machine's to go on with.</summary>
    public bool Teammate => Session.Contains('/');

    /// <summary>
    /// When its record last moved, as the service answered it (<c>updated</c>): for an ended record, when it ended. What a
    /// take made here is compared with, to say whether it came after (CARRY2b). Null where unsaid.
    /// </summary>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>
    /// When its record was made, as the service answered it (<c>created</c>): for a conversation that continues another by the
    /// usage curve (<see cref="FromWhy"/> <c>curve</c>), when it moved, which holds the next move for thirty minutes (SWITCH1c-ii,
    /// the switch design §2). Null where unsaid.
    /// </summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>
    /// Whether the person's words wait for it to go on with (MSG1b, D137 §2.2): its <c>said</c> holds any, parked or ended;
    /// from a host before <c>said</c>, an answered park is the one case that waits.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Never while it runs</b> (ANSWER2): a resumed run moves its record to working and takes the words off it only as
    /// it concludes, so for the whole run they are still on it. Read as waiting, a look took the record up a second time,
    /// whose move to working the ledger refused (working → working), and the driver failed the session it was resuming.
    /// </remarks>
    public bool WordsWaiting => !Running && (Said is { } said ? said.Count > 0 : AnsweredPark);

    /// <summary>
    /// Queued, starting or working: a run has the record (D46 §4), so the words on it are on their way to the session, and
    /// nothing waits for a look to take it up (ANSWER2). A park is not running: it waits on the person.
    /// </summary>
    public bool Running => State.ToLowerInvariant() is "queued" or "starting" or "working";

    /// <summary>
    /// The words it goes on with, in order: each of <see cref="Said"/>, or a host's answer from before <c>said</c>. A word a shell before
    /// this one attempted is settled as this one comes up, before any go-on (COWORK1c, <see cref="UncertainWords"/>); one this shell
    /// attempted and kept waiting after a failed turn goes again (TURNFAIL1).
    /// </summary>
    public IReadOnlyList<SaidWordView> Waiting =>
        Said is { } said ? said
        : Answer is { } answer ? [new SaidWordView(null, answer, DateTimeOffset.MinValue, [], false)]
        : [];

    /// <summary>
    /// The second opinions whose findings wait among its words (XAGENT1e, D155 point 7), each once, in the order handed: another
    /// agent's claims, which go on only in this session's own conversation and never to a new session as the person's words.
    /// </summary>
    public IReadOnlyList<string> Findings => [.. Waiting.Select(word => word.By).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The helper sessions whose answers wait among its words (MIXAGENT2a-ii), each once, in the order handed: another agent's
    /// claims, which go on only in this session's own conversation, as findings do.
    /// </summary>
    public IReadOnlyList<string> Helpers => [.. Waiting.Select(word => word.Helper).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>The session this one answers, when it is a helper's record (MIXAGENT2a-i's <c>serves</c>); null for every other.</summary>
    public string? Serves { get; init; }

    /// <summary>
    /// The piece this one writes, the ask's id, when it is a partner's record (COAGENT1b, the co-agent design §7.3): beside its
    /// <see cref="Serves"/>, which names its lead; null for every other, and from a host before the field.
    /// </summary>
    public string? Piece { get; init; }

    /// <summary>The second opinion this one reads for, when it is a reviewer's record; null for every other.</summary>
    public string? Opinion { get; init; }

    /// <summary>
    /// The session this one continues (MIXAGENT1a-iii's <c>from</c>): a conversation handed over, or a quest's session carried on
    /// after a hand-over or onto another account after a limit; null for every other, and from a host from before.
    /// </summary>
    public string? From { get; init; }

    /// <summary>Why it continues <see cref="From"/>, by <see cref="HandOverWhy"/>'s codes; null with it.</summary>
    public string? FromWhy { get; init; }

    /// <summary>Who asked for it to continue <see cref="From"/>, by <see cref="HandOverBy"/>'s codes; null with it.</summary>
    public string? FromBy { get; init; }

    /// <summary>The workspace the record belongs to, as it names it: where a rule is resolved for it.</summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// The session entry it is a part of (SESSONE1a, D167 point 2), <c>&lt;origin&gt;/&lt;repository&gt;</c>, as the service answers
    /// it: what a later quest of the same line goes into its tree by (SESSONE1d). Null from a host before the key.
    /// </summary>
    public string? Entry { get; init; }

    /// <summary>The quest it served, as its record names it; null for a conversation's. What a later part is told it worked on.</summary>
    public string? Quest { get; init; }
}

/// <summary>
/// One of the person's words waiting on a record (MSG1a, D137 §2.4), as the service answers it to this machine: its id,
/// which the record's events say again where the session took it, the words, when, its files' names, and whether it was
/// said after the record ended.
/// </summary>
/// <param name="Id">Its id on the record; null for an answer a host from before <c>said</c> kept, which has none.</param>
public sealed record SaidWordView(string? Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens)
{
    /// <summary>
    /// The second opinion whose findings these are (XAGENT1e, D155 point 7, D137 as it amends it): another agent's claims in
    /// Daoris's fixed words, never the person's. Null for the person's words, as every word kept before it was.
    /// </summary>
    public string? By { get; init; }

    /// <summary>
    /// The helper session whose answer this is (MIXAGENT2a-ii, D163 point 4): another agent's claim, never the person's words and
    /// no second opinion's findings. Null for every other word.
    /// </summary>
    public string? Helper { get; init; }

    /// <summary>
    /// Its author where that is not the person (COWORK1b, D188; the message-urgency design §9): <see cref="DaorisWords.From"/> for
    /// Daoris's own words, a reopen or a step's go-on. Null for every other word, and for a host's from before the field, whose
    /// words read as they did.
    /// </summary>
    public string? From { get; init; }

    /// <summary>Its producer's key (COWORK1b, the design §6): Daoris's fact's, read by <see cref="DaorisKeys"/>; null for a word kept with none.</summary>
    public string? Key { get; init; }

    /// <summary>
    /// When its sender asked it to reach a working session (COWORK1c, the design §9); null for a word kept with none, which reads as
    /// its old reach: the person's as urgent, any other as ordinary.
    /// </summary>
    public MessageLevel? Level { get; init; }

    /// <summary>
    /// When a dispatcher first wrote it to a harness (COWORK1c, the design §6): a word with one, still waiting when a shell comes up,
    /// may have reached the agent. Null for a word never attempted, and a host's from before the field.
    /// </summary>
    public DateTimeOffset? Attempt { get; init; }

    /// <summary>
    /// The answer of its park's question the person pressed (PARKVIEW1, the park's ask design §4), read from the event its words were
    /// shown under (<see cref="SessionEvent.Choice"/>), since the record keeps words alone. Null for every other word.
    /// </summary>
    public string? Choice { get; init; }

    /// <summary>
    /// What the session is handed of these words: a pressed answer as <c>chose: &lt;label&gt;</c>, then the person's own words after a
    /// blank line, so the agent tells the answer it offered from words it did not (PARKVIEW1); every other word as kept.
    /// </summary>
    public string Handed => PressedWords.Handed(Text, Choice);

    /// <summary>
    /// The supervisor's decision a <see cref="SupervisorHanded.From"/> word claims (SUPERVISOR1a-i, D208): a claim on the record, never
    /// an authority. Whether it is the supervisor's is <see cref="QuestionSupervisorDecisions.Authored"/>'s, by its id.
    /// </summary>
    public string? Decision { get; init; }

    /// <summary>Whether these are Daoris's own words (COWORK1b): read from its mark, never its sentence.</summary>
    public bool Daoris => From == SaidFrom.Daoris;

    /// <summary>Whether these words claim the supervisor (SUPERVISOR1a-i): a claim only, which the look attributes or quarantines.</summary>
    public bool Supervisors => From == SaidFrom.Supervisor;

    /// <summary>Whether these are the person's words: no second opinion's, no helper's and not Daoris's.</summary>
    public bool Persons => By is null && Helper is null && From is null;
}

/// <summary>An ACTIVE session as the service answered it — closed ones never reach the planner.</summary>
/// <param name="State">
/// Where the record stands. The planner has never needed it — "is this repository busy" is the only
/// question it asks — but <see cref="AttentionWatch"/> does, because a park is a state change nothing
/// local performs and is therefore only visible by looking (SURF5b).
/// </param>
/// <param name="Note">What the session said about that state, where it said anything.</param>
public sealed record SessionView(
    string Id, string Repository, string State = "", string? Note = null)
{
    /// <summary>
    /// The ask an INTAKE answers (D65 §1b), or null for every other session. Its "repository" is
    /// <c>ask #id</c>, which no quest names — so the planner never finds it busy with anything.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The tree it holds (D51), or null when unsaid — where a repository opens a tree per session, the
    /// tree is the lock the planner asks about (PAR1).
    /// </summary>
    public string? Tree { get; init; }

    /// <summary>
    /// The quest it serves, or null for a conversation's or an intake's. A session outlives the look that
    /// started it (DEV3), so the next look can find its quest still open, and must not start it again.
    /// </summary>
    public string? Quest { get; init; }

    /// <summary>
    /// The commit its tree stood at when it opened (SURF6), or null where unsaid: what a lost session's done is read as changed
    /// from when the sweep ends it (EVID1b, D144 §3). Answered on loopback alone, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>Its note's lines by code (LANG1a), handed on wherever its note is; null for a record from before parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>
    /// The person's answer to its park (STANDDOWN2), or null: an answered park goes on at the driver's next look, so it waits
    /// on nobody (ANSWER1c), which Ask Daoris's room reads as the Sessions list does (ENTRY1f2). Answered on loopback alone.
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>
    /// The workspace its record is filed under (D48), <c>default</c> where a host from before answers none: what Ask Daoris's
    /// start hands the running conversation of its own workspace by (ASKSCOPE1a).
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// The record's kind as the service answers it, <c>driven</c> or <c>chat</c> (D49 §3); <c>driven</c> where a host answers none,
    /// as every reader of the records takes it. What <see cref="ActiveSessions.HoldsSlot"/> tells the person's conversations from.
    /// </summary>
    public string Kind { get; init; } = SessionKinds.Driven;

    /// <summary>
    /// The second opinion it reads another session's work for, or null for every other session (XAGENT1c): a reviewer the driver
    /// starts, whose record is a chat that serves no quest. Answered on loopback alone.
    /// </summary>
    public string? Opinion { get; init; }

    /// <summary>
    /// The session it answers, or null for every other session (MIXAGENT2a-i): a helper the driver starts, whose record is a chat that
    /// serves no quest. Answered on loopback alone.
    /// </summary>
    public string? Serves { get; init; }

    /// <summary>
    /// The piece a partner's record writes, the ask's id, or null for every other session (COAGENT1b, the co-agent design §7.3): beside
    /// its <see cref="Serves"/>, which names its lead. What tells the common admission a piece's record has opened, so its reservation
    /// is counted once. Answered on loopback alone.
    /// </summary>
    public string? Piece { get; init; }
}

/// <summary>The one spelling of the state that means "only the person can clear this" (D46).</summary>
/// <remarks>
/// A constant rather than a literal because three things compare against it — the watch, the shell's
/// surface and the ledger — and a fourth will; three copies of a string disagree eventually, and the
/// disagreement is silent.
/// </remarks>
public static class SessionStates
{
    public const string AwaitingPerson = "awaiting-person";

    public static bool IsParked(string? state) =>
        string.Equals(state, AwaitingPerson, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A session running on this home as the downloads' guard reads it (KEEP1b, D195 point 6; the cleanup design §3.5, Fable F2.5): its id, when
/// its record was opened, and the downloaded versions its starts were handed, <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>; null
/// for a record from before that field, which keeps the start-time rule.
/// </summary>
public sealed record LiveSession(string Id, DateTimeOffset? Started, IReadOnlyList<string>? Resolved);
