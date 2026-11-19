using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The dependency store's daily eviction as the Keeping pass runs it: the copies not placed within the rule's days go, with their bytes said, and a placed one stays.</summary>
public sealed class DepsKeepingTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "deps-keeping", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Tree => Path.Combine(_scratch, "tree");

    public void Dispose() => GitSeed.Discard(_scratch);

    [Fact]
    public void The_day_removes_exactly_the_copies_unplaced_for_the_days_and_says_what_they_held()
    {
        Directory.CreateDirectory(Path.Combine(Tree, "deps"));
        File.WriteAllText(Path.Combine(Tree, "deps", "a.bin"), new string('a', 1000));
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _, hashed: true);
        foreach (var key in new[] { "stale", "stale-too", "used" }) Assert.True(DependencyStore.Seal(Home, key, Tree, "deps", manifest, CancellationToken.None));
        var now = DateTimeOffset.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(DependencyStore.EntryOf(Home, "stale"), "placed"), now.AddDays(-45).UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(DependencyStore.EntryOf(Home, "stale-too"), "placed"), now.AddDays(-31).UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(DependencyStore.EntryOf(Home, "used"), "placed"), now.AddDays(-1).UtcDateTime);
        var keeping = new DepsKeeping(() => now);
        Assert.True(keeping.Due(Home));

        var (removed, bytes) = keeping.Keep(Home, KeepPolicy.Default);

        Assert.Equal(2, removed);
        Assert.Equal(2 * manifest.Bytes, bytes);
        Assert.True(bytes > 0);
        Assert.Equal(["used"], DependencyStore.Entries(Home).Select(each => each.Key));
        Assert.False(keeping.Due(Home));
    }
}
