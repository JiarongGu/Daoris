namespace Daoris.Contracts.Platform;

// What a local host tells the processes beside it that tell from it: which ledger families moved, and the event journal's rows.

/// <summary>
/// The change stream's answer (FABRIC-EVENT3b, the fabric events design §A4.2). <c>Since</c> is the cursor to ask from next
/// (<c>&lt;epoch&gt;.&lt;n&gt;</c>), and <c>Moved</c> the ledger families that moved after the one asked from, by name (<c>sessions</c>,
/// <c>quests</c>): empty at the wait's end with nothing moved. Families, never records, and no path (D169 §3). <c>Journal</c>
/// (FOUNDATION1 row k part 2) is the event journal's place, answered to a reader that asked for it with <c>journal=1</c> and written to
/// none else, so an answer to a client from before it is what it always was.
/// </summary>
public sealed record ChangesResponse(string Since, IReadOnlyList<string> Moved, ChangesJournalResponse? Journal = null);

/// <summary>Where the event journal stands: the store's identity its positions are of, the lowest position kept (the floor) and the newest issued.</summary>
public sealed record ChangesJournalResponse(string Store, long Floor, long Newest);

/// <summary>
/// One row of the event journal as a reader is answered it (FOUNDATION1 l-i, the foundation design §4.1): the raw transition and the snapshot
/// its coding needs, the columns the capture schema names, never a text. <c>NoteCodes</c> and <c>Marks</c> are the lists the triggers wrote,
/// comma-joined.
/// </summary>
public sealed record JournalRow(
    long Position, long At, string Record, string? Family, string Change, string? StateOld, string? StateNew, bool? LimitedOld, bool? LimitedNew,
    string? NoteCodes, string? Marks, string? Entry, string? FromSession, string? FromWhy, string? FromBy, string? Workspace, string? Quest,
    string? Word, string? WordAuthor, string? WordLevel, bool? WordKeyed, long? WordsOver, string? Op, string? Ask, string? Message);

/// <summary>A page of the journal after a cursor, with where the journal stands: the store's identity, its floor and its newest position.</summary>
public sealed record JournalPage(string Store, long Floor, long Newest, IReadOnlyList<JournalRow> Rows);
