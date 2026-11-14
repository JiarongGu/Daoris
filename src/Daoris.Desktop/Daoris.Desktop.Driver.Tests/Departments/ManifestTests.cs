using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A git place's manifest at a turn's start and end: git's status with each listed path's content hash, kept under the home with
/// the text of each new file in the place's content store. The start against the place's last end finds what the person changed
/// meanwhile; the end against the start is the turn's own, and what of it stays uncommitted is what the line keeps the place with.
/// </summary>
public sealed class ManifestTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("manifest");
    private readonly PlaceMemory _memory;

    public ManifestTests() => _memory = new PlaceMemory(Path.Combine(_scratch, "reports"));

    private string Place => _memory.Place;
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose()
    {
        _memory.Dispose();
        ConformanceScratch.Remove(_scratch);
    }

    private void Put(string path, string text) => _memory.Put(path, text);

    private void Delete(string path) => _memory.Delete(path);

    private async Task<string> RepositoryAsync()
    {
        await _memory.MakeAsync(("kept.md", "kept\n"), ("notes.md", "first\n"));
        return Place;
    }

    [Fact]
    public async Task The_start_finds_the_person_s_edit_made_between_turns_and_the_end_names_the_turn_s_own_uncommitted_paths()
    {
        var root = await RepositoryAsync();
        var store = new ManifestStore(Home, "default", "reports");

        var lastEnd = await ManifestReader.OfPlaceAsync(root, store.Content, CancellationToken.None);
        Put("notes.md", "the person's line\n");
        Put("new.txt", "a new file\n");
        var start = await ManifestReader.OfPlaceAsync(root, store.Content, CancellationToken.None);

        var outside = PlaceChangesSince.Between(lastEnd, start, committed: []);
        Assert.Equal(["new.txt"], outside.Added);
        Assert.Equal(["notes.md"], outside.Changed);
        Assert.Empty(outside.Removed);

        Put("draft.md", "the session's\n");
        Delete("kept.md");
        var end = await ManifestReader.OfPlaceAsync(root, store.Content, CancellationToken.None);

        var turn = PlaceChangesSince.Between(start, end, committed: []);
        Assert.Equal(["draft.md", "kept.md"], turn.All);
        Assert.Equal(["draft.md", "kept.md"], PlaceChangesSince.OwnUncommitted(start, end));
        Assert.Equal(["new.txt", "notes.md"], PlaceChangesSince.NotOwn(start, end));

        // Kept under the home by record and moment, and read back as written.
        store.Keep("s-1a2b3c4d", ManifestMoment.End, end);
        Assert.Equal(end.Entries, store.Read("s-1a2b3c4d", ManifestMoment.End)!.Entries);
        Assert.Equal(end.Head, store.Last()!.Head);

        var draft = end.Entries.Single(entry => entry.Path == "draft.md");
        Assert.Equal("the session's\n", File.ReadAllText(store.Content.PathOf(draft.Sha256!)));
        Assert.Null(end.Entries.Single(entry => entry.Path == "kept.md").Sha256);
    }

    [Fact]
    public async Task A_commit_between_turns_names_its_paths_and_a_clean_place_lists_none()
    {
        var root = await RepositoryAsync();
        var store = new ManifestStore(Home, "default", "reports");

        var before = await ManifestReader.OfPlaceAsync(root, store.Content, CancellationToken.None);
        Assert.Empty(before.Entries);
        Assert.Equal("main", before.Branch);

        Put("kept.md", "committed by the person\n");
        await _memory.CommitAsync("two");
        var after = await ManifestReader.OfPlaceAsync(root, store.Content, CancellationToken.None);
        var committed = await ManifestReader.CommittedBetweenAsync(root, before.Head, after.Head, CancellationToken.None);

        Assert.Equal(["kept.md"], PlaceChangesSince.Between(before, after, committed).Changed);
    }

    [Fact]
    public void The_content_store_keeps_text_alone_and_stops_at_its_bound()
    {
        var content = new ContentStore(Path.Combine(Home, "blobs"), bound: 10);

        Assert.True(content.Keep("abc", "short\n"u8.ToArray()));
        Assert.False(content.Keep("bin", [0x41, 0x00, 0x42]));
        Assert.False(content.Keep("big", "more than ten bytes\n"u8.ToArray()));
        Assert.True(content.Full);
        Assert.True(File.Exists(content.PathOf("abc")));
        Assert.False(File.Exists(content.PathOf("bin")));

        content.Sweep(named: new HashSet<string>());
        Assert.False(File.Exists(content.PathOf("abc")));
    }

    [Fact]
    public void The_keeping_holds_a_place_s_newest_twenty_and_every_named_one()
    {
        var store = new ManifestStore(Home, "default", "reports");
        var empty = new RunManifest("git", "c0ffee", "main", DateTimeOffset.UnixEpoch, []);
        for (var i = 0; i < 23; i++)
        {
            store.Keep($"s-{i:D8}", ManifestMoment.Start, empty with { At = DateTimeOffset.UnixEpoch.AddMinutes(i) });
        }

        store.Tidy(named: new HashSet<string>(StringComparer.Ordinal) { "s-00000000" });

        Assert.NotNull(store.Read("s-00000000", ManifestMoment.Start));
        Assert.Null(store.Read("s-00000001", ManifestMoment.Start));
        Assert.Null(store.Read("s-00000002", ManifestMoment.Start));
        Assert.NotNull(store.Read("s-00000003", ManifestMoment.Start));
        Assert.Equal(21, Directory.GetFiles(DaorisHome.Entry(Home, HomeEntry.Places, "default", "reports", "manifests")).Length);
    }

    [Fact]
    public async Task A_listed_file_another_program_holds_open_is_listed_with_a_hash_of_its_own_and_the_read_does_not_throw()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync();
        var store = new ManifestStore(Home, "default", "reports");
        var held = Path.Combine(root, "held.md");
        Put("held.md", "being written\n");
        using var lease = new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var manifest = await ManifestReader.OfPlaceAsync(root, store.Content, CancellationToken.None);

        Assert.NotNull(manifest.Listed("held.md")!.Sha256);
        Assert.Equal(14, manifest.Listed("held.md")!.Size);
    }
}
