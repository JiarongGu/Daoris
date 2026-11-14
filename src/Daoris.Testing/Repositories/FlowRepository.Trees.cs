using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class FlowRepository
{
    public Task<GitHeldAnswer> RunHeldAsync(GitHeldRun run, GitHeldStart start) => Current.RunHeldAsync(run, start);

    public Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct) => Current.LayoutAsync(folder, ct);
}
