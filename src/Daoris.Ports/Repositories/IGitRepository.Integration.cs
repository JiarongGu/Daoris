namespace Daoris.Ports.Repositories;

/// <summary>How a merge goes in.</summary>
public enum GitMergeKind
{
    /// <summary>Only a fast-forward, the files moved with the branch; a diverged history is refused.</summary>
    FastForwardOnly,

    /// <summary>A merge commit, always, with the message given.</summary>
    Commit,

    /// <summary>A merge commit's content left staged and uncommitted, for the caller to settle and <see cref="IGitRepository.CommitStagedAsync"/>.</summary>
    Stage,
}

/// <summary>A merge of one commit into the checkout's HEAD.</summary>
/// <param name="Commit">The commit merged in, which is the merge commit's second parent.</param>
/// <param name="Kind">How it goes in.</param>
/// <param name="Message">The merge commit's message for <see cref="GitMergeKind.Commit"/>.</param>
public sealed record GitMergeAsk(string Commit, GitMergeKind Kind, string? Message = null);

/// <summary>What a merge came to.</summary>
/// <param name="Ok">Whether it went in: committed, or staged where the ask is a staged merge.</param>
/// <param name="Error">Git's words for why not, empty where it went.</param>
/// <param name="Unmerged">The paths a refused merge left conflicted, none where it went or git stopped it before it began.</param>
/// <param name="Pending">Whether a merge stands begun in the checkout, to be committed or aborted.</param>
public sealed record GitMerged(bool Ok, string Error, IReadOnlyList<string> Unmerged, bool Pending);

public partial interface IGitRepository
{
    /// <summary>
    /// Merges a commit into the checkout's HEAD. A merge whose two sides changed one file differently is left begun with that path
    /// unmerged, the files holding both sides, and reported in <see cref="GitMerged.Unmerged"/>; work in the way refuses it before it begins.
    /// </summary>
    Task<GitMerged> MergeAsync(string folder, GitMergeAsk ask, CancellationToken ct);

    /// <summary>The commits <paramref name="tip"/> reaches that none of <paramref name="exclude"/> does, by full id, newest first; null where git could not say.</summary>
    Task<IReadOnlyList<string>?> RevListAsync(string folder, string tip, IReadOnlyList<string> exclude, CancellationToken ct);

    /// <summary>Abandons a merge begun, the files and the index as HEAD holds them; done where none is begun.</summary>
    Task<GitDone> MergeAbortAsync(string folder, CancellationToken ct);

    /// <summary>The paths a merge begun left unmerged, in path order; empty where none or where git could not say.</summary>
    Task<IReadOnlyList<string>> UnmergedAsync(string folder, CancellationToken ct);

    /// <summary>Commits what is staged, which completes a merge begun as a merge commit, hooks not run; refused where a path is unmerged.</summary>
    Task<GitDone> CommitStagedAsync(string folder, string message, CancellationToken ct);

    /// <summary>
    /// Puts the checkout back at <paramref name="commit"/>, detached: a merge begun abandoned, tracked changes lost, untracked files
    /// removed (ignored ones stay), the files and the index the commit's.
    /// </summary>
    Task<GitDone> PutBackAsync(string folder, string commit, CancellationToken ct);
}
