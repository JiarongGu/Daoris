using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A session's tree over the bridge (`TreesRoutes.cs`, MOD5): the review, the files a composer
/// offers, merge and discard, landing and the hand-off.
/// </summary>
public sealed class DriverModuleTreesTests : DriverModuleBridge
{
    /// <summary>
    /// The review route on a cold start (SURF6). Every other refusal it can raise needs a service to
    /// answer first; this one is the state a person actually meets, and it has to be a sentence.
    /// </summary>
    [Fact]
    public async Task Asking_what_a_session_landed_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_DIFF", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        // The sentence, not just the code: this is the state a person meets on a cold start.
        Assert.Contains("still coming up", refusal);
    }

    /// <summary>A review of nothing in particular is a malformed call, not an empty answer.</summary>
    [Fact]
    public async Task Asking_what_a_session_landed_without_naming_one_is_refused()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_DIFF", new { }));
    }

    /// <summary>
    /// The files a person may `@` (CONV4d) are found through the session's record, as its review is —
    /// so on a cold start the answer is the same sentence, never an empty list, which would read as a
    /// tree with nothing in it.
    /// </summary>
    [Fact]
    public async Task Asking_what_a_session_tree_holds_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_FILES", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_FILES", new { }));
    }

    /// <summary>
    /// A file's preview (PREVIEW1, D111) is found through the session's record, as its review is, so on a cold
    /// start it is the same sentence; and a preview of no file is a malformed call.
    /// </summary>
    [Fact]
    public async Task Reading_a_file_for_its_preview_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_FILE", new { id = "s1a2b3c4", path = "src/chunk.ts" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_FILE", new { id = "s1a2b3c4" }));
    }

    /// <summary>What the page receives for a file read: the path it asked for, its size, and its text — camelCase, as serialized.</summary>
    [Fact]
    public async Task A_file_read_for_its_preview_is_answered_with_its_path_its_size_and_its_text()
    {
        var tree = DaorisHome.Entry(Home, HomeEntry.Trees,"engine");
        Directory.CreateDirectory(Path.Combine(tree, "src"));
        File.WriteAllText(Path.Combine(tree, "src", "chunk.ts"), "export const a = 1;\n");

        var answer = JsonSerializer.SerializeToElement(
            await TreesRoutes.PreviewAsync("s1a2b3c4", tree, "src/chunk.ts", CancellationToken.None),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal("s1a2b3c4", answer.GetProperty("session").GetString());
        Assert.Equal("src/chunk.ts", answer.GetProperty("path").GetString());
        Assert.Equal(20, answer.GetProperty("size").GetInt64());
        Assert.False(answer.GetProperty("binary").GetBoolean());
        Assert.False(answer.GetProperty("truncated").GetBoolean());
        Assert.Equal("export const a = 1;\n", answer.GetProperty("text").GetString());
        // The tree's own path never goes back to the page (D47 §4; platform language §4).
        Assert.DoesNotContain(tree, answer.GetRawText().Replace("\\\\", "\\"));
    }

    /// <summary>
    /// Opening a preview is a line in the machine log (LEFT2, D94): the session and the file relative to the tree,
    /// asked for here by its full path, so the line is seen to keep the relative one and never the tree's own. A
    /// preview that is refused opens nothing and writes nothing.
    /// </summary>
    [Fact]
    public async Task Opening_a_preview_is_a_machine_log_line_with_the_session_and_the_path_relative_to_the_tree()
    {
        var tree = DaorisHome.Entry(Home, HomeEntry.Trees,"engine");
        Directory.CreateDirectory(Path.Combine(tree, "src"));
        File.WriteAllText(Path.Combine(tree, "src", "chunk.ts"), "export const a = 1;\n");
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.DriverConfig), "{}");
        using var log = new MachineLog(Home, "desktop", () => new DateTimeOffset(2026, 9, 30, 7, 10, 0, TimeSpan.Zero));

        await TreesRoutes.PreviewAsync("s1a2b3c4", tree, Path.Combine(tree, "src", "chunk.ts"), CancellationToken.None, log);
        await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.PreviewAsync("s1a2b3c4", tree, "../../driver.json", CancellationToken.None, log));
        await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.PreviewAsync("s1a2b3c4", tree, "src/gone.ts", CancellationToken.None, log));

        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        var lines = Directory.GetFiles(folder).SelectMany(file =>
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
        var line = JsonDocument.Parse(Assert.Single(lines)).RootElement;
        Assert.Equal("preview.opened", line.GetProperty("event").GetString());
        Assert.Equal("info", line.GetProperty("level").GetString());
        Assert.Equal("""{"session":"s1a2b3c4","path":"src/chunk.ts"}""", line.GetProperty("data").GetRawText());
    }

    /// <summary>
    /// The three refusals PREVIEW1 names, and its two pieces of information, each a code of its own: the
    /// page renders each from the catalogue, and a person's next move differs for each.
    /// </summary>
    [Theory]
    [InlineData(FilePreviewRefusal.Outside, "PREVIEW_OUTSIDE_TREE")]
    [InlineData(FilePreviewRefusal.LinkLeaves, "PREVIEW_LINK_LEAVES_TREE")]
    [InlineData(FilePreviewRefusal.GitFolder, "PREVIEW_GIT_FOLDER")]
    [InlineData(FilePreviewRefusal.NoTree, "PREVIEW_NO_TREE")]
    [InlineData(FilePreviewRefusal.NotAFile, "PREVIEW_NOT_A_FILE")]
    public void Each_reason_a_file_was_not_read_is_a_refusal_of_its_own(FilePreviewRefusal why, string code)
    {
        var error = Assert.Throws<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.FileAnswer("s1a2b3c4", "../other/secret.txt", FilePreviewResult.Refused(why)));

        Assert.Equal(code, error.Code);
        Assert.Contains(code, Refusals.All);
        // Each names what was asked for, in the words the page sent — never a machine path it was not told.
        Assert.Equal("../other/secret.txt", error.Parameters!["path"]);
    }

    /// <summary>Over a real tree: a path outside it and one under `.git` are refused by the route's own reading.</summary>
    [Fact]
    public async Task A_path_outside_the_tree_or_under_git_is_refused_by_the_route_s_reading()
    {
        var tree = DaorisHome.Entry(Home, HomeEntry.Trees,"engine");
        Directory.CreateDirectory(Path.Combine(tree, ".git"));
        File.WriteAllText(Path.Combine(tree, ".git", "config"), "[core]\n");
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.DriverConfig), "{}");

        var outside = await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.PreviewAsync("s1a2b3c4", tree, "../../driver.json", CancellationToken.None));
        var git = await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.PreviewAsync("s1a2b3c4", tree, ".git/config", CancellationToken.None));
        var none = await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.PreviewAsync("s1a2b3c4", null, "src/chunk.ts", CancellationToken.None));

        Assert.Equal(Refusals.PreviewOutsideTree, outside.Code);
        Assert.Equal(Refusals.PreviewGitFolder, git.Code);
        Assert.Equal(Refusals.PreviewNoTree, none.Code);
    }

    // ——— a landed session (REVIEW2, D113)

    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static LandedBranch Landing(string branch = "feature/0fda18-fix-the-api-gap") =>
        new("engine", "aurora", branch, "main", "abcdef1234567890", "s1a2b3c4", "0fda18", "Fix the API gap",
            new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero)) { From = "0123456789abcdef" };

    private static WorkingTree.TreeDiff Diff(string from) =>
        new([new WorkingTree.DiffFile("src/chunk.ts", "modified", 4, 1, "@@ -1 +1,2 @@\n-a\n+b")], null, from);

    /// <summary>A session with no landing here answers as it always has: its tree's diff, and no landing.</summary>
    [Fact]
    public void A_review_of_a_session_with_no_landing_is_its_tree_and_says_no_landing()
    {
        var answer = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1a2b3c4", Diff("abc1234567890"), "tree", null, treeGone: false), Camel);

        Assert.Equal("abc1234567890", answer.GetProperty("base").GetString());
        Assert.Equal("tree", answer.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("landed").ValueKind);
        Assert.Equal("src/chunk.ts", answer.GetProperty("files")[0].GetProperty("path").GetString());
    }

    /// <summary>REVIEWDIRTY1-a (D164 point 4): a tree's uncommitted work rides beside the committed list, in its shape, and says when it was cut.</summary>
    [Fact]
    public void A_trees_uncommitted_work_is_answered_beside_the_committed_files()
    {
        var dirty = new WorkingTree.TreeDiff(
            [new WorkingTree.DiffFile("new.txt", "added", 1, 0, "@@ -0,0 +1 @@\n+x")], "1 more untracked file; their patches are not shown here.", "HEAD");

        var answer = JsonSerializer.SerializeToElement(
            TreesRoutes.ReviewAnswer("s1a2b3c4", Diff("abc1234567890"), "tree", null, treeGone: false, uncommitted: dirty), Camel);

        var uncommitted = answer.GetProperty("uncommitted");
        Assert.Equal("new.txt", uncommitted.GetProperty("files")[0].GetProperty("path").GetString());
        Assert.Equal("added", uncommitted.GetProperty("files")[0].GetProperty("status").GetString());
        Assert.True(uncommitted.GetProperty("truncated").GetBoolean());
        Assert.Equal("src/chunk.ts", answer.GetProperty("files")[0].GetProperty("path").GetString());
    }

    /// <summary>Nothing uncommitted, a source that is not the session's own tree, and a branch's review all say null.</summary>
    [Fact]
    public void Uncommitted_is_null_where_nothing_is_uncommitted_or_the_source_is_not_the_sessions_own_tree()
    {
        var clean = new WorkingTree.TreeDiff([], null, "HEAD");

        var none = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1", Diff("abc"), "tree", null, treeGone: false), Camel);
        var empty = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1", Diff("abc"), "tree", null, treeGone: false, uncommitted: clean), Camel);
        var branch = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1", Diff("abc"), "branch", null, treeGone: true, uncommitted: clean), Camel);

        Assert.Equal(JsonValueKind.Null, none.GetProperty("uncommitted").ValueKind);
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("uncommitted").ValueKind);
        Assert.Equal(JsonValueKind.Null, branch.GetProperty("uncommitted").ValueKind);
    }

    /// <summary>
    /// REVIEWBASE1 (D164's note): a tree's review says how far the latest known line is past its HEAD, with the line's name, and how
    /// many commits it holds beyond the line; the page says the first where it is above zero, and the second where it is zero.
    /// </summary>
    [Fact]
    public void A_trees_review_says_how_far_the_line_is_past_it()
    {
        var answer = JsonSerializer.SerializeToElement(
            TreesRoutes.ReviewAnswer("s1a2b3c4", Diff("abc1234567890"), "tree", null, treeGone: false, behind: new LineBehind("main", 3, 1)), Camel);

        var behind = answer.GetProperty("behind");
        Assert.Equal("main", behind.GetProperty("line").GetString());
        Assert.Equal(3, behind.GetProperty("commits").GetInt32());
        Assert.Equal(1, behind.GetProperty("ahead").GetInt32());
    }

    /// <summary>A line that could not be read, and a review read from a landed branch rather than the tree, say null: a shape added.</summary>
    [Fact]
    public void Behind_is_null_where_the_line_was_not_read_or_the_source_is_a_branch()
    {
        var unread = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1", Diff("abc"), "tree", null, treeGone: false), Camel);
        var branch = JsonSerializer.SerializeToElement(
            TreesRoutes.ReviewAnswer("s1", Diff("abc"), "branch", null, treeGone: true, behind: new LineBehind("main", 2, 0)), Camel);

        Assert.Equal(JsonValueKind.Null, unread.GetProperty("behind").ValueKind);
        Assert.Equal(JsonValueKind.Null, branch.GetProperty("behind").ValueKind);
    }

    /// <summary>
    /// REVIEWBASE4 (D164's note): a tree's review names the branch its HEAD stands on, which the page's head says over the record's
    /// guess (TREEFOLLOW1); null where HEAD is detached, and on a review read from a landed branch, whose tree is not read.
    /// </summary>
    [Fact]
    public void A_trees_review_names_the_branch_its_tree_stands_on_and_none_where_detached()
    {
        var named = JsonSerializer.SerializeToElement(
            TreesRoutes.ReviewAnswer("s1", Diff("abc"), "tree", null, treeGone: false, treeBranch: "feature/TK-9-gap"), Camel);
        var detached = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1", Diff("abc"), "tree", null, treeGone: false), Camel);
        var branch = JsonSerializer.SerializeToElement(
            TreesRoutes.ReviewAnswer("s1", Diff("abc"), "branch", null, treeGone: true, treeBranch: "daoris/s1"), Camel);

        Assert.Equal("feature/TK-9-gap", named.GetProperty("treeBranch").GetString());
        Assert.Equal(JsonValueKind.Null, detached.GetProperty("treeBranch").ValueKind);
        Assert.Equal(JsonValueKind.Null, branch.GetProperty("treeBranch").ValueKind);
    }

    /// <summary>The landing plan's count: 0 where clean or not asked, else the paths the tree holds uncommitted.</summary>
    [Fact]
    public void The_landing_plan_says_how_many_paths_are_uncommitted()
    {
        Assert.Equal(0, TreesRoutes.UncommittedCount(null));
        Assert.Equal(5, TreesRoutes.UncommittedCount(5));
    }

    /// <summary>
    /// The installed window's case: the tree is gone and the landed branch stands. The review is the branch's changes,
    /// says where and when the work landed and the pull request a plugin opened, and reads as landed.
    /// </summary>
    [Fact]
    public void A_tidied_landing_is_answered_from_its_branch_with_where_and_when_it_landed()
    {
        var entry = Landing() with { Plugin = "example.lands", Pushed = true, PullRequest = "https://example.test/org/engine/pull/7" };
        var review = new LandedReview(entry, LandedState.Standing, Diff("0123456789abcdef"));

        var answer = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1a2b3c4", review.Changes, "branch", review, treeGone: true), Camel);

        Assert.Equal("branch", answer.GetProperty("source").GetString());
        Assert.Equal("0123456789abcdef", answer.GetProperty("base").GetString());
        var landed = answer.GetProperty("landed");
        Assert.Equal("feature/0fda18-fix-the-api-gap", landed.GetProperty("branch").GetString());
        Assert.Equal("engine", landed.GetProperty("repository").GetString());
        Assert.Equal("main", landed.GetProperty("line").GetString());
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero), landed.GetProperty("landedAt").GetDateTimeOffset());
        Assert.Equal("https://example.test/org/engine/pull/7", landed.GetProperty("pullRequest").GetString());
        Assert.True(landed.GetProperty("pushed").GetBoolean());
        Assert.Equal("standing", landed.GetProperty("state").GetString());
        Assert.True(landed.GetProperty("asLanded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, landed.GetProperty("reads").ValueKind);
        Assert.Equal(JsonValueKind.Null, landed.GetProperty("removed").ValueKind);
        // A landing from before who accepted it was kept says none (D143 point 3).
        Assert.Equal(JsonValueKind.Null, landed.GetProperty("acceptedBy").ValueKind);
    }

    /// <summary>LAND2b (D145 point 4): the review's answer says who accepted the landing, which the note words.</summary>
    [Fact]
    public void A_landing_at_done_is_answered_as_accepted_automatically()
    {
        var review = new LandedReview(Landing() with { AcceptedBy = AcceptedBy.Auto }, LandedState.Standing);

        var answer = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1a2b3c4", null, "branch", review, treeGone: true), Camel);

        Assert.Equal("auto", answer.GetProperty("landed").GetProperty("acceptedBy").GetString());
    }

    /// <summary>A branch gone since: no files, what the clean-up proved when it removed it, and whether its work reads on the line now.</summary>
    [Fact]
    public void A_landing_whose_branch_is_gone_says_so_what_removed_it_and_whether_its_work_reads_on_the_line()
    {
        var entry = Landing() with { GoneAt = new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.Zero), RemovedAs = LandedKind.OnLine, RemovedOn = "origin/main" };
        var review = new LandedReview(entry, LandedState.Gone, Reads: new LandedReads(LandedKind.OnLine, "main", [], null));

        var answer = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1a2b3c4", null, "branch", review, treeGone: true), Camel);

        Assert.Equal("", answer.GetProperty("base").GetString());
        Assert.Equal(0, answer.GetProperty("files").GetArrayLength());
        var landed = answer.GetProperty("landed");
        Assert.Equal("gone", landed.GetProperty("state").GetString());
        Assert.True(landed.GetProperty("asLanded").GetBoolean());
        Assert.Equal("on-line", landed.GetProperty("reads").GetProperty("kind").GetString());
        Assert.Equal("main", landed.GetProperty("reads").GetProperty("where").GetString());
        Assert.Equal("on-line", landed.GetProperty("removed").GetProperty("kind").GetString());
        Assert.Equal("origin/main", landed.GetProperty("removed").GetProperty("where").GetString());
    }

    /// <summary>
    /// A tree still here after its landed branch went may have carried on (WSR6): its review is the tree's, and it does
    /// not read as landed, so accepting the work since is a door again. While the branch stands, it reads as landed.
    /// </summary>
    [Fact]
    public void A_tree_still_here_reads_as_landed_only_while_its_landed_branch_stands()
    {
        var standing = new LandedReview(Landing(), LandedState.Standing);
        var gone = new LandedReview(Landing() with { GoneAt = DateTimeOffset.UnixEpoch }, LandedState.Gone);

        bool AsLanded(LandedReview review) => JsonSerializer.SerializeToElement(
            TreesRoutes.ReviewAnswer("s1a2b3c4", Diff("abc1234567890"), "tree", review, treeGone: false), Camel)
            .GetProperty("landed").GetProperty("asLanded").GetBoolean();

        Assert.True(AsLanded(standing));
        Assert.False(AsLanded(gone));
    }

    /// <summary>A landing whose moment did not read says none, rather than the year one.</summary>
    [Fact]
    public void A_landing_with_no_moment_says_none()
    {
        var review = new LandedReview(Landing() with { LandedAt = DateTimeOffset.MinValue }, LandedState.Standing);

        var answer = JsonSerializer.SerializeToElement(TreesRoutes.ReviewAnswer("s1a2b3c4", null, "branch", review, treeGone: true), Camel);

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("landed").GetProperty("landedAt").ValueKind);
    }

    /// <summary>A file read from the landed branch names that branch; one the branch does not hold is a refusal naming it.</summary>
    [Fact]
    public void A_file_from_the_landed_branch_names_it_and_one_it_lacks_is_its_own_refusal()
    {
        var read = new FilePreviewResult(FilePreviewRefusal.None,
            new PreviewedFile("src/chunk.ts", 2, false, "a\n", false) { Branch = "feature/x" }, "feature/x");
        var answer = JsonSerializer.SerializeToElement(TreesRoutes.FileAnswer("s1a2b3c4", "src/chunk.ts", read), Camel);
        Assert.Equal("feature/x", answer.GetProperty("branch").GetString());

        var disk = JsonSerializer.SerializeToElement(
            TreesRoutes.FileAnswer("s1a2b3c4", "src/chunk.ts", new FilePreviewResult(FilePreviewRefusal.None, new PreviewedFile("src/chunk.ts", 2, false, "a\n", false))),
            Camel);
        Assert.Equal(JsonValueKind.Null, disk.GetProperty("branch").ValueKind);

        var error = Assert.Throws<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.FileAnswer("s1a2b3c4", "src", FilePreviewResult.Refused(FilePreviewRefusal.NotOnBranch, "feature/x")));
        Assert.Equal(Refusals.PreviewNotOnBranch, error.Code);
        Assert.Contains(error.Code, Refusals.All);
        Assert.Equal("src", error.Parameters!["path"]);
        Assert.Equal("feature/x", error.Parameters["branch"]);
    }

    /// <summary>A landed preview with no checkout here is the no-tree answer: neither the tree nor the branch is here, and git is asked nothing.</summary>
    [Fact]
    public async Task A_landed_preview_with_no_checkout_here_is_no_tree()
    {
        var error = await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => TreesRoutes.LandedPreviewAsync("s1a2b3c4", null, Landing(), null, "src/chunk.ts", CancellationToken.None));

        Assert.Equal(Refusals.PreviewNoTree, error.Code);
    }

    /// <summary>
    /// Why a review with no landing to fall back on has nothing to show, each its own code: no tree named here, a tree
    /// gone (REVIEW2: nothing to diff, and no tree to act on), no base recorded, or a range git cannot read.
    /// </summary>
    [Fact]
    public void Each_reason_a_review_has_nothing_to_show_is_its_own_code()
    {
        var tree = DaorisHome.Entry(Home, HomeEntry.Trees,"aurora", "engine", "s-1");

        Assert.Equal(Refusals.SessionNotReviewable, TreesRoutes.Unreviewable("s1a2b3c4", null, "abc1234").Code);
        Assert.Equal(Refusals.SessionTreeGone, TreesRoutes.Unreviewable("s1a2b3c4", tree, "abc1234").Code);
        Directory.CreateDirectory(tree);
        // What a removal leaves where something held the folder open (D109 §5): gone all the same.
        Assert.Equal(Refusals.SessionTreeGone, TreesRoutes.Unreviewable("s1a2b3c4", tree, "abc1234").Code);
        File.WriteAllText(Path.Combine(tree, ".git"), "gitdir: elsewhere\n");
        Assert.Equal(Refusals.SessionNoBase, TreesRoutes.Unreviewable("s1a2b3c4", tree, null).Code);
        Assert.Equal(Refusals.SessionRangeUnreadable, TreesRoutes.Unreviewable("s1a2b3c4", tree, "abc1234").Code);
        Assert.Contains(Refusals.SessionTreeGone, Refusals.All);
    }

    /// <summary>A plan or a press reads the session's record, so before the driver is up each is the cold-start sentence.</summary>
    [Fact]
    public async Task Landing_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LANDING", new { id = "s1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LAND_SESSION_TREE", new { id = "s1a2b3c4" }));
        // An entry's Accept and an ask's (ACCEPTALL1) read the records too.
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "ENTRY_LANDING", new { entry = "ask:a3/engine" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LAND_ENTRY", new { entry = "ask:a3/engine" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "ASK_LANDING", new { ask = "a3" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LAND_ASK", new { ask = "a3" }));
    }

    /// <summary>A hand-off (WSR5b) reads the registry's checkouts, so before the driver is up each route is the cold-start sentence.</summary>
    [Fact]
    public async Task A_hand_off_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HANDOFF_PLAN", new { id = "s1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HANDOFF", new { id = "s1a2b3c4", plugin = "example.lands" }));
    }

    /// <summary>Discard reads the session's record first, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task Discarding_a_tree_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "DISCARD_SESSION_TREE", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
    }

    /// <summary>
    /// The merge alone is retired (LEFT2): the page lands through `LAND_SESSION_TREE` since WSR1, which merges
    /// where the repository's rule says merge, and a door that merged whatever the rule said was a door past it
    /// (D87). Nothing called it, and the terminal never had its twin.
    /// </summary>
    [Fact]
    public async Task The_merge_alone_is_no_route()
    {
        Assert.DoesNotContain("MERGE_SESSION_TREE", DriverModule.Routes);
        Assert.Contains("NO_ROUTE", await RefusalAsync(Module(), "MERGE_SESSION_TREE", new { id = "s1a2b3c4" }));
    }
}
