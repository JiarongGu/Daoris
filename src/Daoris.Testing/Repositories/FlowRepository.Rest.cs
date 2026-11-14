using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class FlowRepository
{
    public Task<IReadOnlyList<GitStatusEntry>> StatusEntriesAsync(string folder, CancellationToken ct) => Current.StatusEntriesAsync(folder, ct);

    public Task<bool> AvailableAsync(CancellationToken ct) => Current.AvailableAsync(ct);

    public Task<GitDone> InitAsync(string folder, string line, CancellationToken ct) => Current.InitAsync(folder, line, ct);

    public Task<GitDone> CommitEmptyAsync(string folder, string name, string email, string message, CancellationToken ct) =>
        Current.CommitEmptyAsync(folder, name, email, message, ct);
}
