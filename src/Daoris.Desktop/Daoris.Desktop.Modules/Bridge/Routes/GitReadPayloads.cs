namespace Daoris.Desktop;

// The payloads of GitRoutes' reads, each named for its route. Every one names the checkout it reads by its <c>Repository</c>, and its
// <c>Workspace</c> where the repository sits in more than one.

/// <summary>A graph page from the tips named, a page at a time; <c>SinceParted</c> keeps to the commits since the tips parted.</summary>
public sealed record GitGraphRead(string? Repository, string? Workspace, IReadOnlyList<string>? Tips, long? Page, bool SinceParted = false);

/// <summary>A commit's page by its whole id.</summary>
public sealed record GitCommitRead(string? Repository, string? Workspace, string? Commit);

/// <summary>A file's history from a commit, a page at a time.</summary>
public sealed record GitHistoryRead(string? Repository, string? Workspace, string? Commit, string? Path, long? Page);

/// <summary>A file as a commit holds it.</summary>
public sealed record GitBlameRead(string? Repository, string? Workspace, string? Commit, string? Path);

/// <summary>Two commits compared; <c>Everything</c> lifts the bound on the commits one side holds.</summary>
public sealed record GitCompareRead(string? Repository, string? Workspace, string? Left, string? Right, bool Everything = false);

/// <summary>A checkout's repository alone: the Changes tab, the stash plan and the fetch plan read nothing more.</summary>
public sealed record GitCheckoutRead(string? Repository, string? Workspace);

/// <summary>One file's hunks on a side, <c>staged</c> or not.</summary>
public sealed record GitHunksRead(string? Repository, string? Workspace, string? Path, string? Side);

/// <summary>A file in conflict.</summary>
public sealed record GitConflictRead(string? Repository, string? Workspace, string? Path);

/// <summary>What a commit or an amend would rest on; <c>Verb</c> is <c>amend</c> or anything else for a commit.</summary>
public sealed record GitCommitPlanRead(string? Repository, string? Workspace, string? Verb);

/// <summary>What a switch to <c>Branch</c> would rest on.</summary>
public sealed record GitSwitchPlanRead(string? Repository, string? Workspace, string? Branch);

/// <summary>The repositories whose held git remote is tried again.</summary>
public sealed record GitTryAgainPress(IReadOnlyList<string>? Repositories, string? Workspace);
