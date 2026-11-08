namespace Daoris.Contracts.Sessions;

// What the service keeps beside a session: a finished tree's record and the memory the session changed. Machine-local, as the rows are.

/// <summary>A file a finished tree's commit changed, with the lines added and removed; -1 for each where git counts none (a binary).</summary>
public sealed record WorkRecordFileWire(string Path, int Added, int Removed);

/// <summary>One commit of a finished tree's, read by <c>--first-parent</c> from its tip: its id, subject and files.</summary>
public sealed record WorkRecordCommitWire(string Id, string Subject, IReadOnlyList<WorkRecordFileWire> Files);

/// <summary>Which hold kept a finished tree's commits as its branch went: <c>line</c>, <c>landing</c>, <c>squash</c>, <c>content</c>, <c>carried</c> or <c>empty</c>.</summary>
public sealed record WorkRecordHoldWire(string Kind, string? Where, string? Form);

/// <summary>The landing that carried a finished tree's work: the branch it made, the line, when it landed, and its pull request where one is opened.</summary>
public sealed record WorkRecordLandingWire(string Branch, string? Line, DateTimeOffset At, string? PullRequest);

/// <summary>What a finished tree held that no commit holds, by name: at most the walk's limit of names, the rest counted.</summary>
public sealed record WorkRecordLeftWire(IReadOnlyList<string> Names, int Count, bool Cut);

/// <summary>A session the record names, and the commits its base attributes to it, or null where none can be read.</summary>
public sealed record WorkRecordSessionResponse(string Session, IReadOnlyList<string>? Commits);

/// <summary>The manifest's facts and the commits read from the tip, as the record keeps them.</summary>
public sealed record WorkRecordPostResponse(
    string Workspace, string Repository, string Folder, string Tree, DateTimeOffset Moved, string? Unit, string Branch, string? Line,
    WorkRecordHoldWire Hold, string Tip, string? GrewFrom, string? Ref, IReadOnlyList<WorkRecordCommitWire>? Commits, string? CommitsUnread,
    WorkRecordLeftWire Left, bool Whole, WorkRecordLandingWire? Landing = null, string? LandingWhy = null);

/// <summary>A finished tree's record, as kept: what the driver posted, when it is kept, and its sessions.</summary>
public sealed record WorkRecordShownResponse(WorkRecordPostResponse Post, DateTimeOffset At, int Schema, IReadOnlyList<WorkRecordSessionResponse> Sessions);

/// <summary>The record as kept: whether this post kept it (false for a second post of a kept identity, which wrote nothing), when, and its sessions.</summary>
public sealed record WorkRecordResponse(bool Kept, DateTimeOffset At, IReadOnlyList<WorkRecordSessionResponse> Sessions);

/// <summary>The records it cleared.</summary>
public sealed record WorkRecordAgeResponse(int Cleared);

/// <summary>One turn line of the usage history: its sequence, its start where the line says, its end and its model, or the split's models joined.</summary>
public sealed record MemoryTurnLine(long Seq, DateTimeOffset? Started, DateTimeOffset? Ended, string? Model);

/// <summary>One session's turn lines as the driver's usage history keeps them, and the instants the person asked.</summary>
public sealed record MemoryTurnsRequest(List<MemoryTurnLine>? Turns, List<DateTimeOffset>? Asked);

/// <summary>An entry a replacement ends, at the revision read.</summary>
public sealed record MemoryPredecessorResponse(string Entry, long Revision);

/// <summary>What an operation asks: a write's text, or an end's reason; the entry it targets and the revision its session read; a relation's source and kind.</summary>
public sealed record MemoryChangeResponse(
    string Kind, string? Text = null, string? Target = null, long? Revision = null, string? Source = null, string? Relation = null,
    string? Recheck = null, IReadOnlyList<MemoryPredecessorResponse>? Predecessors = null);

/// <summary>Who made an operation, from the caller's record at the call, and the call's instant.</summary>
public sealed record MemoryAuthorResponse(
    string Session, string Workspace, string? Quest, string Door, string? DoorVersion, string? Role, string? RoleRevision, DateTimeOffset At);

/// <summary>
/// One row of a repository's memory log. <c>By</c> is absent on a row the service's sweep wrote; <c>Models</c> names the models of the turn
/// that made it; <c>Approver</c> decided an awaiting end; <c>Carried</c> is the quest a proposal travels on.
/// </summary>
public sealed record MemoryOpResponse(
    string Id, string Repository, string State, MemoryChangeResponse Change, MemoryAuthorResponse? By, DateTimeOffset At,
    string? Models = null, string? Approver = null, DateTimeOffset? Holds = null, string? Entry = null, long? Applied = null,
    DateTimeOffset? Settled = null, string? Carried = null, string? Answer = null, DateTimeOffset? Told = null, DateTimeOffset? Began = null);

/// <summary>
/// What waits on the person in memory, since the oldest: for a session, ends and replacements awaiting and the staged rows of a stopped one;
/// for an end the sweep wrote, which has no session, <c>End</c> alone.
/// </summary>
public sealed record MemoryWaitResponse(string? Session, int Ends, int Replacements, int Staged, DateTimeOffset Since, MemoryEndWaitResponse? End = null);

/// <summary>A sweep's end awaiting the person: the row to decide, the entry it ends at the revision it read, and why.</summary>
public sealed record MemoryEndWaitResponse(string Op, string Repository, string Target, long? Revision, string? Reason);

/// <summary>A session's memory changes as its record's page shows them.</summary>
public sealed record SessionMemoryResponse(IReadOnlyList<MemoryOpResponse> Ops);

/// <summary>Every session with a memory change waiting on the person.</summary>
public sealed record MemoryWaitsResponse(IReadOnlyList<MemoryWaitResponse> Waits);

/// <summary>What a line read, each entry as it stands now, for the brief a part continuing it is handed.</summary>
public sealed record MemoryReadResponse(IReadOnlyList<MemoryShownResponse> Shown);

public sealed record MemoryShownResponse(string Reference, long Read, string Standing, long? Revision, long? Checked, string? Why, string Line);

/// <summary>The person's decision through an awaiting end's row, or their keep or drop of an ended session's staged changes, as it stands.</summary>
public sealed record MemoryDecisionResponse(string Message, MemoryOpResponse? Op, int Moved);
