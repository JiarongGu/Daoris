using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELOCAL1 (D216): a new session tree carries the machine-local files its repository's gates read, copied from its checkout
/// read-only. These hold the declaration and the copy over a checkout and a tree that are plain folders, git's word on what it
/// ignores handed in as the open would hand it; the real-git rows (the open, its cut-off, the removal and the gate that reads the
/// copy) are in <see cref="SessionTreesTests"/>.
/// </summary>
public sealed class TreeLocalInputsTests : IDisposable
{
    private const string Patterns = "local/sensitive-patterns.txt";

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-local", Guid.NewGuid().ToString("N")[..8]);
    private string Checkout => Path.Combine(_scratch, "checkout");
    private string Tree => Path.Combine(_scratch, "tree");

    public TreeLocalInputsTests()
    {
        Directory.CreateDirectory(Checkout);
        Directory.CreateDirectory(Tree);
    }

    /// <remarks>Deleted never following a link, read-only files made writable first, as the look's purge deletes (TREESCRATCH2).</remarks>
    public void Dispose()
    {
        TreeLocalInputs.Copying.Value = null;
        TreeSetAside.DeleteNeverFollowing(_scratch);
    }

    private void Gates(string json) => Write(Checkout, "daoris.gates.json", json);

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private LocalCopy Reuse(Func<string, bool>? ignored = null) =>
        TreeLocalInputs.Reuse(Checkout, Tree, ignored ?? (_ => true), CancellationToken.None);

    private void AssertNothingIn(string folder) =>
        Assert.True(!Directory.Exists(folder) || Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length == 0,
            $"{folder} holds {string.Join(", ", Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories) : [])}");

    [Fact]
    public void The_declared_inputs_are_the_gates_file_s_local_inputs_and_its_sensitive_patterns_file()
    {
        Gates("""{ "localInputs": ["local/a.txt", "local\\b.txt", 7, "local/a.txt"], "sensitive": { "patternsFile": "local/sensitive-patterns.txt" } }""");

        Assert.Equal(
            [new LocalInput("local/a.txt", Declared: true), new LocalInput("local/b.txt", Declared: true), new LocalInput(Patterns, Declared: true)],
            TreeLocalInputs.Declared(Checkout));
    }

    [Fact]
    public void A_gates_file_naming_no_patterns_file_implies_the_devkit_s_default_and_none_or_an_unreadable_one_declares_nothing()
    {
        Assert.Empty(TreeLocalInputs.Declared(Checkout));

        Gates("{ }");
        Assert.Equal([new LocalInput(Patterns, Declared: false)], TreeLocalInputs.Declared(Checkout));

        // Named as a local input too, it is declared: missing, it is said.
        Gates("""{ "localInputs": ["local/sensitive-patterns.txt"] }""");
        Assert.Equal([new LocalInput(Patterns, Declared: true)], TreeLocalInputs.Declared(Checkout));

        Gates("{ \"gates\": [ ");
        Assert.Empty(TreeLocalInputs.Declared(Checkout));
        Gates("[]");
        Assert.Empty(TreeLocalInputs.Declared(Checkout));
    }

    [Fact]
    public void A_declared_file_is_copied_read_only_with_the_checkout_s_bytes_and_a_write_to_it_is_refused()
    {
        Gates("""{ "localInputs": ["local/notes/allowed-hosts.txt"], "sensitive": { "patternsFile": "local/sensitive-patterns.txt" } }""");
        Write(Checkout, Patterns, "quiltmark\n# a comment\n");
        Write(Checkout, "local/notes/allowed-hosts.txt", "build.example.test\n");

        var copy = Reuse();

        Assert.Equal(["local/notes/allowed-hosts.txt", Patterns], copy.Copied);
        Assert.Empty(copy.Declined);
        var copied = Path.Combine(Tree, "local", "sensitive-patterns.txt");
        Assert.Equal(File.ReadAllBytes(Path.Combine(Checkout, "local", "sensitive-patterns.txt")), File.ReadAllBytes(copied));
        Assert.True(File.GetAttributes(copied).HasFlag(FileAttributes.ReadOnly), "the copy is read-only");
        Assert.ThrowsAny<UnauthorizedAccessException>(() => File.WriteAllText(copied, "edited in the tree\n"));
        Assert.Equal("quiltmark\n# a comment\n", File.ReadAllText(Path.Combine(Checkout, "local", "sensitive-patterns.txt")));
        Assert.False(File.GetAttributes(Path.Combine(Checkout, "local", "sensitive-patterns.txt")).HasFlag(FileAttributes.ReadOnly),
            "the checkout's own file is left as it was");
        // Made under a name of its own and renamed into place: nothing of the copy under way is left.
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(Tree, "*", SearchOption.AllDirectories),
            entry => Path.GetFileName(entry).StartsWith(".daoris-", StringComparison.Ordinal));
        // Its agent is told what it is and to leave it.
        Assert.Contains("`local/notes/allowed-hosts.txt`, `local/sensitive-patterns.txt`", copy.Told);
        Assert.Contains("read-only", copy.Told);
    }

    [Fact]
    public void A_declared_file_the_checkout_lacks_is_said_missing_and_the_implied_default_says_nothing()
    {
        Gates("""{ "localInputs": ["local/allowed-hosts.txt"] }""");

        var copy = Reuse();

        Assert.Empty(copy.Copied);
        Assert.Equal([new LocalUncopied("local/allowed-hosts.txt", LocalUncopiedWhy.Missing)], copy.Declined);
        Assert.Null(copy.Told);
        AssertNothingIn(Path.Combine(Tree, "local"));

        Gates("""{ "sensitive": { "patternsFile": "local/private.txt" } }""");
        Assert.Equal([new LocalUncopied("local/private.txt", LocalUncopiedWhy.Missing)], Reuse().Declined);

        Gates("{}");
        Assert.Equal(LocalCopy.None, Reuse());
    }

    [Fact]
    public void A_path_outside_local_climbing_out_of_it_a_folder_or_one_git_does_not_ignore_is_refused_and_copies_nothing()
    {
        Gates("""
            { "localInputs": ["secrets/token.txt", "local/../token.txt", "local/./token.txt", "local//token.txt", "local/", "local/c:token.txt",
                "local/certs", "local/unignored.txt"] }
            """);
        Write(Checkout, "secrets/token.txt", "x\n");
        Write(Checkout, "token.txt", "x\n");
        Write(Checkout, "local/token.txt", "x\n");
        Write(Checkout, "local/certs/one.pem", "x\n");
        Write(Checkout, "local/unignored.txt", "x\n");

        var copy = Reuse(ignored: path => path != "local/unignored.txt");

        Assert.Empty(copy.Copied);
        Assert.Equal(
            ["secrets/token.txt", "local/../token.txt", "local/./token.txt", "local//token.txt", "local/", "local/c:token.txt", "local/certs", "local/unignored.txt"],
            copy.Declined.Select(each => each.Input));
        Assert.All(copy.Declined, each => Assert.Equal(LocalUncopiedWhy.Refused, each.Why));
        AssertNothingIn(Tree);
    }

    /// <summary>A link on the way to where the copy goes would carry it out of the tree: the copy never writes through one.</summary>
    [Fact]
    public void A_link_on_the_way_into_the_tree_refuses_the_copy_and_writes_nothing_where_it_leads()
    {
        Gates("{}");
        Write(Checkout, Patterns, "quiltmark\n");
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        TreeDependencies.Link(Path.Combine(Tree, "local"), elsewhere);

        var copy = Reuse();

        Assert.Equal([new LocalUncopied(Patterns, LocalUncopiedWhy.Refused)], copy.Declined);
        AssertNothingIn(elsewhere);
    }

    [Fact]
    public void A_copy_that_fails_says_why_in_the_program_s_words_and_leaves_nothing_half_made()
    {
        Gates("{}");
        Write(Checkout, Patterns, "quiltmark\n");
        // A file where the copy's folder goes: the folder cannot be made.
        Write(Tree, "local", "a tracked file named local\n");

        var copy = Reuse();

        var failed = Assert.Single(copy.Declined);
        Assert.Equal((Patterns, LocalUncopiedWhy.Failed), (failed.Input, failed.Why));
        Assert.False(string.IsNullOrWhiteSpace(failed.Detail));
        Assert.Equal(["local"], Directory.GetFileSystemEntries(Tree).Select(Path.GetFileName));
    }

    /// <summary>Cut off at its second file, it takes back its first: an open cut off holds none of them.</summary>
    [Fact]
    public void A_cancel_while_it_copies_leaves_no_copy()
    {
        Gates("""{ "localInputs": ["local/a.txt"] }""");
        Write(Checkout, "local/a.txt", "a\n");
        Write(Checkout, Patterns, "quiltmark\n");
        using var closing = new CancellationTokenSource();
        TreeLocalInputs.Copying.Value = from =>
        {
            if (from.EndsWith("sensitive-patterns.txt", StringComparison.Ordinal)) closing.Cancel();
        };

        Assert.ThrowsAny<OperationCanceledException>(() => TreeLocalInputs.Reuse(Checkout, Tree, _ => true, closing.Token));

        AssertNothingIn(Tree);
    }
}
