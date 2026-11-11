using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder that is no git repository is worked in place as a repository is: its manifest lists every file but the excludes with its
/// size, time and hash, keeps each text file's content, and a turn's start against the place's last manifest finds what the person
/// changed meanwhile. A folder's lease is its turn's alone, so an end marks nothing; a folder past the bounds is too large to follow and
/// still worked in place; a place whose manifests were git's is never read as a folder.
/// </summary>
public sealed class FolderPlaceTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("folder-place");
    private string Place => Path.Combine(_scratch, "designs");
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private void Write(string path, string text)
    {
        var full = Path.Combine(Place, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private Task<PlaceTold?> StartAsync(string record) =>
        PlaceTurns.StartAsync(Home, "default", "designs", Place, record, null, CancellationToken.None);

    private Task<IReadOnlyList<string>?> EndAsync(string record) => PlaceTurns.EndAsync(Home, record, CancellationToken.None);

    private ManifestStore Store => new(Home, "default", "designs");

    [Fact]
    public async Task A_folder_s_start_finds_what_the_person_changed_between_turns_and_its_end_marks_nothing()
    {
        Write("notes.md", "first\n");
        Write("plans/old.md", "old\n");

        var first = await StartAsync("s-aaaaaaaa");
        Assert.Equal(PlaceKind.Folder, first!.Kind);
        Assert.Empty(first.Outside);
        Write("draft.md", "the session's\n");
        Assert.Empty((await EndAsync("s-aaaaaaaa"))!);

        Write("notes.md", "the person's line\n");
        Write("plans/new.md", "new\n");
        File.Delete(Path.Combine(Place, "plans", "old.md"));
        var told = await StartAsync("s-bbbbbbbb");

        Assert.Equal(["notes.md", "plans/new.md", "plans/old.md"], told!.Outside);
        Assert.Empty(told.Others);
        Assert.Empty(PlaceTurns.Others(Home, "s-bbbbbbbb"));
        var start = Store.Read("s-bbbbbbbb", ManifestMoment.Start)!;
        Assert.Equal(PlaceKind.Folder, start.Kind);
        Assert.Equal(["draft.md", "notes.md", "plans/new.md"], start.Entries.Select(entry => entry.Path));
        Assert.All(start.Entries, entry => Assert.NotNull(entry.Time));
    }

    [Fact]
    public async Task The_excludes_are_passed_by_and_only_text_is_kept()
    {
        Write("notes.md", "text\n");
        Write("~$notes.docx", "owner");
        Write(".~lock.sheet.ods#", "lock");
        Write("Thumbs.db", "thumbs");
        Write("desktop.ini", "ini");
        Write("scratch.tmp", "tmp");
        File.WriteAllBytes(Path.Combine(Place, "picture.png"), [0x89, 0x50, 0x00, 0x01]);

        await StartAsync("s-aaaaaaaa");

        var start = Store.Read("s-aaaaaaaa", ManifestMoment.Start)!;
        Assert.Equal(["notes.md", "picture.png"], start.Entries.Select(entry => entry.Path));
        Assert.True(File.Exists(Store.Content.PathOf(start.Listed("notes.md")!.Sha256!)));
        Assert.False(File.Exists(Store.Content.PathOf(start.Listed("picture.png")!.Sha256!)));
    }

    [Fact]
    public async Task A_folder_past_the_bounds_is_too_large_to_follow_and_still_worked_in_place()
    {
        Write("a.md", "a\n");
        Write("b.md", "b\n");
        Write("c.md", "c\n");

        var told = FolderTurns.Start(Home, "default", "designs", Place, "s-aaaaaaaa", new ManifestBounds(2, TimeSpan.FromMinutes(5)));

        Assert.True(told!.TooLarge);
        Assert.Empty(told.Outside);
        Assert.True(Store.Read("s-aaaaaaaa", ManifestMoment.Start)!.TooLarge);
        Assert.Contains("too large to follow", PlacePrompt.Told(told));
        Assert.Empty((await EndAsync("s-aaaaaaaa"))!);
    }

    [Fact]
    public async Task A_second_manifest_hashes_only_the_files_whose_size_or_time_moved()
    {
        Write("notes.md", "aaaa\n");
        var old = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(Path.Combine(Place, "notes.md"), old);
        await StartAsync("s-aaaaaaaa");
        await EndAsync("s-aaaaaaaa");

        // Same size and the same time, well before the last manifest: its hash is the last one's, read from no file.
        Write("notes.md", "bbbb\n");
        File.SetLastWriteTimeUtc(Path.Combine(Place, "notes.md"), old);
        Assert.Empty((await StartAsync("s-bbbbbbbb"))!.Outside);

        File.SetLastWriteTimeUtc(Path.Combine(Place, "notes.md"), old.AddMinutes(1));
        Assert.Equal(["notes.md"], (await StartAsync("s-cccccccc"))!.Outside);
    }

    [Fact]
    public async Task A_place_whose_manifests_were_git_s_is_never_read_as_a_folder()
    {
        Write("notes.md", "first\n");
        Store.Keep("s-aaaaaaaa", ManifestMoment.End, new RunManifest(PlaceKind.Git, null, "main", DateTimeOffset.UtcNow, []) { Root = Place });

        Assert.Null(await StartAsync("s-bbbbbbbb"));
        Assert.Null(Store.Read("s-bbbbbbbb", ManifestMoment.Start));
    }

    [Fact]
    public void A_folder_session_is_told_it_works_in_a_folder_with_no_git_and_what_changed()
    {
        var told = PlacePrompt.Told(new PlaceTold(["notes.md"], []) { Kind = PlaceKind.Folder });

        Assert.Contains("not a git repository", told);
        Assert.Contains("the person may edit beside you", told);
        Assert.Contains("`notes.md`", told);
        Assert.DoesNotContain("commit", told);
        Assert.Contains("not a git repository", PlacePrompt.Told(new PlaceTold([], []) { Kind = PlaceKind.Folder }));
    }
}
