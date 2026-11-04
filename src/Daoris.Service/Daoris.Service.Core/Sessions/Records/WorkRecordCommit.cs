namespace Daoris.Knowledge;

/// <summary>One commit of a finished tree's, read by <c>--first-parent</c> from its tip (the keep-size design §2.1): its id, subject and files.</summary>
public sealed record WorkRecordCommit(string Id, string Subject, IReadOnlyList<WorkRecordFile> Files);
