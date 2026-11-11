using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder department's copy and its return: the copy made under the place's own folder in the home with the manifest read at its
/// making, a folder past the copy bound refused in words naming both, and the copy's changes brought back file by file against that
/// manifest, a file changed on both sides waiting for the person's choice until it is made.
/// </summary>
public sealed class FolderCopyTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("folder-copy");
    private string Place => Path.Combine(_scratch, "reports");
    private string Home => Path.Combine(_scratch, "home");
    private static readonly ManifestBounds Bounds = ManifestBounds.Default;
    private const string Unit = "run:r1";

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private void Write(string root, string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, path))!);
        File.WriteAllText(Path.Combine(root, path), text);
    }

    private FolderCopy Copy()
    {
        Write(Place, "notes.md", "first\n");
        Write(Place, "plan/q4.md", "the plan\n");
        Write(Place, "kept.txt", "kept\n");
        var made = FolderCopies.Make(Home, "default", "reports", Place, Bounds);
        Assert.Null(made.Refused);
        return made.Copy!;
    }

    private static string Read(string root, string path) => File.ReadAllText(Path.Combine(root, path));

    [Fact]
    public void A_copy_is_made_under_the_place_s_folder_in_the_home_with_the_manifest_read_at_its_making()
    {
        var copy = Copy();

        Assert.StartsWith(Path.Combine(DaorisHome.Entry(Home, HomeEntry.Places, "default", "reports"), "copies", "c-"), copy.Folder, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("the plan\n", Read(copy.Folder, "plan/q4.md"));
        Assert.Equal(["kept.txt", "notes.md", "plan/q4.md"], FolderCopies.Base(Home, copy)!.Entries.Select(entry => entry.Path));

        FolderCopies.Owned(Home, copy.Folder, "rec-1", "q1");
        Assert.Equal(copy.Folder, FolderCopies.Of(Home, ["rec-0", "rec-1"])!.Folder);
        Assert.Equal(["q1"], FolderCopies.At(Home, copy.Folder)!.Runs);
        Assert.Equal(copy.Id, FolderCopies.At(Home, copy.Folder)!.Id);
        Assert.Null(FolderCopies.At(Home, Place));
    }

    [Fact]
    public void A_folder_past_the_copy_bound_is_refused_in_words_naming_the_bound_and_its_size()
    {
        Write(Place, "big.bin", new string('x', 3000));

        var made = FolderCopies.Make(Home, "default", "reports", Place, Bounds with { Copy = 1024 });

        Assert.Null(made.Copy);
        Assert.Contains("2.9 KB", made.Refused);
        Assert.Contains("copy bound of 1 KB", made.Refused);
        Assert.False(Directory.Exists(Path.Combine(DaorisHome.Entry(Home, HomeEntry.Places, "default", "reports"), "copies")));
    }

    [Fact]
    public void A_folder_of_more_files_than_the_copy_bound_is_refused_and_its_session_waits_rather_than_work_in_the_folder()
    {
        Write(Place, "a.md", "a");
        Write(Place, "b.md", "b");
        Write(Place, "c.md", "c");

        var made = FolderCopies.Make(Home, "default", "reports", Place, Bounds with { CopyFiles = 2 });

        Assert.Null(made.Copy);
        Assert.Contains("3 files, past the copy bound of 2 files", made.Refused);
        Assert.Contains("its session waits", made.Refused);
    }

    [Fact]
    public void The_return_takes_back_a_file_changed_only_in_the_copy_and_keeps_one_changed_only_in_the_folder()
    {
        var copy = Copy();
        Write(copy.Folder, "notes.md", "the session's\n");
        Write(copy.Folder, "new.md", "added by the session\n");
        File.Delete(Path.Combine(copy.Folder, "kept.txt"));
        Write(Place, "plan/q4.md", "the person's plan\n");

        var returned = FolderReturn.Run(Home, copy, new Dictionary<string, string>(), Bounds, ReturnDoor.JournalOf(Home, Unit, copy.Id));

        Assert.Empty(returned.Waiting);
        Assert.Equal(["kept.txt", "new.md", "notes.md"], returned.Written);
        Assert.Equal("the session's\n", Read(Place, "notes.md"));
        Assert.Equal("added by the session\n", Read(Place, "new.md"));
        Assert.Equal("the person's plan\n", Read(Place, "plan/q4.md"));
        Assert.False(File.Exists(Path.Combine(Place, "kept.txt")));
        // What the return overwrote or removed is kept first under the home, so nothing in the folder is destroyed.
        var kept = Path.Combine(ReturnDoor.JournalOf(Home, Unit, copy.Id), "kept");
        Assert.Equal("first\n", Read(kept, "notes.md"));
        Assert.Equal("kept\n", Read(kept, "kept.txt"));
    }

    [Fact]
    public void A_file_changed_on_both_sides_waits_for_the_person_and_one_changed_alike_is_nothing()
    {
        var copy = Copy();
        Write(copy.Folder, "notes.md", "the session's\n");
        Write(Place, "notes.md", "the person's\n");
        Write(copy.Folder, "plan/q4.md", "agreed\n");
        Write(Place, "plan/q4.md", "agreed\n");

        var returned = FolderReturn.Run(Home, copy, new Dictionary<string, string>(), Bounds, ReturnDoor.JournalOf(Home, Unit, copy.Id));

        Assert.Equal(["notes.md"], returned.Waiting);
        Assert.Empty(returned.Written);
        Assert.Equal("the person's\n", Read(Place, "notes.md"));
    }

    [Theory]
    [InlineData(NodeWordActs.KeepYours, "the person's\n", null)]
    [InlineData(NodeWordActs.TakeSession, "the session's\n", null)]
    [InlineData(NodeWordActs.KeepBoth, "the person's\n", "the session's\n")]
    public void The_person_s_choice_settles_a_file_changed_on_both_sides(string choice, string folder, string? beside)
    {
        var copy = Copy();
        Write(copy.Folder, "notes.md", "the session's\n");
        Write(Place, "notes.md", "the person's\n");
        Assert.Equal(["notes.md"], FolderReturn.Run(Home, copy, new Dictionary<string, string>(), Bounds, ReturnDoor.JournalOf(Home, Unit, copy.Id)).Waiting);

        var chosen = FolderReturn.Run(Home, copy, new Dictionary<string, string> { ["notes.md"] = choice }, Bounds, ReturnDoor.JournalOf(Home, Unit, copy.Id));

        Assert.Empty(chosen.Waiting);
        Assert.Equal(folder, Read(Place, "notes.md"));
        var besideFile = Path.Combine(Place, $"notes ({copy.Id}).md");
        Assert.Equal(beside, File.Exists(besideFile) ? File.ReadAllText(besideFile) : null);
        // Asked again, the return finds nothing more to do.
        Assert.Empty(FolderReturn.Run(Home, copy, new Dictionary<string, string> { ["notes.md"] = choice }, Bounds, ReturnDoor.JournalOf(Home, Unit, copy.Id)).Written);
    }

    [Fact]
    public void A_copy_its_session_never_changed_returns_nothing()
    {
        var copy = Copy();
        Write(Place, "notes.md", "the person's\n");

        var returned = FolderReturn.Run(Home, copy, new Dictionary<string, string>(), Bounds, ReturnDoor.JournalOf(Home, Unit, copy.Id));

        Assert.True(returned.Nothing);
        Assert.Equal("the person's\n", Read(Place, "notes.md"));
    }
}
