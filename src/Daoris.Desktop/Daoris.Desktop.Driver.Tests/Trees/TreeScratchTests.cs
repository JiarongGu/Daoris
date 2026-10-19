using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEKEEP1 (D102's note): a session's own scratch in an ignored folder the checkout holds too is not removed with its tree
/// unasked. These hold the pure halves: the build list, the walk and its bound, and the words. The real-git rows are in
/// <c>ContentProofLossTests</c>.
/// </summary>
public sealed class TreeScratchTests : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-scratch", Guid.NewGuid().ToString("N")[..8]);
    private string Tree => Path.Combine(_scratch, "tree");
    private string Checkout => Path.Combine(_scratch, "checkout");

    public TreeScratchTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => GitSeed.Discard(_scratch);

    private void Write(string root, string relative, DateTime at, string content = "x\n")
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, at);
    }

    [Theory]
    [InlineData("node_modules/", true)]
    [InlineData("src/app/node_modules/", true)]
    [InlineData("dist/", true)]
    [InlineData("build/", true)]
    [InlineData("out/", true)]
    [InlineData(".angular/", true)]
    [InlineData(".next/", true)]
    [InlineData("coverage/", true)]
    [InlineData("target/", true)]
    [InlineData("__pycache__/", true)]
    [InlineData(".venv/", true)]
    [InlineData(".gradle/", true)]
    [InlineData("src/Daoris/bin/Debug/", true)]
    [InlineData("obj/", true)]
    [InlineData("devtools/.tmp/", false)]
    [InlineData("local/", false)]
    [InlineData("binary/", false)]
    public void The_build_list_matches_by_any_path_segment(string path, bool build) =>
        Assert.Equal(build, TreeScratch.UnderBuildFolder(path));

    [Fact]
    public void A_file_written_after_the_tree_opened_and_absent_from_the_checkout_is_the_trees_own()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Tree, "devtools/.tmp/requests.json", opened.AddHours(1));
        Write(Checkout, "devtools/.tmp/other.json", opened.AddDays(-30));

        var found = TreeScratch.WrittenSince(Tree, Checkout, "devtools/.tmp/", opened, out var tooLarge);

        Assert.False(tooLarge);
        Assert.Equal(["devtools/.tmp/requests.json"], found);
    }

    [Fact]
    public void A_file_not_written_since_the_opening_is_not_the_trees_own()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Tree, "devtools/.tmp/old.json", opened.AddHours(-1));

        Assert.Empty(TreeScratch.WrittenSince(Tree, Checkout, "devtools/.tmp/", opened, out _));
    }

    [Fact]
    public void A_file_the_checkout_holds_identically_is_not_the_trees_own()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Tree, "devtools/.tmp/cache.json", opened.AddHours(1));
        Write(Checkout, "devtools/.tmp/cache.json", opened.AddHours(-5));

        Assert.Empty(TreeScratch.WrittenSince(Tree, Checkout, "devtools/.tmp/", opened, out _));
    }

    [Theory]
    [InlineData("y\n")]
    [InlineData("a longer copy\n")]
    public void A_file_the_checkout_holds_differently_is_the_trees_own(string checkoutBytes)
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Tree, "devtools/.tmp/cache.json", opened.AddHours(1));
        Write(Checkout, "devtools/.tmp/cache.json", opened.AddHours(-5), checkoutBytes);

        var found = TreeScratch.WrittenSince(Tree, Checkout, "devtools/.tmp/", opened, out _);

        Assert.Equal(["devtools/.tmp/cache.json"], found);
    }

    [Fact]
    public void A_folder_past_the_walk_bound_is_too_large_to_look_through()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var folder = Path.Combine(Tree, "wide");
        Directory.CreateDirectory(folder);
        for (var n = 0; n <= TreeScratch.ScratchWalkLimit; n++) File.WriteAllBytes(Path.Combine(folder, $"f{n}"), []);

        TreeScratch.WrittenSince(Tree, Checkout, "wide/", opened, out var tooLarge);

        Assert.True(tooLarge);
    }

    [Fact]
    public void The_refusal_names_up_to_three_written_files_and_says_where_they_are_not()
    {
        var holds = new ContentHolds.TreeHolds([], [], [], ["a/1", "a/2", "a/3", "a/4"], []);

        Assert.Equal(
            "4 file(s) written in the tree after it opened, which your checkout does not hold as written: a/1, a/2, a/3 and 1 more",
            holds.WrittenSaid);
        Assert.Equal(holds.WrittenSaid, holds.OwnSaid);
    }

    [Fact]
    public void A_folder_too_large_to_look_through_says_so()
    {
        var holds = new ContentHolds.TreeHolds([], [], [], [], ["big/"]);

        Assert.Equal("1 ignored folder(s) too large to look through, which count as the tree's own: big/", holds.WrittenSaid);
    }

    [Fact]
    public void A_tree_holding_neither_says_nothing() =>
        Assert.Null(new ContentHolds.TreeHolds([], [], ["node_modules/"], [], []).OwnSaid);
}
