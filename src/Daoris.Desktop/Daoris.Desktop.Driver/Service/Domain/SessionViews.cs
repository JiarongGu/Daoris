using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The session this machine last ran on a quest, and the tree it ran in.</summary>
/// <param name="Session">Its record's id.</param>
/// <param name="Tree">Where it ran — its own tree where the repository opted in, the root otherwise; null when unsaid.</param>
/// <param name="State">How its record ended — `failed` is a cut-off, which a later session carries on.</param>
/// <param name="Note">What its record says about that ending — the words a session carrying on is told.</param>
/// <param name="Repository">Where it ran — whether a chain's next step can build on its tree.</param>
/// <param name="Interrupted">
/// A `stopped` ending that is not the person's: the orphan sweep's, or the driver's shutdown — a
/// cut-off, carried on like a `failed` one.
/// </param>
public sealed record PriorSession(
    string Session, string? Tree, string State = "", string? Note = null, string? Repository = null,
    bool Interrupted = false)
{
    public string? Actor { get; init; }
    public string? Choice { get; init; }
    public ActorAsked? Asked { get; init; }
    public System.Text.Json.JsonElement? Override { get; init; }
    public ActorRan? Ran { get; init; }
    public ActorChosen? Chosen { get; init; }
    /// <summary>
    /// Whether it ended because the person stopped it: <c>stopped</c>, and not by the sweep or a shutdown. Such a stop
    /// holds its quest until the person releases it.
    /// </summary>
    public bool PersonStopped => string.Equals(State, "stopped", StringComparison.OrdinalIgnoreCase) && !Interrupted;

    /// <summary>
    /// Whether that stop is the steward's, by its note's code, never its English: a sibling of the person's
    /// stop that holds its quest the same way, for the steward's or the owner's <i>Try again</i>, and is said as the steward's.
    /// </summary>
    public bool StewardStopped => PersonStopped && NoteParts?.Any(part =>
        part.Code == RecordNotes.EndedStoppedSteward || part.Code == RecordNotes.StoppedCheckpointStoppedSteward
        || part.Code == RecordNotes.StoppedPausedQuestSteward) == true;

    /// <summary>
    /// The account it ran on, as its record names it, or null for the tool's own home: what a carry-on is
    /// compared with, to say whether it runs on another account. Served on loopback alone, which is
    /// where this machine's driver reads its own records.
    /// </summary>
    public string? Profile { get; init; }

    /// <summary>Its record says an account's limit made its failure: what a carry-on is told after one.</summary>
    public bool Limit { get; init; }

    /// <summary>
    /// It ended <c>choice-not-offered</c>, read by its end code: its agent refused the choice at the open, so no
    /// conversation began there, and a conversation's go-on is a fresh start of its actor.
    /// </summary>
    public bool NotOffered { get; init; }

    /// <summary>
    /// Its opening passed its bound (<c>chat.not-up</c>, Ask Daoris's <c>chat.help-not-up</c>), read by its end code: where no
    /// conversation of it is kept, its harness never opened one, and its go-on is a fresh start of its actor, as a refused choice's is.
    /// </summary>
    public bool NotUp { get; init; }

    /// <summary>The adapter its record opened on: an answer resumes its conversation only on the same one.</summary>
    public string? Adapter { get; init; }

    /// <summary>
    /// The access mode an Ask Daoris record last ran under, as the service answers it on loopback; null for
    /// every other record and from an older host, which <see cref="HelpModes.Recorded"/> reads as restricted.
    /// </summary>
    public string? HelpMode { get; init; }

    /// <summary>
    /// The access setup a driven record started under, each pause with its value, as the service answers it on
    /// loopback: what a resume or a carry-on compares its own with, to say the change, its permission prompts
    /// (<see cref="DrivenAccess.ChangedSince"/>) and its go-aheads (<see cref="Continuations.GoAheadsChanged"/>).
    /// Null from a record without the setting and from an older host, which <see cref="DrivenAccess.Ran"/> reads as the current setup.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Access { get; init; }

    /// <summary>The harness version its record opened on: a resumed run says so where the version moved since.</summary>
    public string? DoorVersion { get; init; }

    /// <summary>
    /// The commit its tree stood at when it opened: a resumed run's evidence is counted from it, so the review's
    /// range is the whole session's. Served on loopback, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>
    /// A park the person answered, still parked: its own record goes on, and its harness conversation resumes
    /// where it can. An answer an older service takes has already ended the record, and is a carry-on.
    /// </summary>
    public bool AnsweredPark => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase) && SaidWordView.AnyPerson(Said);

    /// <summary>
    /// The person's words waiting on its record for it to go on with, in the order said; null where the
    /// host answers no <c>said</c>, an older one, and empty where nothing waits.
    /// </summary>
    public IReadOnlyList<SaidWordView>? Said { get; init; }

    /// <summary>Whether any of its waiting words is the person's: what an answer to a park, or words to an ended session, are.</summary>
    public bool PersonSaid => SaidWordView.AnyPerson(Said);

    /// <summary>The ask an intake answers, or null for every other record: an intake never goes on.</summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The work the record opened for, as the service answers it on loopback (<see cref="ParticipantWork"/>); null
    /// for a teammate's mirror, which has none here, and from an older host, whatever its marks say. Read by nothing yet: the
    /// readers that tell the work build it from the marks (<see cref="WorkMarks"/>), which every host answers.
    /// </summary>
    public string? Work { get; init; }

    /// <summary>
    /// <c>driven</c> or <c>chat</c>, as the record says: an ended chat the person wrote to goes on through the chat runner
    /// never the planner, which plans a quest's sessions.
    /// </summary>
    public string Kind { get; init; } = "driven";

    /// <summary>
    /// Its note's lines, each by its code with its values: what a line the driver adds to its note carries
    /// on. Null for a record without parts, whose note is then carried whole.
    /// </summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>Its note as composed: its parts, or its English whole as one part.</summary>
    public Noted AsNoted() => Noted.From(Note, NoteParts);

    /// <summary>Waiting on the person: <c>awaiting-person</c>, the one state a record goes on from without leaving an ended one.</summary>
    public bool Parked => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase);

    /// <summary>A record that comes down from the team, keyed <c>origin/id</c>: never this machine's to go on with.</summary>
    public bool Teammate => Session.Contains('/');

    /// <summary>
    /// When its record last moved, as the service answers it (<c>updated</c>): for an ended record, when it ended. What a
    /// take made here is compared with, to say whether it came after. Null where unsaid.
    /// </summary>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>
    /// When its record is made, as the service answers it (<c>created</c>): for a conversation that continues another by the
    /// usage curve (<see cref="FromWhy"/> <c>curve</c>), when it moved, which holds the next move for thirty minutes. Null where unsaid.
    /// </summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>
    /// Whether the person's words wait for it to go on with: its <c>said</c> holds any, parked or ended;
    /// from a host without <c>said</c>, an answered park is the one case that waits.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Never while it runs</b>: a resumed run moves its record to working and takes the words off it only as
    /// it concludes, so for the whole run they are still on it. Read as waiting, a look would take the record up a second time,
    /// whose move to working the ledger refuses (working → working), and the driver would fail the session it is resuming.
    /// </remarks>
    public bool WordsWaiting => !Running && Waiting.Count > 0;

    /// <summary>
    /// Queued, starting or working: a run has the record, so the words on it are on their way to the session, and
    /// nothing waits for a look to take it up. A park is not running: it waits on the person.
    /// </summary>
    public bool Running => State.ToLowerInvariant() is "queued" or "starting" or "working";

    /// <summary>
    /// The words it goes on with, in order: each of <see cref="Said"/>. A word an earlier
    /// shell attempted is settled as this one comes up, before any go-on (<see cref="UncertainWords"/>); one this shell
    /// attempted and kept waiting after a failed turn goes again.
    /// </summary>
    public IReadOnlyList<SaidWordView> Waiting => Said is { } said ? [.. said.Where(word => !WatchKeys.Is(word.Key))] : [];

    /// <summary>
    /// The second opinions whose findings wait among its words, each once, in the order handed: another
    /// agent's claims, which go on only in this session's own conversation and never to a new session as the person's words.
    /// </summary>
    public IReadOnlyList<string> Findings => [.. Waiting.Select(word => word.By).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The helper sessions whose answers wait among its words, each once, in the order handed: another agent's
    /// claims, which go on only in this session's own conversation, as findings do.
    /// </summary>
    public IReadOnlyList<string> Helpers => [.. Waiting.Select(word => word.Helper).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>The session this one answers, when it is a helper's record (<c>serves</c>); null for every other.</summary>
    public string? Serves { get; init; }

    /// <summary>
    /// The piece this one writes, the ask's id, when it is a partner's record: beside its
    /// <see cref="Serves"/>, which names its lead; null for every other, and from an older host.
    /// </summary>
    public string? Piece { get; init; }

    /// <summary>The second opinion this one reads for, when it is a reviewer's record; null for every other.</summary>
    public string? Opinion { get; init; }

    /// <summary>The attempt this one decides for, when it is a supervisor's turn; null for every other.</summary>
    public string? Decides { get; init; }

    /// <summary>The plugin step's attempt this one performs, when it is a step's session; null for every other.</summary>
    public string? Performs { get; init; }

    /// <summary>
    /// The session this one continues (its <c>from</c>): a conversation handed over, or a quest's session carried on
    /// after a hand-over or onto another account after a limit; null for every other, and from an older host.
    /// </summary>
    public string? From { get; init; }

    /// <summary>Why it continues <see cref="From"/>, by <see cref="HandOverWhy"/>'s codes; null with it.</summary>
    public string? FromWhy { get; init; }

    /// <summary>Who asked for it to continue <see cref="From"/>, by <see cref="HandOverBy"/>'s codes; null with it.</summary>
    public string? FromBy { get; init; }

    /// <summary>The workspace the record belongs to, as it names it: where a rule is resolved for it.</summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// The session entry it is a part of, <c>&lt;origin&gt;/&lt;repository&gt;</c>, as the service answers
    /// it: what a later quest of the same line goes into its tree by. Null from an older host.
    /// </summary>
    public string? Entry { get; init; }

    /// <summary>The quest it served, as its record names it; null for a conversation's. What a later part is told it worked on.</summary>
    public string? Quest { get; init; }

    /// <summary>The quest a conversation took through its own connector, its <see cref="Quest"/> staying null.</summary>
    public string? TookQuest { get; init; }
}

/// <summary>
/// One of the person's words waiting on a record, as the service answers it to this machine: its id,
/// which the record's events say again where the session took it, the words, when, its files' names, and whether it is
/// said after the record ended.
/// </summary>
/// <param name="Id">Its id on the record; null for an answer an older host keeps, which has none.</param>
public sealed record SaidWordView(string? Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens)
{
    /// <summary>
    /// The second opinion whose findings these are: another agent's claims in
    /// Daoris's fixed words, never the person's. Null for the person's words.
    /// </summary>
    public string? By { get; init; }

    /// <summary>
    /// The helper session whose answer this is: another agent's claim, never the person's words and
    /// no second opinion's findings. Null for every other word.
    /// </summary>
    public string? Helper { get; init; }

    /// <summary>
    /// Its author where that is not the person: <see cref="DaorisWords.From"/> for
    /// Daoris's own words, a reopen or a step's go-on. Null for every other word, and for an older host's, whose
    /// words read as the person's.
    /// </summary>
    public string? From { get; init; }

    /// <summary>Its producer's key: Daoris's fact's, read by <c>DaorisKeys</c>; null for a word kept with none.</summary>
    public string? Key { get; init; }

    /// <summary>
    /// When its sender asked it to reach a working session; null for a word kept with none, which reads by its sender:
    /// the person's as urgent, any other as ordinary.
    /// </summary>
    public MessageLevel? Level { get; init; }

    /// <summary>
    /// When a dispatcher first wrote it to a harness: a word with one, still waiting when a shell comes up,
    /// may have reached the agent. Null for a word never attempted, and an older host's.
    /// </summary>
    public DateTimeOffset? Attempt { get; init; }

    /// <summary>
    /// The answer of its park's question the person pressed, read from the event its words were
    /// shown under (<see cref="SessionEvent.Choice"/>), since the record keeps words alone. Null for every other word.
    /// </summary>
    public string? Choice { get; init; }

    /// <summary>
    /// What the session is handed of these words: a pressed answer as <c>chose: &lt;label&gt;</c>, then the person's own words after a
    /// blank line, so the agent tells the answer it offered from words it did not; every other word as kept.
    /// </summary>
    public string Handed => PressedWords.Handed(Text, Choice);

    /// <summary>
    /// The supervisor's decision a <see cref="SupervisorHanded.From"/> word claims: a claim on the record, never
    /// an authority. Whether it is the supervisor's is <see cref="QuestionSupervisorDecisions.Authored"/>'s, by its id.
    /// </summary>
    public string? Decision { get; init; }

    /// <summary>Whether these are Daoris's own words: read from its mark, never its sentence.</summary>
    public bool Daoris => From == SaidFrom.Daoris;

    /// <summary>Whether these words claim the supervisor: a claim only, which the look attributes or quarantines.</summary>
    public bool Supervisors => From == SaidFrom.Supervisor;

    /// <summary>Whether these are the person's words: no second opinion's, no helper's or partner's, and not Daoris's or the supervisor's.</summary>
    public bool Persons => Senders.Of(this) == Senders.Person;

    /// <summary>Whether any of <paramref name="said"/> is the person's: what an answered park is, and what an ended session's carry-on is handed.</summary>
    public static bool AnyPerson(IReadOnlyList<SaidWordView>? said) => said is not null && said.Any(word => word.Persons);

    /// <summary>The person's words among <paramref name="said"/> joined by a blank line, or null where none: for a sentence that quotes them.</summary>
    public static string? PersonsJoined(IEnumerable<SaidWordView>? said) =>
        said?.Where(word => word.Persons).Select(word => word.Text).ToList() is { Count: > 0 } persons ? string.Join("\n\n", persons) : null;
}

/// <summary>An ACTIVE session as the service answers it — closed ones never reach the planner.</summary>
/// <param name="State">
/// Where the record stands. The planner does not need it — "is this repository busy" is the only
/// question it asks — but <see cref="AttentionWatch"/> does, because a park is a state change nothing
/// local performs and is therefore only visible by looking.
/// </param>
/// <param name="Note">What the session says about that state, where it says anything.</param>
public sealed record SessionView(
    string Id, string Repository, string State = "", string? Note = null)
{
    /// <summary>
    /// The ask an INTAKE answers, or null for every other session. Its "repository" is
    /// <c>ask #id</c>, which no quest names — so the planner never finds it busy with anything.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The tree it holds, or null when unsaid — where a repository opens a tree per session, the
    /// tree is the lock the planner asks about.
    /// </summary>
    public string? Tree { get; init; }

    /// <summary>
    /// The quest it serves, or null for a conversation's or an intake's. A session outlives the look that
    /// started it, so the next look can find its quest still open, and must not start it again.
    /// </summary>
    public string? Quest { get; init; }

    /// <summary>The adapter its record opened on, or null where unsaid: an ending told at <c>work/event</c> names it where it is one of this build's.</summary>
    public string? Adapter { get; init; }

    /// <summary>
    /// The commit its tree stood at when it opened, or null where unsaid: what a lost session's done is read as changed
    /// from when the sweep ends it. Answered on loopback alone, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>Its note's lines by code, handed on wherever its note is; null for a record without parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>
    /// The words waiting on its record, answered on loopback alone; null from an older host and for a teammate's.
    /// A person's among them make an answered park, which goes on at the driver's next look and so waits on nobody,
    /// which Ask Daoris's room reads as the Sessions list does.
    /// </summary>
    public IReadOnlyList<SaidWordView>? Said { get; init; }

    /// <summary>Whether the person has said a word on it: the answer of a park.</summary>
    public bool Answered => SaidWordView.AnyPerson(Said);

    /// <summary>
    /// The workspace its record is filed under, <c>default</c> where an older host answers none: what Ask Daoris's
    /// start hands the running conversation of its own workspace by.
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// The record's kind as the service answers it, <c>driven</c> or <c>chat</c>; <c>driven</c> where a host answers none,
    /// as every reader of the records takes it. What <see cref="ActiveSessions.HoldsSlot"/> tells the person's conversations from.
    /// </summary>
    public string Kind { get; init; } = SessionKinds.Driven;

    /// <summary>
    /// The second opinion it reads another session's work for, or null for every other session: a reviewer the driver
    /// starts, whose record is a chat that serves no quest. Answered on loopback alone.
    /// </summary>
    public string? Opinion { get; init; }

    /// <summary>
    /// The session it answers, or null for every other session: a helper the driver starts, whose record is a chat that
    /// serves no quest. Answered on loopback alone.
    /// </summary>
    public string? Serves { get; init; }

    /// <summary>
    /// The piece a partner's record writes, the ask's id, or null for every other session: beside
    /// its <see cref="Serves"/>, which names its lead. What tells the common admission a piece's record has opened, so its reservation
    /// is counted once. Answered on loopback alone.
    /// </summary>
    public string? Piece { get; init; }

    /// <summary>The attempt a supervisor's turn decides for, which takes no place under the cap; answered on loopback alone.</summary>
    public string? Decides { get; init; }

    /// <summary>The plugin step's attempt a step's session performs, which takes no place under the cap; answered on loopback alone.</summary>
    public string? Performs { get; init; }

    /// <summary>The uncommitted paths of its own a parked line keeps its git place with, on loopback alone.</summary>
    public IReadOnlyList<string>? HoldsPlace { get; init; }
}

/// <summary>The one spelling of the state that means "only the person can clear this".</summary>
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

    private static readonly string[] Endings = ["completed", "declined", "stood-down", "failed", "stopped"];

    /// <summary>
    /// Whether a record's state is one of the five endings (the page's <c>SESSION_ACTIVE</c> complement): an empty or unknown
    /// state is never read as ended.
    /// </summary>
    public static bool IsEnded(string? state) => Endings.Contains(state, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A session running on this home as the downloads' guard reads it: its id, when
/// its record opened, and the downloaded versions its starts are handed, <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>; null
/// for a record without that field, which keeps the start-time rule.
/// </summary>
public sealed record LiveSession(string Id, DateTimeOffset? Started, IReadOnlyList<string>? Resolved);
