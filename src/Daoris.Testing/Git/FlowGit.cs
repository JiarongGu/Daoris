using Daoris.Ports.Git;

namespace Daoris.Testing;

/// <summary>
/// The git a test process's root hands as its port, which each test's flow may choose for itself: a test that scripts git, or watches
/// what is asked of it, <see cref="Use"/>s its own git for the flow it awaits, and every other flow reaches the git the root chose. The
/// choice is the test kit's, so the application reads its port and nothing more.
/// </summary>
public sealed class FlowGit(IGit otherwise) : IGit
{
    private static readonly AsyncLocal<IGit?> s_chosen = new();

    /// <summary>The git this flow reaches now: its own choice, or the root's.</summary>
    public IGit Current => s_chosen.Value ?? otherwise;

    /// <summary>The git the root chose, reached by every flow that chose none.</summary>
    public IGit Otherwise => otherwise;

    /// <summary>The git this flow chose, or null where it chose none.</summary>
    public static IGit? Chosen => s_chosen.Value;

    /// <summary>Makes <paramref name="git"/> the git of this flow and what it awaits, until disposed, when the one before is restored.</summary>
    public static IDisposable Use(IGit git)
    {
        ArgumentNullException.ThrowIfNull(git);
        var before = s_chosen.Value;
        s_chosen.Value = git;
        return new Restore(before);
    }

    public Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct) => Current.RunAsync(ask, ct);

    public Task<GitBytes> ReadBytesAsync(GitAsk ask, int limit, CancellationToken ct) => Current.ReadBytesAsync(ask, limit, ct);

    public Task<int?> ReadAsync(GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct) => Current.ReadAsync(ask, take, ct);

    public string? InUse(string? home) => Current.InUse(home);

    private sealed class Restore(IGit? before) : IDisposable
    {
        public void Dispose() => s_chosen.Value = before;
    }
}
