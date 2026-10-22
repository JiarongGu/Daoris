using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1a (D199 point 5; the co-agent design §2.5, §8.2): the asker's work in progress captured for its helper, over a real folder
/// with git's two answers handed in, so nothing starts a process: the fast half (MOD8). Changed, new, binary, renamed and deleted
/// files are carried with their manifest; an unresolved merge, a submodule, a nested repository, an unreadable file and a capture
/// over its bounds are each refused; a capture that changed under it is taken again once, and refused when it changes again; and
/// the capture is laid over a copy whole. Links, which need a junction made by a process, and real git are
/// <see cref="HelperCaptureProcessTests"/>'.
/// </summary>
public sealed class HelperCaptureTests : IDisposable
{
    private const string Commit = "2222222222222222222222222222222222222222";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-helper-capture-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _tree;

    public HelperCaptureTests()
    {
        _tree = Path.Combine(_root, "tree");
        Directory.CreateDirectory(_tree);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Put(string path, string text) => Put(path, System.Text.Encoding.UTF8.GetBytes(text));

    private void Put(string path, byte[] bytes)
    {
        var full = Path.Combine(_tree, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }

    /// <summary>git as the capture asks it: the commit, and each status listing in turn (the last one repeated), every call kept.</summary>
    private sealed class Git(params string[] listings)
    {
        public List<IReadOnlyList<string>> Asked { get; } = [];
        public Action<int>? OnStatus { get; init; }
        private int _statuses;

        public Task<(int Code, string Stdout, string Stderr)> RunAsync(string tree, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            Asked.Add(arguments);
            if (arguments.Contains("rev-parse")) return Task.FromResult((0, Commit + "\n", ""));
            var at = _statuses++;
            OnStatus?.Invoke(at);
            return Task.FromResult((0, listings[Math.Min(at, listings.Length - 1)], ""));
        }
    }

    private static string Changed(string path, string xy = ".M") => $"1 {xy} N... 100644 100644 100644 {new string('1', 40)} {new string('1', 40)} {path}\0";

    private static string Renamed(string path, string from) => $"2 R. N... 100644 100644 100644 {new string('1', 40)} {new string('1', 40)} R100 {path}\0{from}\0";

    private static string Untracked(string path) => $"? {path}\0";

    private Task<HelperCaptureRead> Take(Git git) => HelperCapture.TakeAsync(_tree, git.RunAsync);

    [Fact]
    public async Task Changed_new_binary_renamed_and_deleted_files_are_carried_with_their_manifest()
    {
        Put("src/report.ts", "export const days = 7;\n");
        Put("notes/new note.md", "a name with a space\n");
        byte[] binary = [0, 1, 2, 255, 0, 13, 10];
        Put("assets/icon.bin", binary);
        Put("src/moved.ts", "moved\n");
        var git = new Git(
            Changed("src/report.ts") + Changed("old.md", ".D") + Untracked("notes/new note.md") + Untracked("assets/icon.bin")
            + Renamed("src/moved.ts", "src/before.ts") + "# branch.oid " + Commit + "\0");

        var read = await Take(git);

        var capture = Assert.IsType<HelperCaptured>(read.Captured);
        Assert.Equal(Commit, capture.Commit);
        Assert.Equal(["assets/icon.bin", "notes/new note.md", "src/moved.ts", "src/report.ts"], capture.Files.Select(file => file.Path));
        Assert.Equal(["old.md", "src/before.ts"], capture.Deleted);
        Assert.Empty(capture.LeftOut);
        Assert.Equal(binary, capture.Bytes["assets/icon.bin"]);
        Assert.Equal(HelperCaptures.HashOf(binary), capture.Files[0].Hash);
        Assert.Equal(binary.Length, capture.Files[0].Size);
        Assert.Equal(HelperCaptures.IdOf(Commit, capture.Files, capture.Deleted), capture.Id);
        Assert.Matches("^[0-9a-f]{12}$", capture.Id);

        // Asked so its status writes no index: the asker's index and refs are never touched.
        var status = git.Asked.First(arguments => arguments.Contains("status"));
        Assert.Equal("--no-optional-locks", status[0]);
        Assert.Contains("--untracked-files=all", status);
        Assert.DoesNotContain("--ignored", status);
    }

    /// <summary>The same work makes the same name; another byte makes another.</summary>
    [Fact]
    public async Task The_same_work_makes_the_same_name_and_another_byte_another()
    {
        Put("a.txt", "one\n");
        var first = (await Take(new Git(Untracked("a.txt")))).Captured!;
        var again = (await Take(new Git(Untracked("a.txt")))).Captured!;
        Put("a.txt", "two\n");
        var other = (await Take(new Git(Untracked("a.txt")))).Captured!;

        Assert.Equal(first.Id, again.Id);
        Assert.NotEqual(first.Id, other.Id);
    }

    [Fact]
    public async Task A_clean_tree_is_captured_as_nothing_uncommitted()
    {
        var capture = (await Take(new Git(""))).Captured!;

        Assert.True(capture.Clean);
        Assert.Equal("nothing uncommitted", HelperCaptures.Counted(capture));
    }

    [Theory]
    [InlineData("u", HelperWhy.CaptureMerge)]
    [InlineData("submodule", HelperWhy.CaptureSubmodule)]
    [InlineData("nested", HelperWhy.CaptureSubmodule)]
    public async Task An_unresolved_merge_a_submodule_and_a_nested_repository_are_refused(string row, string code)
    {
        Put("a.txt", "one\n");
        Directory.CreateDirectory(Path.Combine(_tree, "vendor", "repo", ".git"));
        var listing = row switch
        {
            "u" => $"u UU N... 100644 100644 100644 100644 {new string('1', 40)} {new string('2', 40)} {new string('3', 40)} a.txt\0",
            "submodule" => $"1 .M SC.. 160000 160000 160000 {new string('1', 40)} {new string('1', 40)} vendor/lib\0",
            _ => Untracked("vendor/repo/"),
        };

        var read = await Take(new Git(Untracked("a.txt") + listing));

        Assert.Null(read.Captured);
        Assert.Equal(code, read.Code);
    }

    [Fact]
    public async Task A_file_over_its_bound_and_a_capture_over_its_paths_are_refused()
    {
        var big = Path.Combine(_tree, "big.bin");
        using (var stream = File.Create(big)) stream.SetLength(HelperCaptures.FileBound + 1);

        var tooBig = await Take(new Git(Untracked("big.bin")));
        var tooMany = await Take(new Git(string.Concat(Enumerable.Range(0, HelperCaptures.PathBound + 1).Select(at => Untracked($"f{at}.txt")))));

        Assert.Equal((HelperWhy.CaptureBound, "big.bin"), (tooBig.Code, tooBig.Detail));
        Assert.Equal(HelperWhy.CaptureBound, tooMany.Code);
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_refused()
    {
        Put("held.txt", "held\n");
        using var held = new FileStream(Path.Combine(_tree, "held.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var read = await Take(new Git(Untracked("held.txt")));

        Assert.Equal((HelperWhy.CaptureUnreadable, "held.txt"), (read.Code, read.Detail));
    }

    [Fact]
    public async Task A_tree_git_cannot_read_is_unreadable()
    {
        var read = await HelperCapture.TakeAsync(_tree, (_, _, _) => Task.FromResult((128, "", "fatal: not a git repository")));

        Assert.Equal((HelperWhy.Unreadable, "fatal: not a git repository"), (read.Code, read.Detail));
    }

    /// <summary>
    /// A file written while it was read is seen when the bytes are hashed again: that capture is thrown away and taken once more,
    /// and the second, read still, is the one kept, with the new bytes.
    /// </summary>
    [Fact]
    public async Task A_capture_that_changed_under_it_is_taken_again_once()
    {
        Put("a.txt", "before\n");
        var git = new Git(Untracked("a.txt")) { OnStatus = at => { if (at == 1) Put("a.txt", "after\n"); } };

        var read = await Take(git);

        Assert.Equal("after\n", System.Text.Encoding.UTF8.GetString(read.Captured!.Bytes["a.txt"]));
        Assert.Equal(4, git.Asked.Count(arguments => arguments.Contains("status")));
    }

    /// <summary>A listing that moves at every read is refused `capture-moved` after the second capture, never carried in part.</summary>
    [Fact]
    public async Task A_capture_that_changed_again_is_refused()
    {
        Put("a.txt", "one\n");
        Put("b.txt", "two\n");
        var git = new Git(Untracked("a.txt"), Untracked("a.txt") + Untracked("b.txt"), Untracked("a.txt"), Untracked("a.txt") + Untracked("b.txt"));

        var read = await Take(git);

        Assert.Equal(HelperWhy.CaptureMoved, read.Code);
    }

    /// <summary>
    /// Laid over a copy: each deletion taken out, each file written with its bytes, its folders made, a file the copy held replaced;
    /// the copy's other files left as they are.
    /// </summary>
    [Fact]
    public void The_capture_is_laid_over_a_copy_whole()
    {
        var copy = Path.Combine(_root, "copy");
        Directory.CreateDirectory(Path.Combine(copy, "src"));
        File.WriteAllText(Path.Combine(copy, "old.md"), "old\n");
        File.WriteAllText(Path.Combine(copy, "src", "report.ts"), "export const days = 6;\n");
        File.WriteAllText(Path.Combine(copy, "kept.md"), "kept\n");
        byte[] binary = [0, 255, 13, 10];
        var capture = new HelperCaptured("0123456789ab", Commit, DateTimeOffset.UtcNow,
            [new CapturedFile("src/report.ts", "", 0), new CapturedFile("deep/new/icon.bin", "", 0)], ["old.md"], [])
        {
            Bytes = new Dictionary<string, byte[]>
            {
                ["src/report.ts"] = "export const days = 7;\n"u8.ToArray(),
                ["deep/new/icon.bin"] = binary,
            },
        };

        Assert.Null(HelperCapture.LayOver(copy, capture));

        Assert.False(File.Exists(Path.Combine(copy, "old.md")));
        Assert.Equal("export const days = 7;\n", File.ReadAllText(Path.Combine(copy, "src", "report.ts")));
        Assert.Equal(binary, File.ReadAllBytes(Path.Combine(copy, "deep", "new", "icon.bin")));
        Assert.Equal("kept\n", File.ReadAllText(Path.Combine(copy, "kept.md")));
    }

    /// <summary>A path that leads out of the copy is never written, and a file the capture did not carry is not invented.</summary>
    [Fact]
    public void A_path_out_of_the_copy_or_a_file_not_carried_is_not_laid()
    {
        var copy = Path.Combine(_root, "copy");
        Directory.CreateDirectory(copy);
        var escaping = new HelperCaptured("x", Commit, DateTimeOffset.UtcNow, [new CapturedFile("../outside.txt", "", 0)], [], [])
        {
            Bytes = new Dictionary<string, byte[]> { ["../outside.txt"] = [1] },
        };
        var missing = new HelperCaptured("y", Commit, DateTimeOffset.UtcNow, [new CapturedFile("a.txt", "", 0)], [], []);

        Assert.NotNull(HelperCapture.LayOver(copy, escaping));
        Assert.False(File.Exists(Path.Combine(_root, "outside.txt")));
        Assert.NotNull(HelperCapture.LayOver(copy, missing));
    }

    [Fact]
    public void Gits_listing_reads_each_kind_and_a_path_with_spaces_whole()
    {
        var entries = HelperCaptures.ParseStatus(
            "# branch.oid x\0" + Changed("a b/c d.txt") + Renamed("new.txt", "old name.txt") + Untracked("u.txt") + "! ignored.txt\0")!;

        Assert.Equal(
            [('1', "a b/c d.txt", false, (string?)null), ('2', "new.txt", false, "old name.txt"), ('?', "u.txt", false, null)],
            entries.Select(entry => (entry.Kind, entry.Path, entry.Submodule, entry.From)));
        Assert.Null(HelperCaptures.ParseStatus("1 .M short\0"));
        Assert.Null(HelperCaptures.ParseStatus("z what\0"));
    }
}
