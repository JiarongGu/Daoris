using System.Text.Json;
using Daoris.Contracts.Asks;

namespace Daoris.Contracts.Sessions;

// A session's record as every door answers it. What names this machine's tree, account, words or provenance answers a caller on a local
// host's own machine only, as a quest's file path does; a teammate's record and a browser over a remote carry none of it.

/// <summary>What the page reads a session's state, kind and provenance as, where the wire carries a word or JSON text.</summary>
public static class SessionPageTypes
{
    public const string State =
        "'queued' | 'starting' | 'working' | 'awaiting-person' | 'completed' | 'declined' | 'stood-down' | 'failed' | 'stopped'";

    public const string Kind = "'driven' | 'chat'";

    private const string Value =
        "{ source: 'entry' | 'account-default' | 'command' | 'environment'; value?: string | null; carried: boolean }";

    public const string Asked = "{ agent: string; model: " + Value + "; effort: " + Value + " }";

    public const string Override = "{ model?: string; effort?: string }";

    public const string Ran =
        "{ models: string[]; more: number; entries: { at: string; acknowledged?: string | null; effort?: string | null }[]; entriesMore: number }";

    public const string Chosen = "{ rule: string; by: string; condition?: string | null; from?: string | null }";

    public const string NoteParts =
        "({ code: string; values?: Record<string, unknown> | null; text?: string | null; words?: undefined }"
        + " | { words: string; by?: string | null; code?: undefined })[]";
}

/// <summary>
/// One word waiting on a record: its id, which the record's events say again where the session took it; the words; when; its files'
/// names, never where they are; and whether it is said after the record ended. The marks say whose it is: <c>By</c> the second opinion
/// whose findings these are, <c>Helper</c> a helper's answer, <c>From</c> an author that is not the person (<c>daoris</c>) and <c>Key</c>
/// its producer's; absent, the person's. <c>Level</c> and <c>Attempt</c> are when its sender asked it to reach a working session and
/// when a dispatcher first wrote it to a harness. <c>Decision</c>, <c>Question</c> and <c>Choice</c> are what a supervisor's word claims.
/// </summary>
public sealed record SaidWordResponse(
    string Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens, string? By = null, string? Helper = null,
    string? From = null, string? Key = null, string? Level = null, DateTimeOffset? Attempt = null,
    string? Decision = null, string? Question = null, string? Choice = null);

/// <summary>
/// A session's record. <c>Quest</c> is null for a chat, and <c>Kind</c> says which way in it is: a conversation is a session, not a
/// second kind of thing. The profile, the tree and the base commit are machine-local like the transcript beside them. <c>Entry</c>
/// is the line of work it belongs to, <c>&lt;origin&gt;/&lt;repository&gt;</c>, which names no path and no account and so goes to every caller.
/// </summary>
/// <param name="Deletable">Whether the ledger would delete the record, as the list answers it; false at a shared deployment.</param>
/// <param name="Head">The id of the part its line ends at, the list's answer only; absent for a mirror and where the walk found a cycle.</param>
/// <param name="Work">What the record is opened for, read from its marks and never stored.</param>
/// <param name="Limit">An account's limit made its failure; it names no account.</param>
/// <param name="Interrupted">A stop that is not the person's.</param>
/// <param name="Took">Whether it took its own quest through its own connector.</param>
/// <param name="TookQuest">The quest a chat took, its <c>Quest</c> staying null.</param>
/// <param name="Ask">The ask an intake answers.</param>
/// <param name="Serves">The session a helper's or a partner's record answers.</param>
/// <param name="Piece">The piece a partner's record writes.</param>
/// <param name="HelpMode">The access mode an Ask Daoris record's run got.</param>
/// <param name="Resolved">The downloaded versions its starts were handed.</param>
public sealed record SessionResponse(
    string Id, string? Quest, string Repository, string Door, [property: PageType(SessionPageTypes.State)] string State,
    string? Note, string? Evidence, string? Transcript, DateTimeOffset Created, DateTimeOffset Updated,
    string Workspace, [property: PageType(SessionPageTypes.Kind)] string Kind, string? DoorVersion, string? Profile, string? Tree,
    string? BaseCommit = null, string? Ask = null, bool Took = false, bool Interrupted = false, bool Limit = false, bool Deletable = false,
    IReadOnlyList<SaidWordResponse>? Said = null,
    [property: PageType(SessionPageTypes.NoteParts)] JsonElement? NoteParts = null,
    string? Opinion = null, string? Serves = null, string? From = null, string? FromWhy = null, string? FromBy = null,
    string? Entry = null, string? HelpMode = null, IReadOnlyDictionary<string, string>? Access = null,
    IReadOnlyList<string>? Resolved = null, string? Piece = null, string? TookQuest = null,
    string? Actor = null, string? Choice = null,
    [property: PageType(SessionPageTypes.Asked)] JsonElement? Asked = null,
    [property: PageType(SessionPageTypes.Override)] JsonElement? Override = null,
    [property: PageType(SessionPageTypes.Ran)] JsonElement? Ran = null,
    [property: PageType(SessionPageTypes.Chosen)] JsonElement? Chosen = null,
    string? Work = null, string? Head = null, string? Decides = null, IReadOnlyList<string>? HoldsPlace = null, string? Performs = null,
    string? Line = null);

/// <summary>
/// The list as its change since a mark: the mark to ask from next, every record's id in the list's order, and each record whose answer
/// moved since the mark asked from, as the list answers it.
/// </summary>
/// <param name="Whole">The mark asked from is none this host holds, so every record is among the changed.</param>
public sealed record SessionChangesResponse(string Mark, IReadOnlyList<string> Order, IReadOnlyList<SessionResponse> Changed, bool Whole);

/// <summary>The session as it stands, and the ledger's sentence.</summary>
public sealed record SessionActionResponse(SessionResponse Session, string Message);

/// <summary>
/// An open's answer. A keyed open (<c>Opens</c> named) echoes its key and lists, in order, each word it moved: its id on the record left
/// (<c>Word</c>) and on the record opened (<c>Id</c>, which a moved word keeps), its producer <c>Key</c> and its <c>Transfer</c> id. A
/// refusal of a keyed open carries <c>Error</c> and a <c>Code</c> (<c>malformed</c>, <c>gone</c> or the refusal's name) beside it.
/// </summary>
public sealed record SessionOpenResponse(
    SessionResponse? Session, string Message, string? Opens = null, IReadOnlyList<OpenTransferResponse>? Transfer = null,
    string? Head = null, string? Error = null, string? Code = null);

/// <summary>One word a keyed open moved.</summary>
public sealed record OpenTransferResponse(string Word, string? Key, string Transfer, int Order, string Id);

/// <summary>
/// What the person added to a running session, as its driver reports it. <c>Kept</c> false is no error: the session is on no ask, and the
/// message says so. <c>Ask</c> is the ask it is kept on.
/// </summary>
public sealed record AddedResponse(bool Kept, string Message, AskResponse? Ask);

/// <summary>
/// A say's answer: the session as it stands, the ledger's sentence, and the word as kept (to this machine only). A keyed say the line
/// already settled answers <c>Settled</c>, the record its word left, with no session and no word, and only to a say that asked for it.
/// </summary>
public sealed record SaidResponse(SessionResponse? Session, string Message, SaidWordResponse? Said, string? Settled = null);

/// <summary>
/// A say, a take or a move refused: the sentence as <c>Error</c>, beside its word (<c>Refusal</c>), which a reader acts on instead of the
/// sentence, and the quest, ask, machine or second opinion it names. <c>Head</c> is the line's head where a record's line went on past it.
/// </summary>
public sealed record SessionSayRefusalResponse(
    string Error, string Refusal, string? Quest = null, string? Ask = null, string? Origin = null, string? Opinion = null, string? Head = null);

/// <summary>The session as it stands, the ledger's sentence, and the ids named that no longer wait, which were marked nothing.</summary>
public sealed record AttemptResponse(SessionResponse Session, string Message, IReadOnlyList<string> Unmarked);

/// <summary>
/// A take-back's answer: <c>Done</c>, the ledger's sentence, and, when done, the session as it stands and a reword's new word. Not done,
/// <c>Why</c> is <c>taken</c>: the word no longer waits, or the record runs again and may have read it; an answer, never an error.
/// </summary>
public sealed record TakeBackResponse(
    bool Done, string Message, string? Why = null, SessionResponse? Session = null, SaidWordResponse? Said = null);

/// <summary>
/// A move's answer: <c>Moved</c>, the ledger's sentence, and <c>To</c>, the head it is kept on, with the session and the word kept
/// there; a retry is <c>Again</c>. Not moved, <c>Why</c> is <c>taken</c>, an answer as the take-back's is.
/// </summary>
public sealed record MoveSaidResponse(
    bool Moved, string Message, string? Why = null, string? To = null, bool? Again = null, SessionResponse? Session = null,
    SaidWordResponse? Said = null);

/// <summary>
/// A session delete refused, or its judgement alone. <c>Error</c> is the ledger's sentence; <c>Refusal</c> its word
/// (<c>not-found</c>, <c>not-ours</c>, <c>live</c>, <c>served-quest</c>, <c>named</c>, <c>on-remote</c>), which a reader acts on, and
/// the facts the word names.
/// </summary>
public sealed record SessionDeletionResponse(
    bool Deletable, string? Error = null, string? Refusal = null,
    string? Quest = null, string? Ask = null, string? Origin = null, string? Workspace = null);
