using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A review's range read from ONE git answer and split into each file's patch (REVIEW3). The answer is handed in as git
/// wrote it, captured from real git over a fixture that holds every shape below, so this class starts no process and runs
/// in the fast half. The same shapes over real git, with the processes a review starts counted, are <c>TreeDiffTests</c>.
/// </summary>
/// <remarks>
/// One git start on a large repository costs about a second, and the review used to make one per changed file: a session
/// of 61 files took 52.7 s to review on an install. The count is held here by the seam the review reads git through.
/// </remarks>
public sealed class TreeDiffSplitTests
{
    private const string Before = "ab5fb09a1b2c3d4e5f60718293a4b5c6d7e8f901";
    private const string Tip = "da9ddca1b2c3d4e5f60718293a4b5c6d7e8f9012";

    private static readonly string Root = AppContext.BaseDirectory;

    /// <summary>
    /// <c>git -c core.quotePath=false diff --no-color --no-ext-diff --src-prefix=a/ --dst-prefix=b/ -M -z --raw --numstat -p</c>
    /// over a range holding a modification, an addition, a deletion, a symlink become a file (a typechange, whose patch git
    /// writes in two halves), a binary file, two renames with spaces and without changes, a path holding a double quote and
    /// a tab (which git quotes on the `diff --git` line and not in the lists), a rename with a change, a mode-only change, a
    /// path with a space and one outside ASCII. Captured from git 2.53; the quoted path was committed through the index,
    /// since no Windows disk can hold it.
    /// </summary>
    private static readonly string Captured = string.Concat(
        ":100644 100644 8239e59 df97349 M\0README.md\0",
        ":000000 100644 0000000 3e75765 A\0added.txt\0",
        ":100644 000000 2d030d7 0000000 D\0gone.txt\0",
        ":120000 100644 8239e59 3f899ea T\0link\0",
        ":000000 100644 0000000 d652de3 A\0logo.bin\0",
        ":100644 100644 28ce6a8 28ce6a8 R100\0mv space.txt\0moved space.txt\0",
        ":100644 100644 03e6bdc 03e6bdc R100\0pure.txt\0pure-moved.txt\0",
        ":100644 100644 2fa992c 5975685 M\0quo\"te\ttab.txt\0",
        ":100644 100644 bab081f 377a22a R096\0moved.txt\0renamed.txt\0",
        ":100644 100755 8b2fe54 8b2fe54 M\0run.sh\0",
        ":100644 100644 7898192 422c2b7 M\0with space.txt\0",
        ":100644 100644 6178079 9ddeb5c M\0中文.txt\0",
        "1\t1\tREADME.md\0",
        "1\t0\tadded.txt\0",
        "0\t1\tgone.txt\0",
        "1\t1\tlink\0",
        "-\t-\tlogo.bin\0",
        "0\t0\t\0mv space.txt\0moved space.txt\0",
        "0\t0\t\0pure.txt\0pure-moved.txt\0",
        "1\t0\tquo\"te\ttab.txt\0",
        "1\t1\t\0moved.txt\0renamed.txt\0",
        "0\t0\trun.sh\0",
        "1\t0\twith space.txt\0",
        "1\t0\t中文.txt\0",
        "\0",
        Patches.Readme,
        Patches.Added,
        Patches.Gone,
        Patches.Link,
        Patches.Logo,
        Patches.MovedSpace,
        Patches.PureMoved,
        Patches.Quoted,
        Patches.Renamed,
        Patches.RunSh,
        Patches.WithSpace,
        Patches.NonAscii);

    /// <summary>Each file's part of the captured answer, as git wrote it.</summary>
    private static class Patches
    {
        public const string Readme =
            "diff --git a/README.md b/README.md\nindex 8239e59..df97349 100644\n--- a/README.md\n+++ b/README.md\n"
            + "@@ -1 +1 @@\n-# engine\n+# changed\n";

        public const string Added =
            "diff --git a/added.txt b/added.txt\nnew file mode 100644\nindex 0000000..3e75765\n--- /dev/null\n+++ b/added.txt\n"
            + "@@ -0,0 +1 @@\n+new\n";

        public const string Gone =
            "diff --git a/gone.txt b/gone.txt\ndeleted file mode 100644\nindex 2d030d7..0000000\n--- a/gone.txt\n+++ /dev/null\n"
            + "@@ -1 +0,0 @@\n-delete me\n";

        public const string Link =
            "diff --git a/link b/link\ndeleted file mode 120000\nindex 8239e59..0000000\n--- a/link\n+++ /dev/null\n"
            + "@@ -1 +0,0 @@\n-# engine\n"
            + "diff --git a/link b/link\nnew file mode 100644\nindex 0000000..3f899ea\n--- /dev/null\n+++ b/link\n"
            + "@@ -0,0 +1 @@\n+now a file\n";

        public const string Logo =
            "diff --git a/logo.bin b/logo.bin\nnew file mode 100644\nindex 0000000..d652de3\n"
            + "Binary files /dev/null and b/logo.bin differ\n";

        public const string MovedSpace =
            "diff --git a/mv space.txt b/moved space.txt\nsimilarity index 100%\nrename from mv space.txt\nrename to moved space.txt\n";

        public const string PureMoved =
            "diff --git a/pure.txt b/pure-moved.txt\nsimilarity index 100%\nrename from pure.txt\nrename to pure-moved.txt\n";

        public const string Quoted =
            "diff --git \"a/quo\\\"te\\ttab.txt\" \"b/quo\\\"te\\ttab.txt\"\nindex 2fa992c..5975685 100644\n"
            + "--- \"a/quo\\\"te\\ttab.txt\"\n+++ \"b/quo\\\"te\\ttab.txt\"\n@@ -1 +1,2 @@\n keep\n+changed\n";

        public const string Renamed =
            "diff --git a/moved.txt b/renamed.txt\nsimilarity index 96%\nrename from moved.txt\nrename to renamed.txt\n"
            + "index bab081f..377a22a 100644\n--- a/moved.txt\n+++ b/renamed.txt\n@@ -4,7 +4,7 @@ line 3\n"
            + " line 4\n line 5\n line 6\n-line 7\n+line seven\n line 8\n line 9\n line 10\n";

        public const string RunSh = "diff --git a/run.sh b/run.sh\nold mode 100644\nnew mode 100755\n";

        public const string WithSpace =
            "diff --git a/with space.txt b/with space.txt\nindex 7898192..422c2b7 100644\n--- a/with space.txt\t\n"
            + "+++ b/with space.txt\t\n@@ -1 +1,2 @@\n a\n+b\n";

        public const string NonAscii =
            "diff --git a/中文.txt b/中文.txt\nindex 6178079..9ddeb5c 100644\n--- a/中文.txt\n"
            + "+++ b/中文.txt\n@@ -1 +1,2 @@\n b\n+c\n";
    }

    [Fact]
    public async Task Every_shape_reads_from_one_answer_with_its_status_its_counts_and_its_own_patch()
    {
        var git = new FakeGit(Captured);

        var diff = await WorkingTree.DiffAsync(Root, Before, git.Read, CancellationToken.None);

        Assert.NotNull(diff);
        Assert.Equal(Before, diff!.Base);
        Assert.Null(diff.Truncated);
        Assert.Equal(
            new WorkingTree.DiffFile[]
            {
                new("README.md", "modified", 1, 1, Patches.Readme),
                new("added.txt", "added", 1, 0, Patches.Added),
                new("gone.txt", "deleted", 0, 1, Patches.Gone),
                // A typechange is said as a change, as before, and both halves git writes for it are its patch.
                new("link", "modified", 1, 1, Patches.Link),
                // Binary: not counted, which is not "counted nothing", and its patch is git's one line about it.
                new("logo.bin", "added", null, null, Patches.Logo),
                // A rename is its new path, with the counts the whole range gave it and git's own rename as its patch.
                new("moved space.txt", "renamed", 0, 0, Patches.MovedSpace),
                new("pure-moved.txt", "renamed", 0, 0, Patches.PureMoved),
                // The lists carry the path verbatim; only the `diff --git` line quotes it, and that is what it was matched by.
                new("quo\"te\ttab.txt", "modified", 1, 0, Patches.Quoted),
                new("renamed.txt", "renamed", 1, 1, Patches.Renamed),
                new("run.sh", "modified", 0, 0, Patches.RunSh),
                new("with space.txt", "modified", 1, 0, Patches.WithSpace),
                new("中文.txt", "modified", 1, 0, Patches.NonAscii),
            },
            diff.Files);
    }

    /// <summary>
    /// A control character in a path: git writes it as an octal or a letter escape on the `diff --git` line and verbatim in
    /// the lists. Captured from git 2.53 over a path holding 0x01, DEL, a backspace and a vertical tab.
    /// </summary>
    [Fact]
    public async Task A_path_git_escapes_on_its_diff_line_is_matched_to_its_own_patch()
    {
        const string path = "c\u0001t\u007fl\b\u000bv.txt";
        const string patch =
            "diff --git \"a/c\\001t\\177l\\b\\vv.txt\" \"b/c\\001t\\177l\\b\\vv.txt\"\nnew file mode 100644\nindex 0000000..2fa992c\n"
            + "--- /dev/null\n+++ \"b/c\\001t\\177l\\b\\vv.txt\"\n@@ -0,0 +1 @@\n+keep\n";
        var answer = string.Concat(":000000 100644 0000000 2fa992c A\0", path, "\0", "1\t0\t", path, "\0", "\0", patch);

        var diff = await WorkingTree.DiffAsync(Root, Before, new FakeGit(answer).Read, CancellationToken.None);

        Assert.Equal(new WorkingTree.DiffFile(path, "added", 1, 0, patch), Assert.Single(diff!.Files));
    }

    /// <summary>git hands its answer over in pieces of whatever size the pipe gives; a piece may end anywhere.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(4096)]
    public async Task The_answer_reads_the_same_however_git_hands_it_over(int piece)
    {
        var whole = await WorkingTree.DiffAsync(Root, Before, new FakeGit(Captured).Read, CancellationToken.None);
        var pieces = await WorkingTree.DiffAsync(Root, Before, new FakeGit(Captured, piece).Read, CancellationToken.None);

        Assert.Equal(whole!.Files, pieces!.Files);
        Assert.Equal(whole.Truncated, pieces.Truncated);
    }

    /// <summary>
    /// 🔴 The point of REVIEW3: a review asks git twice — whether the root is a repository's top, and the range — whatever
    /// the number of files, and no read names a file.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(61)]
    [InlineData(500)]
    public async Task A_review_starts_two_git_processes_however_many_files_changed(int count)
    {
        var files = Enumerable.Range(0, count).Select(i => ($"src/file{i}.txt", Section($"src/file{i}.txt", 200))).ToList();

        var live = new FakeGit(Answer(files));
        var tree = await WorkingTree.DiffAsync(Root, Before, live.Read, CancellationToken.None);
        var landed = new FakeGit(Answer(files));
        var branch = await WorkingTree.DiffBetweenAsync(Root, Before, Tip, landed.Read, CancellationToken.None);

        foreach (var (git, diff) in new[] { (live, tree), (landed, branch) })
        {
            Assert.Equal(2, git.Calls.Count);
            Assert.Equal(new[] { "rev-parse", "--show-toplevel" }, git.Calls[0]);
            Assert.Contains("diff", git.Calls[1]);
            Assert.DoesNotContain("--", git.Calls[1]);
            Assert.Equal(count, diff!.Files.Count);
            Assert.All(diff.Files.Zip(files), pair => Assert.Equal(pair.Second.Item2, pair.First.Patch));
        }
    }

    /// <summary>
    /// The bound, exactly as each file's own read applied it: a patch is kept while less than the budget is spent, so the
    /// sixth 79,000-character patch is kept (395,000 spent before it) and every file after it is counted. And git is
    /// stopped there, not read to the end: the rest of a session that changed a great deal is never read.
    /// </summary>
    [Fact]
    public async Task The_budget_drops_whole_patches_in_order_says_so_and_stops_git_once_spent()
    {
        var files = Enumerable.Range(0, 12).Select(i => ($"big{i}.txt", Section($"big{i}.txt", 79_000))).ToList();
        var git = new FakeGit(Answer(files), piece: 4096);

        var diff = await WorkingTree.DiffAsync(Root, Before, git.Read, CancellationToken.None);

        Assert.Equal(12, diff!.Files.Count);
        Assert.Equal(files.Take(6).Select(file => file.Item2), diff.Files.Take(6).Select(file => file.Patch));
        Assert.All(diff.Files.Skip(6), file => Assert.Null(file.Patch));
        Assert.All(diff.Files, file => Assert.Equal(((int?)1, (int?)1), (file.Added, file.Removed)));
        Assert.Equal("6 more files changed; their patches are not shown here. `git diff` in the tree has all of it.", diff.Truncated);
        Assert.True(git.Stopped);
    }

    /// <summary>The landed branch's sentence names the two commits, as before.</summary>
    [Fact]
    public async Task A_landed_branch_s_dropped_patches_name_the_two_commits()
    {
        var files = Enumerable.Range(0, 7).Select(i => ($"big{i}.txt", Section($"big{i}.txt", 79_000))).ToList();

        var diff = await WorkingTree.DiffBetweenAsync(Root, Before, Tip, new FakeGit(Answer(files)).Read, CancellationToken.None);

        Assert.Equal(
            "1 more file changed; their patches are not shown here. `git diff ab5fb09a da9ddca1` in the repository's checkout has all of it.",
            diff!.Truncated);
    }

    /// <summary>One generated file cannot spend the budget alone: its patch is cut at the cap, said so, and counted as cut.</summary>
    [Fact]
    public async Task A_long_patch_is_cut_at_the_cap_and_the_files_after_it_still_read()
    {
        var huge = Section("generated.txt", 200_000);
        var small = Section("small.txt", 300);
        var git = new FakeGit(Answer([("generated.txt", huge), ("small.txt", small)]), piece: 4096);

        var diff = await WorkingTree.DiffAsync(Root, Before, git.Read, CancellationToken.None);

        Assert.Equal(huge[..WorkingTree.PatchCap] + "\n… this file's patch is longer than the surface renders.", diff!.Files[0].Patch);
        Assert.Equal(small, diff.Files[1].Patch);
        Assert.Null(diff.Truncated);
        Assert.False(git.Stopped);
    }

    /// <summary>
    /// A part of the answer headed in a way no listed file would be is kept by no file, and a file with no part of its own
    /// gets no patch rather than its neighbour's: a patch is never shown under the wrong file.
    /// </summary>
    [Fact]
    public async Task A_patch_no_file_owns_is_shown_under_none_rather_than_under_its_neighbour()
    {
        var first = Section("first.txt", 200);
        var stray = Section("stray.txt", 200);
        var last = Section("last.txt", 200);
        var answer = string.Concat(
            ":100644 100644 1111111 2222222 M\0first.txt\0",
            ":100644 100644 1111111 2222222 M\0quiet.txt\0",
            ":100644 100644 1111111 2222222 M\0last.txt\0",
            "1\t1\tfirst.txt\0", "1\t1\tquiet.txt\0", "1\t1\tlast.txt\0",
            "\0",
            first, stray, last);

        var diff = await WorkingTree.DiffAsync(Root, Before, new FakeGit(answer).Read, CancellationToken.None);

        Assert.Equal(new string?[] { first, null, last }, diff!.Files.Select(file => file.Patch));
        Assert.Null(diff.Truncated);
    }

    /// <summary>A range git cannot read is null, which a surface says; a range that changed nothing is an empty answer.</summary>
    [Fact]
    public async Task Git_that_cannot_answer_is_null_and_a_range_that_changed_nothing_is_no_files()
    {
        Assert.Null(await WorkingTree.DiffAsync(Root, Before, new FakeGit("", code: 128).Read, CancellationToken.None));

        var nothing = await WorkingTree.DiffAsync(Root, Before, new FakeGit("").Read, CancellationToken.None);
        Assert.NotNull(nothing);
        Assert.Empty(nothing!.Files);
        Assert.Null(nothing.Truncated);
    }

    /// <summary>A root that is not the top of a repository of its own is refused before the range is asked (git walks UP).</summary>
    [Fact]
    public async Task A_root_git_places_elsewhere_is_refused_before_the_range_is_asked()
    {
        var git = new FakeGit(Captured, topLevel: Path.GetTempPath());

        Assert.Null(await WorkingTree.DiffAsync(Root, Before, git.Read, CancellationToken.None));
        Assert.Single(git.Calls);
    }

    /// <summary>The plan's count asks only a tree this home opened: a folder it did not make (a person's checkout) is not counted (D49 §3).</summary>
    [Fact]
    public async Task The_plan_s_uncommitted_count_is_not_asked_of_a_folder_this_home_did_not_open()
    {
        Assert.Null(await new SessionTrees(Root).UncommittedAsync(Root));
    }

    /// <summary>git that answers each kind of ask its own way, as the uncommitted read makes three of them (REVIEWDIRTY1-a).</summary>
    private static WorkingTree.GitRead Dirty(string range, string status, Func<string, (int Code, string Text)> noIndex, List<IReadOnlyList<string>>? calls = null) =>
        (root, arguments, take, ct) =>
        {
            calls?.Add([.. arguments]);
            var (code, text) = arguments switch
            {
                _ when arguments.Contains("--show-toplevel") => (0, root.Replace('\\', '/') + "\n"),
                _ when arguments.Contains("--no-index") => noIndex(arguments[^1]),
                _ when arguments.Contains("status") => (0, status),
                _ => (0, range),
            };
            take(text.AsMemory());
            return Task.FromResult(code);
        };

    /// <summary>REVIEWDIRTY1-a: the splitter is agnostic to the revision, so `HEAD` (the index and the files against it) reads as a range does.</summary>
    [Fact]
    public async Task The_uncommitted_read_splits_a_head_range_as_it_splits_a_committed_one()
    {
        var calls = new List<IReadOnlyList<string>>();

        var diff = await WorkingTree.UncommittedDiffAsync(Root, Dirty(Captured, "", _ => (1, ""), calls), CancellationToken.None);

        Assert.NotNull(diff);
        Assert.Equal(12, diff!.Files.Count);
        Assert.Equal(Patches.Readme, diff.Files[0].Patch);
        Assert.Null(diff.Truncated);
        Assert.Contains(calls, call => call[^1] == "HEAD" && call.Contains("--raw"));
    }

    /// <summary>
    /// REVIEWLOCK1: the uncommitted read asks git not to refresh the index, which would take the tree's <c>index.lock</c>, so a file
    /// whose stat data alone moved comes back listed <c>M</c> with no count and no patch (as git 2.53 wrote it), and is no file.
    /// </summary>
    [Fact]
    public async Task The_uncommitted_read_takes_no_lock_and_a_file_whose_stat_alone_moved_is_no_file()
    {
        const string patch = "diff --git a/b.txt b/b.txt\nindex 6178079..5ea2ed4 100644\n--- a/b.txt\n+++ b/b.txt\n@@ -1 +1 @@\n-b\n+changed\n";
        var range = ":100644 100644 7898192 0000000 M\0a.txt\0:100644 100644 6178079 0000000 M\0b.txt\0" + "1\t1\tb.txt\0" + "\0" + patch;
        var calls = new List<IReadOnlyList<string>>();

        var diff = await WorkingTree.UncommittedDiffAsync(Root, Dirty(range, " M b.txt\0", _ => (1, ""), calls), CancellationToken.None);

        var file = Assert.Single(diff!.Files);
        Assert.Equal(("b.txt", 1, 1, patch), (file.Path, file.Added, file.Removed, file.Patch));
        var read = calls.Single(call => call[^1] == "HEAD" && call.Contains("--raw"));
        var status = calls.Single(call => call.Contains("status"));
        foreach (var call in new[] { read, status })
        {
            Assert.Equal("--no-optional-locks", call[0]);
        }

        var words = read.ToList();
        Assert.True(words.IndexOf("diff.autoRefreshIndex=false") is var at && at > 0 && at < words.IndexOf("diff") && words[at - 1] == "-c");
    }

    /// <summary>An untracked file is listed `A`, with the patch git's no-index diff gives and the lines it added counted.</summary>
    [Fact]
    public async Task An_untracked_file_is_listed_added_with_its_no_index_patch()
    {
        const string patch = "diff --git a/fresh.txt b/fresh.txt\nnew file mode 100644\nindex 0000000..3e75765\n--- /dev/null\n+++ b/fresh.txt\n@@ -0,0 +1,2 @@\n+one\n+two\n";
        var status = " M README.md\0?? fresh.txt\0?? dir/blob.bin\0";
        var git = Dirty(Captured, status, path => path == "fresh.txt"
            ? (1, patch)
            : (1, "diff --git a/dir/blob.bin b/dir/blob.bin\nnew file mode 100644\nindex 0000000..d652de3\nBinary files /dev/null and b/dir/blob.bin differ\n"));

        var diff = await WorkingTree.UncommittedDiffAsync(Root, git, CancellationToken.None);

        var fresh = diff!.Files.Single(file => file.Path == "fresh.txt");
        Assert.Equal("added", fresh.Status);
        Assert.Equal(2, fresh.Added);
        Assert.Equal(0, fresh.Removed);
        Assert.Equal(patch, fresh.Patch);
        // Binary: listed, with no patch and no count.
        var blob = diff.Files.Single(file => file.Path == "dir/blob.bin");
        Assert.Equal("added", blob.Status);
        Assert.Null(blob.Patch);
        Assert.Null(blob.Added);
    }

    /// <summary>A file over 200 KB is listed with no patch, and git is not asked for it.</summary>
    [Fact]
    public async Task An_untracked_file_over_two_hundred_kilobytes_is_listed_with_no_patch()
    {
        var name = $"big-{Guid.NewGuid():N}.txt";
        var path = Path.Combine(Root, name);
        await File.WriteAllTextAsync(path, new string('x', WorkingTree.UntrackedPatchLimit + 1));
        try
        {
            var asked = new List<string>();
            var git = Dirty("", $"?? {name}\0", file => { asked.Add(file); return (1, "x"); });

            var diff = await WorkingTree.UncommittedDiffAsync(Root, git, CancellationToken.None);

            var file = Assert.Single(diff!.Files);
            Assert.Equal(("added", null), (file.Status, file.Patch));
            Assert.Empty(asked);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Nothing uncommitted is an empty answer, not null; a root git places elsewhere, or git failing, is null.</summary>
    [Fact]
    public async Task A_clean_tree_is_no_files_and_a_tree_git_cannot_read_is_null()
    {
        var clean = await WorkingTree.UncommittedDiffAsync(Root, Dirty("", "", _ => (1, "")), CancellationToken.None);
        Assert.Empty(clean!.Files);
        Assert.Null(clean.Truncated);

        Assert.Null(await WorkingTree.UncommittedDiffAsync(Root, new FakeGit(Captured, topLevel: Path.GetTempPath()).Read, CancellationToken.None));
        Assert.Null(await WorkingTree.UncommittedDiffAsync(Root, new FakeGit("", code: 128).Read, CancellationToken.None));
    }

    /// <summary>The answer for these files, as git writes it: each one modified, counted 1 and 1, and its patch.</summary>
    private static string Answer(IReadOnlyList<(string Path, string Patch)> files) =>
        string.Concat(files.Select(file => $":100644 100644 1111111 2222222 M\0{file.Path}\0"))
        + string.Concat(files.Select(file => $"1\t1\t{file.Path}\0"))
        + "\0"
        + string.Concat(files.Select(file => file.Patch));

    /// <summary>One file's patch as git heads it, filled with added lines to exactly <paramref name="length"/> characters.</summary>
    private static string Section(string path, int length)
    {
        var text = new System.Text.StringBuilder(
            $"diff --git a/{path} b/{path}\nindex 1111111..2222222 100644\n--- a/{path}\n+++ b/{path}\n@@ -1 +1,9 @@\n");
        // Every line at least `+` and its newline, so the next file's `diff --git` line starts a line of its own.
        while (text.Length < length)
        {
            var left = length - text.Length;
            var line = left > 81 ? 80 : left == 81 ? 79 : left;
            text.Append('+').Append('x', line - 2).Append('\n');
        }

        Assert.Equal(length, text.Length);
        return text.ToString();
    }

    /// <summary>git as the review reads it, answering from what it was handed, and counting each start.</summary>
    private sealed class FakeGit(string answer, int piece = 1 << 20, int code = 0, string? topLevel = null)
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        /// <summary>Whether the reader stopped git before its answer ended.</summary>
        public bool Stopped { get; private set; }

        public Task<int> Read(
            string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
        {
            Calls.Add([.. arguments]);
            var asked = arguments.Contains("--show-toplevel");
            var text = asked ? (topLevel ?? root).Replace('\\', '/') + "\n" : answer;
            for (var at = 0; at < text.Length; at += piece)
            {
                if (take(text.AsMemory(at, Math.Min(piece, text.Length - at)))) continue;
                Stopped = true;
                return Task.FromResult(0);
            }

            return Task.FromResult(asked ? 0 : code);
        }
    }
}
