namespace Daoris.Knowledge;

/// <summary>
/// How much one repository contributes to the index, and what it is fed from. <paramref name="IndexedAt"/> and
/// <paramref name="IndexedCommit"/> say when this process read the repository's checkout and the commit it stood at;
/// null where it read none, as a fed repository's or one not read yet.
/// </summary>
public sealed record RepositorySummary(
    string Repository, int Total, int Local, int Canonical, string Workspace = Workspaces.Default,
    FeedProvenance? Fed = null, DateTimeOffset? IndexedAt = null, string? IndexedCommit = null);
