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

    // ---- TIDYOUTPUT1: a repository's declared output, and a build's own folders ----

    [Theory]
    [InlineData("bin/", true)]
    [InlineData("src/App/obj/Debug/", true)]
    [InlineData("tools/node_modules/", true)]
    [InlineData("devtools/_samples/bin/Debug/Samples.dll", true)]
    [InlineData("dist/", false)]
    [InlineData("out/", false)]
    [InlineData("build/", false)]
    [InlineData("local/", false)]
    [InlineData("binary/", false)]
    [InlineData("devtools/_exports/objects.csv", false)]
    public void A_builds_own_folders_are_only_the_three_a_tool_names_itself(string path, bool made) =>
        Assert.Equal(made, TreeScratch.UnderMadeFolder(path));

    [Theory]
    [InlineData("devtools/_test-results/", true)]
    [InlineData("devtools/_test-results/run.xml", true)]
    [InlineData("devtools/_e2e-p1-data/lyntai.db", true)]
    [InlineData("devtools/_e2e-p2-data/", true)]
    [InlineData("devtools/_samples/Samples.csproj", true)]
    [InlineData("devtools/_e2e-data/", false)]
    [InlineData("devtools/", false)]
    [InlineData("devtools/_test-results-old/run.xml", false)]
    [InlineData("local/note.md", false)]
    [InlineData("DEVTOOLS/_TEST-RESULTS/run.xml", true)]
    public void A_declared_glob_covers_a_path_or_a_folder_above_it(string path, bool covered)
    {
        var declared = TreeScratch.ParseDeclared(
            """{ "integration": { "scratch": ["devtools/_test-results/**", "devtools/_e2e-*-data/**", "devtools/_samples/Samples.csproj"] } }""");

        Assert.Equal(covered, TreeScratch.IsDeclared(declared, path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "integration": [] }""")]
    [InlineData("""{ "integration": { "scratch": "local/**" } }""")]
    [InlineData("""{ "integration": { "scratch": [1, null, "", "  "] } }""")]
    [InlineData("""{ "scratch": ["devtools/**"] }""")]
    public void A_declaration_that_is_absent_or_malformed_declares_nothing(string text) =>
        Assert.Empty(TreeScratch.ParseDeclared(text));

    [Theory]
    [InlineData("**")]
    [InlineData("**/*.db")]
    [InlineData("*")]
    [InlineData("../elsewhere/**")]
    [InlineData("devtools/../../elsewhere/**")]
    [InlineData("/devtools/**")]
    [InlineData("C:/devtools/**")]
    [InlineData("C:\\devtools")]
    public void A_glob_that_reaches_every_folder_or_leaves_the_repository_is_passed_over(string glob) =>
        Assert.Empty(TreeScratch.ParseDeclared($$"""{ "integration": { "scratch": ["{{glob.Replace("\\", "\\\\")}}"] } }"""));

    /// <summary>
    /// INTEGRATE1d: the scratch is read through the declaration's one reader, so a declaration that breaks another key's shape declares
    /// no scratch either: nothing acts on half of one. Which text is read, the accepted blob at the line's tip and never the checkout's
    /// working file, is <c>ContentProofLossTests</c>' real-git rows.
    /// </summary>
    [Theory]
    [InlineData("""{ "integration": { "by": "someone", "scratch": ["out-data/**"] } }""")]
    [InlineData("""{ "integration": { "scratch": ["out-data/**", "**"] } }""")]
    public void A_declaration_that_breaks_a_keys_shape_declares_no_scratch(string text) =>
        Assert.Empty(TreeScratch.ParseDeclared(text));

    [Fact]
    public void A_declaration_that_reads_declares_its_scratch() =>
        Assert.True(TreeScratch.IsDeclared(TreeScratch.ParseDeclared("""{ "integration": { "scratch": ["out-data/**"] } }"""), "out-data/x.db"));

    [Fact]
    public void A_build_folder_and_a_declared_file_in_a_shared_folder_are_not_the_trees_own()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Tree, "devtools/_samples/bin/Debug/Samples.dll", opened.AddHours(1));
        Write(Tree, "devtools/_samples/obj/project.assets.json", opened.AddHours(1));
        Write(Tree, "devtools/_samples/Samples.csproj", opened.AddHours(1));
        Write(Tree, "devtools/_samples/export.csv", opened.AddHours(1));
        var declared = TreeScratch.ParseDeclared("""{ "integration": { "scratch": ["devtools/_samples/Samples.csproj"] } }""");

        var found = TreeScratch.WrittenSince(Tree, Checkout, "devtools/_samples/", opened, out _, declared);

        Assert.Equal(["devtools/_samples/export.csv"], found);
    }

    [Fact]
    public void A_folder_the_checkout_lacks_holds_as_its_own_what_is_neither_declared_nor_a_builds()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Tree, "devtools/_run/bin/Debug/App.dll", opened.AddHours(1));
        Write(Tree, "devtools/_run/results.xml", opened.AddHours(1));
        Write(Tree, "devtools/_run/notes.md", opened.AddHours(-1));
        var declared = TreeScratch.ParseDeclared("""{ "integration": { "scratch": ["devtools/_run/results.xml"] } }""");

        var found = TreeScratch.OwnFilesIn(Tree, "devtools/_run/", declared, out var tooLarge);

        Assert.False(tooLarge);
        Assert.Equal(["devtools/_run/notes.md"], found);
    }

    [Fact]
    public void A_folder_the_checkout_lacks_holds_nothing_of_its_own_where_all_of_it_is_output()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Tree, "src/App/obj/a.json", opened.AddHours(1));
        Write(Tree, "src/App/bin/a.dll", opened.AddHours(1));

        Assert.Empty(TreeScratch.OwnFilesIn(Tree, "src/App/", [], out var tooLarge));
        Assert.False(tooLarge);
    }

    [Fact]
    public void A_wide_folder_the_checkout_lacks_is_too_large_to_look_through()
    {
        var folder = Path.Combine(Tree, "wide");
        Directory.CreateDirectory(folder);
        for (var n = 0; n <= TreeScratch.ScratchWalkLimit; n++) File.WriteAllBytes(Path.Combine(folder, $"f{n}"), []);

        TreeScratch.OwnFilesIn(Tree, "wide/", [], out var tooLarge);

        Assert.True(tooLarge);
    }

    [Fact]
    public void A_read_only_copy_with_the_checkouts_bytes_is_not_the_trees_own()
    {
        var opened = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Write(Checkout, "local/sensitive-patterns.txt", opened.AddDays(-9), "pattern\n");
        Write(Tree, "local/sensitive-patterns.txt", opened.AddHours(1), "pattern\n");
        File.SetAttributes(Path.Combine(Tree, "local", "sensitive-patterns.txt"), FileAttributes.ReadOnly);
        Write(Tree, "local/note.md", opened.AddHours(1), "a session's note\n");

        var found = TreeScratch.WrittenSince(Tree, Checkout, "local/", opened, out _);

        Assert.Equal(["local/note.md"], found);
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
