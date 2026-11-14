using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class FlowRepository
{
    public Task<GitMerged> MergeAsync(string folder, GitMergeAsk ask, CancellationToken ct) => Current.MergeAsync(folder, ask, ct);

    public Task<GitDone> MergeAbortAsync(string folder, CancellationToken ct) => Current.MergeAbortAsync(folder, ct);

    public Task<IReadOnlyList<string>> UnmergedAsync(string folder, CancellationToken ct) => Current.UnmergedAsync(folder, ct);

    public Task<GitDone> CommitStagedAsync(string folder, string message, CancellationToken ct) => Current.CommitStagedAsync(folder, message, ct);

    public Task<GitDone> PutBackAsync(string folder, string commit, CancellationToken ct) => Current.PutBackAsync(folder, commit, ct);
}
