namespace Daoris.Driver;

/// <summary>What a git start may do to the repository, read from its shape.</summary>
public enum GitClass
{
    /// <summary>Nothing written but objects.</summary>
    Read,

    /// <summary>Refs, the index, a tree or configuration, and never by its shape a branch's removal or a move dropping its commits.</summary>
    Write,

    /// <summary>May remove a branch or move it to a commit that does not hold its tip.</summary>
    Drops,
}

/// <summary>A branch a start may drop, by its short name (or <c>refs/heads/*</c> for every one a pattern reaches), and the commit the start
/// verifies it at: null where it verifies none.</summary>
internal sealed record GitDrop(string Branch, string? Verifies);

/// <summary>A git start as the table of git's commands reads it: its class, whether its command and verb have a row, the branches it drops,
/// the trees it touches (the folder it runs in, a <c>--work-tree</c>, the tree a <c>worktree</c> verb names) and the refs it writes or drops.</summary>
internal sealed record GitCommandRead(GitClass Class, bool Rowed, IReadOnlyList<GitDrop> Drops, IReadOnlyList<string> Trees, IReadOnlyList<string> Refs);
