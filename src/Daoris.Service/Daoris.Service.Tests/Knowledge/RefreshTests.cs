using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A refresh must retire what the source no longer has. Found on the platform's own Overview: a
/// repository renamed weeks earlier was still being served with dozens of entries, because refresh
/// replaced every repository it SAW and said nothing about the ones it did not — and absent
/// repositories are exactly where the ghosts live. On the one surface whose job is telling a person
/// what exists, a ghost is indistinguishable from a live project.
/// </summary>
public sealed class RefreshTests
{
    private sealed class MutableSource : IKnowledgeSource
    {
        public string Name => "test";
        public List<KnowledgeEntry> Entries { get; } = [];
        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>([.. Entries]);
    }

    private static KnowledgeEntry Entry(string repository, string title) => new(
        repository, EntryKind.Rule, Provenance.Local, title, "body", $".claude/rules/{title}.md", null);

    [Fact]
    public async Task A_repository_that_left_the_source_leaves_the_index()
    {
        var store = new InMemoryKnowledgeStore();
        var index = new KnowledgeIndex(store);
        var source = new MutableSource();
        source.Entries.Add(Entry("Sonora", "old-rule"));
        source.Entries.Add(Entry("Yaorin", "new-rule"));
        await index.RefreshAsync(source);

        source.Entries.RemoveAll(e => e.Repository == "Sonora"); // renamed away on disk
        await index.RefreshAsync(source);

        var repositories = (await store.AllAsync()).Select(e => e.Repository).Distinct().ToList();
        Assert.Equal(["Yaorin"], repositories);
    }

    /// <summary>
    /// The one shape retirement must NOT cover: a scan that found nothing at all. That is a mis-set
    /// root far more often than a family that emptied, and "refresh wiped the index" is the wrong
    /// answer to a wrong path — the ghost rule only applies when the scan proved it can see.
    /// </summary>
    [Fact]
    public async Task A_scan_that_saw_nothing_retires_nothing()
    {
        var store = new InMemoryKnowledgeStore();
        var index = new KnowledgeIndex(store);
        var source = new MutableSource();
        source.Entries.Add(Entry("Yaorin", "new-rule"));
        await index.RefreshAsync(source);

        source.Entries.Clear(); // the root vanished, or points somewhere empty
        await index.RefreshAsync(source);

        Assert.Single(await store.AllAsync());
    }

    /// <summary>
    /// The ghost rule's mirror image: a repository that appeared AFTER the host started must be
    /// readable by the next refresh, without a restart.
    /// </summary>
    /// <remarks>
    /// The folder list was enumerated once, when the source was constructed — so `knowledge_refresh`,
    /// whose whole promise is "re-read every repository from disk", re-read only the repositories that
    /// existed when the process launched. A project born today was invisible until someone restarted
    /// the host, and nothing said so: the refresh reported success and a count that looked right.
    /// Found by the workspace rehearsal (D48), where two repositories are born mid-run.
    /// </remarks>
    [Fact]
    public async Task A_repository_that_appeared_after_startup_is_read_by_the_next_refresh()
    {
        var root = Path.Combine(Path.GetTempPath(), $"daoris-born-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "elder", ".claude", "knowledge"));
        File.WriteAllText(
            Path.Combine(root, "elder", ".claude", "knowledge", "old.md"), "# old\n\nWas here first.\n");

        var source = FileSystemKnowledgeSource.UnderFolder(root);
        var store = new InMemoryKnowledgeStore();
        var index = new KnowledgeIndex(store);
        await index.RefreshAsync(source);

        // Born after the host came up — exactly what the rehearsal and every real family do.
        Directory.CreateDirectory(Path.Combine(root, "newborn", ".claude", "knowledge"));
        File.WriteAllText(
            Path.Combine(root, "newborn", ".claude", "knowledge", "new.md"), "# new\n\nBorn later.\n");
        await index.RefreshAsync(source);

        var repositories = (await store.AllAsync())
            .Select(e => e.Repository).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["elder", "newborn"], repositories);

        Directory.Delete(root, recursive: true);
    }
}
