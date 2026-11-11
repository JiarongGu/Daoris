using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder's return refuses rather than guesses: a rename that changes only a name's case, a file the session replaced with a folder
/// and a folder the person replaced with a file are each held for the person with the path and what it found, and nothing of the
/// folder is removed for them; a path that leads outside the folder is never one to write.
/// </summary>
public sealed class FolderReturnRefusalTests : IDisposable
{
    private const string Unit = "run:r1";
    private readonly string _scratch = ConformanceScratch.New("return-refusal");
    private string Home => Path.Combine(_scratch, "home");
    private string Place => Path.Combine(_scratch, "reports");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private FolderCopy Copy(params (string Path, string Text)[] files)
    {
        foreach (var (path, text) in files)
        {
            var file = Path.Combine(Place, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
        }

        return FolderCopies.Make(Home, "default", "reports", Place, ManifestBounds.Default).Copy!;
    }

    private FolderReturned Return(FolderCopy copy, IReadOnlyDictionary<string, string>? choices = null) =>
        FolderReturn.Run(Home, copy, choices ?? new Dictionary<string, string>(), ManifestBounds.Default, ReturnDoor.JournalOf(Home, Unit, copy.Id));

    [Fact]
    public void A_rename_that_changes_only_case_is_refused_and_the_folder_keeps_its_file()
    {
        var copy = Copy(("notes.md", "first\n"));
        File.Move(Path.Combine(copy.Folder, "notes.md"), Path.Combine(copy.Folder, "Notes.md"));

        var returned = Return(copy);

        Assert.Equal("first\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
        Assert.Equal(ReturnRefusal.Collision, returned.Refused["Notes.md"]);
        Assert.Equal(ReturnRefusal.Collision, returned.Refused["notes.md"]);
        Assert.Empty(returned.Written);
    }

    [Fact]
    public void A_file_the_session_replaced_with_a_folder_is_refused_and_the_folder_keeps_its_file()
    {
        var copy = Copy(("plan", "the plan\n"), ("other.md", "o\n"));
        File.Delete(Path.Combine(copy.Folder, "plan"));
        Directory.CreateDirectory(Path.Combine(copy.Folder, "plan"));
        File.WriteAllText(Path.Combine(copy.Folder, "plan", "a.md"), "a\n");

        var returned = Return(copy);

        Assert.Equal("the plan\n", File.ReadAllText(Path.Combine(Place, "plan")));
        Assert.Equal(ReturnRefusal.TypeInCopy, returned.Refused["plan"]);
        Assert.Equal(ReturnRefusal.TypeInCopy, returned.Refused["plan/a.md"]);
    }

    [Fact]
    public void A_folder_the_person_replaced_with_a_file_is_refused_though_they_chose_the_session_s()
    {
        var copy = Copy(("docs/a.md", "a\n"));
        File.WriteAllText(Path.Combine(copy.Folder, "docs", "a.md"), "the session's\n");
        Directory.Delete(Path.Combine(Place, "docs"), recursive: true);
        File.WriteAllText(Path.Combine(Place, "docs"), "now a file\n");

        var returned = Return(copy, new Dictionary<string, string> { ["docs/a.md"] = NodeWordActs.TakeSession });

        Assert.Equal("now a file\n", File.ReadAllText(Path.Combine(Place, "docs")));
        Assert.Equal(ReturnRefusal.TypeInFolder, returned.Refused["docs/a.md"]);
    }

    [Fact]
    public void A_refused_path_is_settled_by_keep_yours_alone()
    {
        var copy = Copy(("notes.md", "first\n"));
        File.Move(Path.Combine(copy.Folder, "notes.md"), Path.Combine(copy.Folder, "Notes.md"));

        var taken = Return(copy, new Dictionary<string, string> { ["Notes.md"] = NodeWordActs.TakeSession, ["notes.md"] = NodeWordActs.TakeSession });
        Assert.Equal(2, taken.Refused.Count);

        var kept = Return(copy, new Dictionary<string, string> { ["Notes.md"] = NodeWordActs.KeepYours, ["notes.md"] = NodeWordActs.KeepYours });
        Assert.Empty(kept.Refused);
        Assert.Empty(kept.Waiting);
        Assert.Equal("first\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("a/../../outside.txt")]
    [InlineData("notes.md:hidden")]
    [InlineData("C:/outside.txt")]
    public void A_path_that_leads_outside_the_folder_is_refused(string path)
    {
        Directory.CreateDirectory(Place);
        Assert.Equal(ReturnRefusal.Escapes, ReturnRefusal.Escaping(Place, path));
        Assert.Null(ReturnRefusal.Escaping(Place, "q4/notes.md"));
    }
}
