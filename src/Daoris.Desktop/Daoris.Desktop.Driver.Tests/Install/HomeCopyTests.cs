using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The copy a home proof is made on (<see cref="HomeCopy"/>) is made from a home in use and is never vacuous: a seat a running service
/// takes is named and never opened, a file another holder keeps is a counted stand-in unless the proof's reading reads it, a folder the
/// reading lists that cannot be listed refuses the copy, content folders are left out by name, a read-only or hard-linked file is copied
/// as its content with no mark, and a copy is removed whatever marks it holds.
/// </summary>
public sealed class HomeCopyTests : IDisposable
{
    private readonly string _install = Path.Combine(TestHomes.Root, "daoris-copy-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<Action> _undo = [];

    public HomeCopyTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_install, "INSTALLED.md"), "# Daoris — installed desktop\n");
    }

    private string Home => Path.Combine(_install, "data");

    private string Into => Path.Combine(_install, StagedBuild.Folder, HomeProbe.Scratch, "c1");

    public void Dispose()
    {
        foreach (var undo in _undo) undo();
        if (Directory.Exists(_install)) FolderRemoval.Delete(_install);
    }

    private string Put(string relative, string text = "")
    {
        var path = Path.Combine(_install, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private HomeCopyMade Make() => HomeCopy.Make(Home, Into, HomeProof.Reads);


    [Fact]
    public void A_seat_a_running_service_takes_is_named_never_opened_and_another_held_file_is_a_counted_stand_in()
    {
        var seat = Put("data/checks/broker.lock", "seat");
        var log = Put("data/checks/04e0ba77.log", "a check's log");
        Put("data/workflows/w.json", "{}");

        HomeCopyMade made;
        using (new FileStream(seat, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            made = Make();
        }

        Assert.Equal(0, new FileInfo(Path.Combine(made.Home, "checks", "broker.lock")).Length);
        Assert.Equal(0, new FileInfo(Path.Combine(made.Home, "checks", "04e0ba77.log")).Length);
        Assert.Equal("{}", File.ReadAllText(Path.Combine(made.Home, "workflows", "w.json")));
        Assert.Equal((1, 1), (made.Seats, made.StandIns));
        Assert.Contains("1 held", made.Said);
    }

    [Fact]
    public void A_held_record_the_proof_s_reading_reads_refuses_the_copy_naming_it()
    {
        var held = Put("data/workflows/w.json", "{}");

        using (new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var refused = Assert.Throws<HomeCopyRefused>(Make);
            Assert.Contains("`data/workflows/w.json`", refused.Message);
        }
    }

    [Fact]
    public void Only_a_sharing_or_lock_violation_is_a_holder_s_any_other_fault_refuses()
    {
        Assert.True(HomeCopy.Held(new IOException("in use", unchecked((int)0x80070020))));
        Assert.True(HomeCopy.Held(new IOException("locked", unchecked((int)0x80070021))));
        Assert.False(HomeCopy.Held(new IOException("a data error", unchecked((int)0x80070017))));
        Assert.False(HomeCopy.Held(new UnauthorizedAccessException("denied")));
    }

    [Fact]
    public void A_folder_the_reading_lists_that_cannot_be_listed_refuses_the_copy()
    {
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "claude-code", "acct-a"));
        Put("data/brainstorms/b1/notes.md", "kept");
        Assert.True(Directory.Exists(Path.Combine(Make().Home, "brainstorms", "b1")));
        HomeCopy.Remove(Into);

        var loop = Path.Combine(_install, "profiles", "agents", "claude-code");
        if (!UnlistableFolder.Loop(loop)) return;
        var refused = Assert.Throws<HomeCopyRefused>(Make);
        // The link itself, which a recursive delete cannot unmount.
        Directory.Delete(loop);
        Assert.Contains("`profiles/agents/claude-code`", refused.Message);
    }

    [Fact]
    public void A_deeper_folder_nothing_reads_that_cannot_be_listed_is_kept_empty_and_counted()
    {
        Put("data/brainstorms/b1/notes.md", "unread");
        if (UnlistableFolder.Denied(Path.Combine(_install, "data", "brainstorms", "b1")) is not { } denied) return;
        _undo.Add(denied.Dispose);

        var made = Make();

        Assert.Equal(1, made.Unlisted);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(made.Home, "brainstorms", "b1")));
    }

    [Fact]
    public void A_home_that_cannot_be_listed_refuses_the_copy()
    {
        Directory.Delete(_install, recursive: true);

        Assert.Throws<HomeCopyRefused>(Make);
    }

    [Fact]
    public void Content_folders_are_left_out_by_name_and_a_record_at_any_depth_is_copied()
    {
        const string slot = "data/workflows/lines/trees/slot-1";
        Put(slot + "/README.md", "a tree's own file");
        Put("data/plugins/daoris-github/index.mjs", "a package");
        Put("data/plugins/.trials/t1/index.mjs", "a trial");
        Put("data/plugins/.data/daoris-github/state.json", "{\"kept\":1}");
        Put("data/workflows/units/a/b/c/d/e/f.json", "{\"deep\":1}");
        Put("data/sessions/4f0c2a91.events.jsonl", "{}\n");

        var made = Make();

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(made.Home, "workflows", "lines", "trees")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(made.Home, "plugins", "daoris-github")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(made.Home, "plugins", ".trials")));
        Assert.Equal("{\"kept\":1}", File.ReadAllText(Path.Combine(made.Home, "plugins", ".data", "daoris-github", "state.json")));
        Assert.Equal("{\"deep\":1}", File.ReadAllText(Path.Combine(made.Home, "workflows", "units", "a", "b", "c", "d", "e", "f.json")));
        Assert.Equal("{}\n", File.ReadAllText(Path.Combine(made.Home, "sessions", "4f0c2a91.events.jsonl")));
        Assert.Equal(3, made.Content);
    }

    [Fact]
    public void A_read_only_hard_link_is_copied_as_its_content_a_large_file_as_an_empty_one_unless_the_reading_reads_it()
    {
        var store = Put("work/deps/k1/node_modules/pkg/index.js", "module.exports = 1;");
        File.SetAttributes(store, FileAttributes.ReadOnly);
        var linked = Path.Combine(Home, "kept", "pkg", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        Assert.True(FileLinks.Make(linked, store), "the volume links");
        using (var large = File.Create(Path.Combine(Home, "knowledge.db"))) large.SetLength(HomeCopy.MaxFile + 1);
        Directory.CreateDirectory(Path.Combine(Home, "sessions"));
        using (var record = File.Create(Path.Combine(Home, "sessions", "4f0c2a91.events.jsonl"))) record.SetLength(HomeCopy.MaxFile + 1);

        var made = Make();

        var copied = new FileInfo(Path.Combine(made.Home, "kept", "pkg", "index.js"));
        Assert.Equal("module.exports = 1;", File.ReadAllText(copied.FullName));
        Assert.False(copied.IsReadOnly);
        Assert.Equal(0, new FileInfo(Path.Combine(made.Home, "knowledge.db")).Length);
        Assert.Equal(HomeCopy.MaxFile + 1, new FileInfo(Path.Combine(made.Home, "sessions", "4f0c2a91.events.jsonl")).Length);
        Assert.Equal(1, made.LeftOut);
        HomeCopy.Remove(Into);
        Assert.False(Directory.Exists(Into));
        Assert.True(File.Exists(store) && File.Exists(linked), "the source is read, never written");
    }

    [Fact]
    public void Every_place_the_proof_s_reading_reads_is_one_the_copy_reads_and_none_lies_in_a_content_folder()
    {
        var folder = DaorisHome.FolderOf(Home);
        string Relative(string path) => Path.GetRelativePath(folder, path).Replace('\\', '/');
        // The folders the reading opens, by the functions the reading opens them with.
        var opened = new[]
        {
            Relative(Path.GetDirectoryName(HarnessAccountFolders.ProfileHome(Home, "claude-code", "acct-a"))!),
            Relative(DaorisHome.Entry(Home, HomeEntry.Sessions)),
            Relative(Path.GetDirectoryName(DesktopStore.LocationIn(Home))!),
            Relative(WorkflowStore.FolderOf(Home)),
            Relative(WorkflowRunBindings.FolderOf(Home)),
        };

        // Listed by the copy: a place of the reading's passes through the folder or names it.
        static bool Listed(string place) => HomeProof.Reads.Select(read => read.Split('/')).Any(read =>
            place.Split('/') is var parts && read.Length >= parts.Length
            && parts.Select((part, at) => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(read[at], part)).All(match => match));
        foreach (var place in opened)
        {
            Assert.True(Listed(place), $"`{place}` is a place the copy lists");
            Assert.False(HomeCopy.IsContent(place), $"`{place}` is copied");
        }

        Assert.Contains(Relative(DesktopStore.LocationIn(Home)), HomeProof.Reads);
        Assert.Equal("data/workflows/lines/trees", Relative(Path.Combine(VerifyFiles.LinesFolder(Home), "trees")));
        Assert.True(HomeCopy.IsContent("data/workflows/lines/trees") && HomeCopy.IsContent("data/plugins/" + PluginTrials.TrialsFolder));
        Assert.False(HomeCopy.IsContent("data/plugins/" + PluginCatalog.DataFolder));
    }

    [Fact]
    public void A_home_holding_a_record_of_each_kind_and_content_in_every_left_out_folder_reads_from_the_copy_as_from_the_home()
    {
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "claude-code", "acct-a"));
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "codex", "acct-b"));
        Put("data/sessions/4f0c2a91.events.jsonl", "{\"seq\":1,\"kind\":\"user\",\"text\":\"hi\"}\n");
        Put("data/workflows/broken.json", "{ not json");
        Put("data/workflows/runs/r-0001.json", "{ not json");
        Put("data/stray-place/thing.txt", "x");
        foreach (var content in new[] { "data/workflows/lines/trees/slot-1", "data/plugins/daoris-github", "data/plugins/.trials/t1" })
        {
            Put(content + "/broken.json", "{ not json");
            Put(content + "/runs/r-0002.json", "{ not json");
        }

        var made = Make();

        var home = HomeProof.Read(Home);
        var copy = HomeProof.Read(made.Home);
        Assert.Equal(home.Accounts, copy.Accounts);
        Assert.Equal((home.Sessions, home.Runs), (copy.Sessions, copy.Runs));
        Assert.Empty(HomeProbe.Novel(copy.Problems, home.Problems.Select(problem => problem.Replace(Home, made.Home, StringComparison.OrdinalIgnoreCase)), made.Home));
        Assert.Equal(home.Problems.Count, copy.Problems.Count);
        Assert.Equal(home.Strays, copy.Strays);
    }

    [Fact]
    public void A_copy_left_with_a_read_only_mark_is_removed()
    {
        var made = Path.Combine(_install, "update", "probe", "left");
        var marked = Put("update/probe/left/data/x.js", "x");
        File.SetAttributes(marked, FileAttributes.ReadOnly);

        HomeCopy.Remove(made);

        Assert.False(Directory.Exists(made));
    }
}
