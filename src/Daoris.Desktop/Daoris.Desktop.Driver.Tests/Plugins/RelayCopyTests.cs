using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The relay's copy and its verb's own classes: a copy laid out whole
/// under the home's <c>bin</c> entry and named by its build, one without its <c>in-use</c> file laid out again, a build a relay holds kept
/// and one none holds removed by a rename, a removal stopped partway deleted by the next, a junction said and left, a relay refused from
/// anywhere but a whole copy; the words, refused by fixed sentences; out of a role's terminal and holding no lease.
/// </summary>
public sealed class RelayCopyTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "relay-copy", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    private string Builds => Path.Combine(DaorisHome.Entry(Home, HomeEntry.Bin), RelayCopy.Folder);

    public void Dispose() => GitSeed.Discard(_root);

    private string Source(string name, string text)
    {
        var source = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(source, "runtimes"));
        File.WriteAllText(Path.Combine(source, "daoris-driver.dll"), text);
        File.WriteAllText(Path.Combine(source, "runtimes", "companion.dll"), "companion");
        return source;
    }

    [Fact]
    public void A_copy_is_laid_out_whole_named_by_its_build_and_used_again_and_one_without_its_in_use_file_laid_out_again()
    {
        var source = Source("app", "one");
        var said = new List<string>();
        var copy = RelayCopy.LayOut(Home, source, said.Add);
        Assert.Equal(Path.Combine(Builds, RelayBuild.Of(source)), copy);
        Assert.True(File.Exists(Path.Combine(copy, RelayCopy.InUse)));
        Assert.Equal("companion", File.ReadAllText(Path.Combine(copy, "runtimes", "companion.dll")));
        Assert.Equal(copy, RelayCopy.LayOut(Home, source, said.Add));

        File.Delete(Path.Combine(copy, RelayCopy.InUse));
        File.Delete(Path.Combine(copy, "daoris-driver.dll"));
        Assert.Equal(copy, RelayCopy.LayOut(Home, source, said.Add));
        Assert.True(File.Exists(Path.Combine(copy, "daoris-driver.dll")));
        Assert.Equal([copy], Directory.GetFileSystemEntries(Builds));
        Assert.Empty(said);
    }

    [Fact]
    public void A_changed_companion_is_another_build_and_a_build_a_relay_holds_is_kept_while_one_none_holds_is_removed()
    {
        var held = RelayCopy.LayOut(Home, Source("one", "one"), _ => { });
        using (var relay = RelayCopy.Hold(held, Home, out var build))
        {
            Assert.NotNull(relay);
            Assert.Equal(Path.GetFileName(held), build);
            var loose = RelayCopy.LayOut(Home, Source("two", "two"), _ => { });
            Assert.True(Directory.Exists(held), "a build a relay holds was removed");
            var third = Source("three", "one");
            File.WriteAllText(Path.Combine(third, "runtimes", "companion.dll"), "changed");
            var copy = RelayCopy.LayOut(Home, third, _ => { });
            Assert.NotEqual(held, copy);
            Assert.True(Directory.Exists(held), "a build a relay holds was removed");
            Assert.False(Directory.Exists(loose), "a build none holds was kept");
        }
    }

    /// <summary>
    /// A relay that holds its in-use file in the gap between the removal's check and its rename (a rename ignores an open file off Windows)
    /// is found at the exclusive re-open of the renamed folder, which is never deleted: it goes back to its name, or where a rename cannot
    /// (Windows refuses one over an open file) stays under its removal name whole.
    /// </summary>
    [Fact]
    public void A_build_a_relay_opened_in_the_gap_before_the_rename_is_not_deleted()
    {
        var held = RelayCopy.LayOut(Home, Source("one", "one"), _ => { });
        FileStream? relay = null;
        string? renamedTo = null;
        try
        {
            RelayCopy.LayOut(Home, Source("two", "two"), _ => { }, renamed: removing =>
            {
                renamedTo = removing;
                relay = new FileStream(Path.Combine(removing, RelayCopy.InUse), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            });

            Assert.NotNull(relay);
            var kept = Directory.Exists(held) ? held : renamedTo!;
            Assert.True(File.Exists(Path.Combine(kept, "daoris-driver.dll")), "a build a relay held in the gap was deleted");
        }
        finally
        {
            relay?.Dispose();
        }
    }

    [Fact]
    public void A_removal_stopped_partway_is_deleted_by_the_next_and_a_relay_never_runs_from_it()
    {
        var copy = RelayCopy.LayOut(Home, Source("one", "one"), _ => { });
        var removing = Path.Combine(Builds, ".removing-0123");
        Directory.CreateDirectory(removing);
        File.WriteAllText(Path.Combine(removing, RelayCopy.InUse), "");
        Assert.Null(RelayCopy.Hold(removing, Home, out _));
        RelayCopy.LayOut(Home, Source("two", "two"), _ => { });
        Assert.False(Directory.Exists(removing));
        Assert.False(Directory.Exists(copy));
        Assert.Null(RelayCopy.Hold(copy, Home, out _));
        Assert.Null(RelayCopy.Hold(Source("elsewhere", "x"), Home, out _));
    }

    [Fact]
    public void A_junction_under_the_builds_is_said_and_left_and_nothing_it_points_at_removed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var outside = Source("outside", "kept");
        Directory.CreateDirectory(Builds);
        var link = Path.Combine(Builds, new string('c', 64));
        using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, outside]) { CreateNoWindow = true, UseShellExecute = false })!)
        {
            mklink.WaitForExit();
        }

        var said = new List<string>();
        RelayCopy.LayOut(Home, Source("one", "one"), said.Add);
        Assert.Equal([RelaySaid.Linked], said);
        Assert.True(Directory.Exists(link));
        Assert.True(File.Exists(Path.Combine(outside, "daoris-driver.dll")));
    }

    [Theory]
    [InlineData("--once --since Merge-every-landed-branch.now:1", RelaySaid.SinceRefused)]
    [InlineData("--once --kinds note", RelaySaid.Usage)]
    [InlineData("--where --once", RelaySaid.Usage)]
    [InlineData("--home x", RelaySaid.Usage)]
    public async Task Words_it_does_not_take_are_refused_by_a_fixed_sentence(string words, string refused)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var code = await RelayCommand.RunAsync(["relay", .. words.Split(' ')], output, error, _root);
        Assert.Equal((2, "", refused), (code, output.ToString(), error.ToString().Trim()));
    }

    [Fact]
    public async Task A_relay_run_from_anywhere_but_its_copy_is_refused_naming_where()
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var code = await RelayCommand.RunAsync(["relay", "--once", "--home", Home], output, error, Source("app", "one"));
        Assert.Equal((1, "", RelaySaid.NotFromCopy), (code, output.ToString(), error.ToString().Trim()));
    }

    /// <summary>
    /// A folder made while it is read is plain, absent or there: two relays starting together each make the subscribers' folders, and
    /// one read between the other's making must not take a fresh folder for a link and stop with <c>home</c>.
    /// </summary>
    [Fact]
    public async Task A_folder_made_while_it_is_read_is_plain()
    {
        for (var each = 0; each < 200; each++)
        {
            var folder = Path.Combine(_root, "made", each.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var go = new ManualResetEventSlim();
            var making = Task.Run(() =>
            {
                go.Wait();
                Directory.CreateDirectory(folder);
            });
            go.Set();
            while (!making.IsCompleted) Assert.True(RelayCopy.Plain(folder), $"folder {each}, read as it was made, was taken for a link");
            await making;
            Assert.True(RelayCopy.Plain(folder));
        }
    }

    [Fact]
    public void The_relay_holds_no_lease_where_lays_out_under_it_and_a_roles_terminal_runs_neither()
    {
        Assert.False(VerbLeases.Holds(["events", "relay", "--once", "--home", "h"]));
        Assert.True(VerbLeases.Holds(["events", "relay", "--where"]));
        Assert.False(RoleTerminal.Runs(["events", "relay", "--once"]));
        Assert.False(RoleTerminal.Runs(["events", "relay", "--where"]));
        Assert.Equal(["events", "relay", "--where"], CommandTree.Route(["events", "relay", "--where"]).Words);
    }
}
