using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class FlowRepository
{
    public Task<GitHeldAnswer> RunHeldAsync(GitHeldRun run, GitHeldStart start) => Current.RunHeldAsync(run, start);

    public Task<GitTreeStatus?> TreeStatusAsync(string folder, CancellationToken ct) => Current.TreeStatusAsync(folder, ct);

    public Task<int?> UncommittedAsync(string folder, CancellationToken ct) => Current.UncommittedAsync(folder, ct);

    public Task<GitTreePlace> PlaceAsync(string folder, CancellationToken ct) => Current.PlaceAsync(folder, ct);

    public Task<GitDone> RemoveTreeAsync(string folder, string path, bool force, CancellationToken ct) => Current.RemoveTreeAsync(folder, path, force, ct);

    public Task<IReadOnlyList<string>?> UnlandedAsync(string folder, string revision, string? except, CancellationToken ct) => Current.UnlandedAsync(folder, revision, except, ct);

    public Task<GitFreshTop> FreshTopAsync(string folder, CancellationToken ct) => Current.FreshTopAsync(folder, ct);

    public Task<IReadOnlyList<GitRefFact>?> RefFactsAsync(string folder, CancellationToken ct) => Current.RefFactsAsync(folder, ct);

    public Task<IReadOnlyList<GitRef>?> HoldingRefsAsync(string folder, string commit, CancellationToken ct) => Current.HoldingRefsAsync(folder, commit, ct);

    public Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct) => Current.LayoutAsync(folder, ct);
}
