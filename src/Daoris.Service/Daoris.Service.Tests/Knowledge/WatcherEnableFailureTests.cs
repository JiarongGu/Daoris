using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A watcher that cannot be enabled (inotify's limit) is released at once, with its handlers, and the watch says it holds none.</summary>
public sealed class WatcherEnableFailureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"daoris-enable-{Guid.NewGuid():N}");

    public WatcherEnableFailureTests() => Directory.CreateDirectory(Path.Combine(_root, ".claude", "knowledge"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_watcher_whose_enabling_throws_is_disposed_and_not_held()
    {
        var made = new List<FileSystemWatcher>();
        using var freshness = new CheckoutFreshness(_root, null, null, watcher =>
        {
            made.Add(watcher);
            throw new IOException("The configured user limit on the number of inotify instances has been reached.");
        });

        Assert.NotEmpty(made);
        Assert.False(freshness.Watching);
        // A disposed watcher refuses to be enabled; one left to the finalizer would not.
        Assert.All(made, watcher => Assert.Throws<ObjectDisposedException>(() => watcher.EnableRaisingEvents = true));
    }
}
