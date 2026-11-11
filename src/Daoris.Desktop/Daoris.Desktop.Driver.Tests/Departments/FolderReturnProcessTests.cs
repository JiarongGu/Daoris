using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder's return on a real folder, against what else touches it: a write by someone else between the return's read and its replace
/// is never lost, the file put to the person as changed on both sides; a file another program holds open is left; a return killed at
/// any boundary of a file's write is taken up by the next from its journal, every file ending as the session left it with what it
/// replaced kept beside the journal and nothing of the return's own left in the folder; and a link in either folder is refused, never
/// followed or read as a removal.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class FolderReturnProcessTests : IDisposable
{
    private const string Unit = "run:r1";
    private readonly string _scratch = ConformanceScratch.New("return-proc");
    private string Home => Path.Combine(_scratch, "home");
    private string Place => Path.Combine(_scratch, "reports");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private sealed class Killed : Exception;

    private FolderCopy Copy()
    {
        Directory.CreateDirectory(Path.Combine(Place, "q4"));
        File.WriteAllText(Path.Combine(Place, "notes.md"), "first\n");
        File.WriteAllText(Path.Combine(Place, "old.md"), "old\n");
        File.WriteAllText(Path.Combine(Place, "q4", "figures.csv"), "month,total\n");
        return FolderCopies.Make(Home, "default", "reports", Place, ManifestBounds.Default).Copy!;
    }

    private FolderReturned Return(FolderCopy copy, Action<string>? passed = null) =>
        FolderReturn.Run(Home, copy, new Dictionary<string, string>(), ManifestBounds.Default, ReturnDoor.JournalOf(Home, Unit, copy.Id), passed);

    private string Kept(FolderCopy copy, string path) => Path.Combine(ReturnDoor.JournalOf(Home, Unit, copy.Id), "kept", path);

    [Theory]
    [InlineData(ReturnSteps.Staged)]
    [InlineData(ReturnSteps.SetAside)]
    public void A_write_by_someone_else_in_the_window_is_kept_and_the_file_put_to_the_person(string window)
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");

        var returned = Return(copy, step =>
        {
            if (step == window) File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's late edit\n");
        });

        Assert.Equal("the person's late edit\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
        Assert.Contains("notes.md", returned.Waiting);
        Assert.Empty(Directory.GetFiles(Place, "*.daoris-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_file_another_program_holds_open_is_left_and_waits()
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");
        var target = Path.Combine(Place, "notes.md");
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "-NoProfile", "-Command",
            $"$s = [IO.File]::Open('{target.Replace("'", "''")}', 'Open', 'ReadWrite', 'ReadWrite'); [Console]::Out.WriteLine('open'); [Console]::Out.Flush(); Start-Sleep -Seconds 60" })
        {
            info.ArgumentList.Add(argument);
        }

        using var holder = Process.Start(info)!;
        try
        {
            using var said = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Assert.Equal("open", await holder.StandardOutput.ReadLineAsync(said.Token));

            var returned = Return(copy);

            Assert.Contains("notes.md", returned.Waiting);
            using (var reader = new StreamReader(new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                Assert.Equal("first\n", reader.ReadToEnd());
            }

            Assert.Empty(Directory.GetFiles(Place, "*.daoris-*", SearchOption.AllDirectories));
        }
        finally
        {
            holder.Kill(entireProcessTree: true);
            await holder.WaitForExitAsync();
        }
    }

    public static TheoryData<string, int> Boundaries
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var step in ReturnSteps.All)
            {
                data.Add(step, 1);
                data.Add(step, 2);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void A_return_killed_at_any_boundary_is_taken_up_from_its_journal(string boundary, int nth)
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");
        File.WriteAllText(Path.Combine(copy.Folder, "q4", "new.md"), "new\n");
        File.Delete(Path.Combine(copy.Folder, "old.md"));

        var passes = 0;
        try
        {
            Return(copy, step =>
            {
                if (step == boundary && ++passes == nth) throw new Killed();
            });
        }
        catch (Killed)
        {
            // Stopped for good there: nothing after the boundary ran.
        }

        var resumed = Return(copy);

        Assert.Empty(resumed.Waiting);
        Assert.Equal("the session's\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
        Assert.Equal("new\n", File.ReadAllText(Path.Combine(Place, "q4", "new.md")));
        Assert.False(File.Exists(Path.Combine(Place, "old.md")));
        Assert.Equal("first\n", File.ReadAllText(Kept(copy, "notes.md")));
        Assert.Equal("old\n", File.ReadAllText(Kept(copy, "old.md")));
        Assert.Equal(["notes.md", "q4/figures.csv", "q4/new.md"],
            Directory.GetFiles(Place, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(Place, file).Replace('\\', '/')).Order(StringComparer.Ordinal));
    }

    private FolderReturned Return(FolderCopy copy, IReadOnlyDictionary<string, string> choices) =>
        FolderReturn.Run(Home, copy, choices, ManifestBounds.Default, ReturnDoor.JournalOf(Home, Unit, copy.Id));

    private IEnumerable<string> KeptTexts(FolderCopy copy)
    {
        var journal = ReturnDoor.JournalOf(Home, Unit, copy.Id);
        return new[] { "kept", "earlier" }.Select(entry => Path.Combine(journal, entry)).Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)).Select(File.ReadAllText).ToList();
    }

    [Fact]
    public void A_path_written_over_twice_keeps_the_bytes_of_each_write()
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");
        Return(copy);
        File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's edit after\n");

        Return(copy, new Dictionary<string, string> { ["notes.md"] = ReturnChoices.TakeSession });

        Assert.Equal("the session's\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
        var kept = KeptTexts(copy).ToList();
        Assert.Contains("first\n", kept);
        Assert.Contains("the person's edit after\n", kept);
    }

    [Fact]
    public async Task A_copy_file_that_could_not_be_read_is_never_taken_as_a_removal_of_the_folder_s()
    {
        var copy = Copy();
        var held = Path.Combine(copy.Folder, "notes.md");
        File.WriteAllText(held, "the session's\n");
        await using var lockedOpen = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None);

        var returned = Return(copy, new Dictionary<string, string> { ["notes.md"] = ReturnChoices.TakeSession });

        Assert.Equal("first\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
        Assert.Contains("notes.md", returned.Waiting);
    }

    [Fact]
    public void A_keep_both_that_could_not_write_beside_waits_on_the_path_the_person_chose_for()
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");
        File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's\n");
        Directory.CreateDirectory(Path.Combine(Place, FolderReturn.Beside("notes.md", copy.Id)));

        var returned = Return(copy, new Dictionary<string, string> { ["notes.md"] = ReturnChoices.KeepBoth });

        Assert.Equal(["notes.md"], returned.Waiting);
    }

    [Fact]
    public async Task A_junction_in_the_folder_is_refused_and_nothing_is_written_through_it()
    {
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        var copy = Copy();
        await JunctionAsync(Path.Combine(Place, "shared"), elsewhere);
        Directory.CreateDirectory(Path.Combine(copy.Folder, "shared"));
        File.WriteAllText(Path.Combine(copy.Folder, "shared", "new.md"), "through\n");

        var returned = Return(copy);

        Assert.Equal(ReturnRefusal.LinkInFolder, returned.Refused["shared/new.md"]);
        Assert.Empty(Directory.GetFiles(elsewhere));
    }

    [Fact]
    public async Task A_junction_in_the_copy_is_refused_and_never_read_as_a_removal()
    {
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        var copy = Copy();
        Directory.Delete(Path.Combine(copy.Folder, "q4"), recursive: true);
        await JunctionAsync(Path.Combine(copy.Folder, "q4"), elsewhere);

        var returned = Return(copy);

        Assert.Equal(ReturnRefusal.LinkInCopy, returned.Refused["q4/figures.csv"]);
        Assert.Equal("month,total\n", File.ReadAllText(Path.Combine(Place, "q4", "figures.csv")));
    }

    /// <summary>A link at <paramref name="link"/> to <paramref name="target"/>: a junction on Windows, which needs no privilege.</summary>
    private static async Task JunctionAsync(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"mklink failed: {await output} {await error}");
    }
}
