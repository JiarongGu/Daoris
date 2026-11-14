using Daoris.Ports.Repositories;
using Daoris.Ports.Git;

namespace Daoris.Testing;

/// <summary>
/// The repository a test process's root hands as its port, which each test's flow may choose for itself: a test that runs a family on a
/// <see cref="MemoryRepository"/> <see cref="Use"/>s it for the flow it awaits, and every other flow reaches the repository the root chose.
/// </summary>
public sealed class FlowRepository(IGitRepository otherwise) : IGitRepository
{
    private static readonly AsyncLocal<IGitRepository?> s_chosen = new();

    /// <summary>The repository this flow reaches now: its own choice, or the root's.</summary>
    public IGitRepository Current => s_chosen.Value ?? otherwise;

    /// <summary>Makes <paramref name="repository"/> the repository of this flow and what it awaits, until disposed, when the one before is restored.</summary>
    public static IDisposable Use(IGitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var before = s_chosen.Value;
        s_chosen.Value = repository;
        return new Restore(before);
    }

    public Task<string?> HeadAsync(string folder, CancellationToken ct) => Current.HeadAsync(folder, ct);

    public Task<string?> BranchAsync(string folder, CancellationToken ct) => Current.BranchAsync(folder, ct);

    public Task<string?> CommitOfAsync(string folder, string revision, CancellationToken ct) => Current.CommitOfAsync(folder, revision, ct);

    public Task<string?> MergeBaseAsync(string folder, string first, string second, CancellationToken ct) => Current.MergeBaseAsync(folder, first, second, ct);

    public Task<bool?> IsAncestorAsync(string folder, string ancestor, string descendant, CancellationToken ct) => Current.IsAncestorAsync(folder, ancestor, descendant, ct);

    public Task<int?> CountAsync(string folder, string tip, IReadOnlyList<string> exclude, CancellationToken ct) => Current.CountAsync(folder, tip, exclude, ct);

    public Task<IReadOnlyList<GitRef>?> RefsAsync(string folder, IReadOnlyList<string> prefixes, string? contains, int? limit, CancellationToken ct) =>
        Current.RefsAsync(folder, prefixes, contains, limit, ct);

    public Task<IReadOnlyList<GitTreeEntry>?> EntriesAsync(string folder, string commit, IReadOnlyList<string> within, bool recursive, CancellationToken ct) =>
        Current.EntriesAsync(folder, commit, within, recursive, ct);

    public Task<string?> TextAtAsync(string folder, string commit, string path, CancellationToken ct) => Current.TextAtAsync(folder, commit, path, ct);

    public Task<bool> IgnoresCaseAsync(string folder, CancellationToken ct) => Current.IgnoresCaseAsync(folder, ct);

    public Task<IReadOnlyList<string>> IgnoredTopsAsync(string folder, CancellationToken ct) => Current.IgnoredTopsAsync(folder, ct);

    public Task<IReadOnlyList<string>> IgnoredAsync(string folder, IReadOnlyList<string> within, CancellationToken ct) => Current.IgnoredAsync(folder, within, ct);

    public Task<IReadOnlyList<GitWorktree>?> WorktreesAsync(string folder, CancellationToken ct) => Current.WorktreesAsync(folder, ct);

    public Task<IReadOnlyDictionary<string, string>> StatusAsync(string folder, bool ignored, CancellationToken ct) => Current.StatusAsync(folder, ignored, ct);

    public Task<IReadOnlyList<string>?> HiddenAsync(string folder, CancellationToken ct) => Current.HiddenAsync(folder, ct);

    public Task<GitDone> UpdateRefAsync(string folder, string name, string to, string? from, CancellationToken ct) => Current.UpdateRefAsync(folder, name, to, from, ct);

    public Task<GitDone> SwitchAsync(string folder, GitSwitch to, CancellationToken ct) => Current.SwitchAsync(folder, to, ct);

    public Task<GitDone> CommitAllAsync(string folder, string message, CancellationToken ct) => Current.CommitAllAsync(folder, message, ct);

    public Task<GitDone> LockAsync(string folder, string path, string reason, CancellationToken ct) => Current.LockAsync(folder, path, reason, ct);

    public Task<GitDone> UnlockAsync(string folder, string path, CancellationToken ct) => Current.UnlockAsync(folder, path, ct);

    public Task<GitDone> PruneAsync(string folder, CancellationToken ct) => Current.PruneAsync(folder, ct);

    public Task<GitDone> AddWorktreeAsync(string folder, GitWorktreeAdd add, CancellationToken ct) => Current.AddWorktreeAsync(folder, add, ct);

    public Task<string?> TopOfAsync(string folder, CancellationToken ct) => Current.TopOfAsync(folder, ct);

    public Task<GitDone> DeleteBranchAsync(string folder, string branch, CancellationToken ct) => Current.DeleteBranchAsync(folder, branch, ct);

    public Task<IReadOnlyList<string>?> ChangedAsync(string folder, string from, string to, CancellationToken ct) => Current.ChangedAsync(folder, from, to, ct);

    public Task<IReadOnlyList<GitMatch>> GrepAsync(string folder, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct) =>
        Current.GrepAsync(folder, commit, names, pathspecs, ct);

    public Task<GitRefRun> TransactAsync(string folder, GitRefTransaction transaction, CancellationToken ct) => Current.TransactAsync(folder, transaction, ct);

    public Task<GitDone> RestoreAsync(string folder, IReadOnlyList<string> paths, CancellationToken ct) => Current.RestoreAsync(folder, paths, ct);

    public Task<GitDone> RunHookAsync(string folder, string hook, IReadOnlyList<string> arguments, CancellationToken ct) => Current.RunHookAsync(folder, hook, arguments, ct);

    public Task<GitDone> RemoveWorktreeAsync(string folder, string path, CancellationToken ct) => Current.RemoveWorktreeAsync(folder, path, ct);

    public IGitRepository Held(GitRun run) => Current.Held(run);

    private sealed class Restore(IGitRepository? before) : IDisposable
    {
        public void Dispose() => s_chosen.Value = before;
    }
}
