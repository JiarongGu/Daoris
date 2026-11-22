using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The resolver's tree is taken as settled only on what git answered: a read of it that failed (no exit) is unknown, never "no file
/// is still conflicted", "nothing else changed" or "the commit differs in nothing". Git is scripted, so no process starts.
/// </summary>
public sealed class ResolveFailedReadTests
{
    private const string Tree = "resolver-tree-that-does-not-exist";

    private static readonly ResolveStopAt Stop = new("c1", "a subject", "patch", "h1", []);

    private static IDisposable Failing() => GitFlow.Failing(_ => true);

    [Fact]
    public async Task A_failed_read_of_the_conflicted_files_says_so_and_never_settles_them()
    {
        using var failing = Failing();

        var marked = await ResolveTree.MarkedAsync(Tree, Stop, CancellationToken.None);

        Assert.NotNull(marked);
        Assert.Contains("did not answer", marked);
    }

    [Fact]
    public async Task A_failed_read_of_the_trees_other_changes_is_unknown_never_none()
    {
        using var failing = Failing();

        Assert.Null(await ResolveTree.OthersAsync(Tree, Stop, CancellationToken.None));
    }

    [Fact]
    public async Task A_failed_read_of_the_stops_commit_is_unknown_never_proved()
    {
        using var failing = Failing();

        Assert.Null(await ResolveTree.ProvedAsync(Tree, Stop, CancellationToken.None));
    }
}
