using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Sessions;

// The driver's session records. State only: the service never spawns a process; the record is what the platform renders and what
// survives a driver restart, and the process handle stays with the driver that owns it. The list, the two opens and the state move are
// served by every host; the doors for words are a local host's, since a session goes on only where its process and conversation are.

/// <summary>
/// The records of this host, newest first; absent <c>includeClosed</c> is the active ones. <c>Deletable</c> says which the ledger would
/// delete (none at a shared deployment), and <c>Head</c> the part each one's line ends at.
/// </summary>
public sealed record ListSessions(string? Repository = null, bool? IncludeClosed = null, string? Workspace = null) : IRoute<SessionResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/sessions";
}

/// <summary>
/// One record as the list answers it, closed or open, of any workspace: a reader of one session asks for it alone. Not found for an
/// unknown id.
/// </summary>
public sealed record ReadSession(string Id) : IRoute<SessionResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/sessions/{id}";
}

/// <summary>
/// The widest list (every repository, closed included, every workspace) as its change since a mark this door answered: a reader that
/// keeps the records lays the change over them and reads the list whole no more.
/// </summary>
/// <param name="Since">The mark of the answer the reader holds; none, or one this host no longer holds, is answered every record.</param>
public sealed record ListSessionChanges(string? Since = null) : IRoute<SessionChangesResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/sessions/changes";
}

/// <summary>
/// The move of the ledger's state machine. Not found for an unknown record; a conflict for a terminal record, an illegal move, a tree
/// another session holds, or words that changed; a bad request for an unknown state name.
/// </summary>
/// <param name="Interrupted">A move to <c>stopped</c> that is not the person's.</param>
/// <param name="Limit">A move to <c>failed</c> that an account's limit made.</param>
/// <param name="NoteParts">The note's lines by code, beside it; a note sent without them clears the record's.</param>
/// <param name="Expected">The state the record must be in, compared in the ledger's step.</param>
/// <param name="UnbindAsk">An intake's end lets its ask go in the same step, for the next intake.</param>
/// <param name="Expect">The exact supervisor words a move to working admits; none admits none.</param>
/// <param name="Turns">The usage history's turn lines of each session a commit point releases memory for, by session.</param>
/// <param name="HoldsPlace">The uncommitted paths of its own a move out of a running state keeps its git place with; none lets it go.</param>
public sealed record AdvanceSession(
    string Id, string? State, string? Note, string? Evidence, string? Transcript, bool? Interrupted = null, bool? Limit = null,
    JsonElement? NoteParts = null, string? Expected = null, bool? UnbindAsk = null, IReadOnlyList<string>? Expect = null,
    Dictionary<string, MemoryTurnsRequest>? Turns = null, IReadOnlyList<string>? HoldsPlace = null) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/state";
}

/// <summary>The words a keyed open moves onto the record it opens: the record they move from, the ids the course named, and the ids it saw waiting.</summary>
public sealed record OpenMovesRequest(string? From, List<string>? Words, List<string>? Read);

/// <summary>
/// A driven record's open for a quest. The harness version and the profile are observed by the driver before it spawns; the service
/// records what it is told. <c>Opens</c> keys the open (answered with <c>transfer</c>), <c>Moves</c> beside it moves words onto the record.
/// </summary>
/// <param name="Tree">The working tree this spawn will hold; unstated resolves to the registered root.</param>
/// <param name="BaseCommit">Where the tree stood before the process ran.</param>
/// <param name="From">The session the new one continues, with <paramref name="FromWhy"/> and <paramref name="FromBy"/>.</param>
/// <param name="Access">The access setup the driver resolved, each pause with its value; one that is no short map of words is the caller's mistake.</param>
public sealed record OpenSession(
    string? Quest, string? Door, string? DoorVersion, string? Profile, string? Tree,
    string? BaseCommit = null, string? From = null, string? FromWhy = null, string? FromBy = null,
    Dictionary<string, string>? Access = null, string? Actor = null, string? Opens = null, OpenMovesRequest? Moves = null)
    : IRoute<SessionOpenResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions";
}

/// <summary>A chat: a session a person entered rather than the driver planned, same record and lock, no quest required.</summary>
public sealed record OpenChat(
    string? Repository, string? Door, string? DoorVersion, string? Profile, string? Tree,
    string? BaseCommit = null, string? From = null, string? FromWhy = null, string? FromBy = null, string? Actor = null, string? Opens = null,
    OpenMovesRequest? Moves = null) : IRoute<SessionOpenResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/chat";
}

/// <summary>
/// What the person added to a running session, as its driver reports it: kept on the ask its work is for. A session on no ask is
/// answered <c>kept: false</c> with the reason.
/// </summary>
public sealed record AddToSession(string Id, string? Text) : IRoute<AddedResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/added";
}

/// <summary>
/// What the person says to a parked or ended session of this machine's, kept on its record for it to go on with. What never goes on is
/// refused by its word (<see cref="SessionSayRefusalResponse"/>): 404 not found, 400 for no words or an author, level or supervisor
/// text the service does not know, 409 for the rest. A running session's words are kept for its run only where the driver that runs it asks.
/// </summary>
/// <param name="Files">The names of what they gave with it; a path is cut to its name.</param>
/// <param name="Channel">The id of the channel message the words are, which the ask already keeps.</param>
/// <param name="From">The author where it is not the person (<c>daoris</c>).</param>
/// <param name="Key">The producer's key: a word kept once per key on the record.</param>
/// <param name="Level"><c>ordinary</c>, <c>urgent</c> or <c>now</c>.</param>
/// <param name="Running">The driver that runs the session asks for its run.</param>
/// <param name="Decision">A supervisor's release names a decision, question and choice with no text; any other author naming one is refused.</param>
/// <param name="Settled">The caller reads a settled answer, which a keyed say may be given where the key's word left a record of its line.</param>
/// <param name="Ending">The shell's word for a record that winds up or moves, kept on it for its next part.</param>
public sealed record SaySession(
    string Id, string? Text, IReadOnlyList<string>? Files = null, string? Channel = null, string? From = null, string? Key = null,
    string? Level = null, bool? Running = null, string? Decision = null, string? Question = null, string? Choice = null,
    bool? Settled = null, bool? Ending = null) : IRoute<SaidResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/say";
}

/// <summary>A park the driver took up and could not hand put back, keeping every word but the supervisor's it admitted.</summary>
/// <param name="TakeBack">The supervisor's words it admitted.</param>
public sealed record RestoreSessionPark(string Id, IReadOnlyList<string>? TakeBack, string? Note, JsonElement? NoteParts = null)
    : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/restore";
}

/// <summary>
/// The words a session took off its record, by their ids: the resumed run's first prompt went, or the session a fallback handed them
/// to took them. Each of the person's said after the record ended is kept on the ask as <c>reopened</c>, to the session that took it.
/// </summary>
/// <param name="By">The session that took them where it is not this one.</param>
public sealed record TakeSaid(string Id, IReadOnlyList<string?>? Said, string? By = null) : IRoute<SessionActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/taken";
}

/// <summary>The words a session's dispatcher is about to write to its harness, marked attempted before the wire, so a shell that comes up again never sends one twice.</summary>
public sealed record AttemptSaid(string Id, IReadOnlyList<string?>? Said) : IRoute<AttemptResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/attempt";
}

/// <summary>
/// The person taking back one word waiting on a record before a session took it; with <c>Text</c>, rewording it: the old word out and
/// these words kept last under a new id, its files' names kept. Nothing is kept on the ask, since nothing took the words.
/// </summary>
public sealed record TakeBackSaid(string Id, string? Word, string? Text = null) : IRoute<TakeBackResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/take-back";
}

/// <summary>
/// The person moving one word held on a part to its line's head: taken off and kept there in one step under the key
/// <c>move:&lt;part&gt;:&lt;word&gt;</c>, so a retry after a lost answer is answered moved and nothing is said twice.
/// </summary>
/// <param name="To">The head the page showed; absent, the part's own line.</param>
public sealed record MoveSaid(string Id, string? Word, string? To = null) : IRoute<MoveSaidResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/move";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpenIntake))]
[JsonSerializable(typeof(OpenHelp))]
[JsonSerializable(typeof(ReleasePlace))]
[JsonSerializable(typeof(KeepHelpMode))]
[JsonSerializable(typeof(KeepAccess))]
[JsonSerializable(typeof(KeepActor))]
[JsonSerializable(typeof(KeepResolved))]
[JsonSerializable(typeof(PostWorkRecord))]
[JsonSerializable(typeof(AgeWorkRecords))]
[JsonSerializable(typeof(ReadSessionRecord))]
[JsonSerializable(typeof(ReadSessionMemory))]
[JsonSerializable(typeof(ListMemoryWaits))]
[JsonSerializable(typeof(ReadLineMemory))]
[JsonSerializable(typeof(DecideMemory))]
[JsonSerializable(typeof(KeepSessionMemory))]
[JsonSerializable(typeof(DeleteSession))]
[JsonSerializable(typeof(JudgeSessionDelete))]
[JsonSerializable(typeof(ListSessions))]
[JsonSerializable(typeof(ReadSession))]
[JsonSerializable(typeof(SessionResponse))]
[JsonSerializable(typeof(ListSessionChanges))]
[JsonSerializable(typeof(SessionChangesResponse))]
[JsonSerializable(typeof(AdvanceSession))]
[JsonSerializable(typeof(OpenSession))]
[JsonSerializable(typeof(OpenChat))]
[JsonSerializable(typeof(AddToSession))]
[JsonSerializable(typeof(SaySession))]
[JsonSerializable(typeof(RestoreSessionPark))]
[JsonSerializable(typeof(TakeSaid))]
[JsonSerializable(typeof(AttemptSaid))]
[JsonSerializable(typeof(TakeBackSaid))]
[JsonSerializable(typeof(MoveSaid))]
[JsonSerializable(typeof(SessionResponse[]))]
[JsonSerializable(typeof(SessionActionResponse))]
[JsonSerializable(typeof(SessionOpenResponse))]
[JsonSerializable(typeof(AddedResponse))]
[JsonSerializable(typeof(SaidResponse))]
[JsonSerializable(typeof(SessionSayRefusalResponse))]
[JsonSerializable(typeof(AttemptResponse))]
[JsonSerializable(typeof(TakeBackResponse))]
[JsonSerializable(typeof(MoveSaidResponse))]
[JsonSerializable(typeof(SessionDeletionResponse))]
[JsonSerializable(typeof(WorkRecordResponse))]
[JsonSerializable(typeof(WorkRecordAgeResponse))]
[JsonSerializable(typeof(WorkRecordShownResponse))]
[JsonSerializable(typeof(SessionMemoryResponse))]
[JsonSerializable(typeof(MemoryWaitsResponse))]
[JsonSerializable(typeof(MemoryReadResponse))]
[JsonSerializable(typeof(MemoryDecisionResponse))]
internal sealed partial class SessionJson : JsonSerializerContext;
