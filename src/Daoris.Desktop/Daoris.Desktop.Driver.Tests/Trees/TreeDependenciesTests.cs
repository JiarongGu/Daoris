using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEDEPS1 (D51's note): a new session tree starts with the dependencies its checkout installed at the same lockfile, copied in,
/// never linked. These hold the gate and the copy over a checkout and a tree that are plain folders, git's facts handed in as the
/// open would hand them; the real-git rows (the open, its cut-off and the removal) are in <see cref="SessionTreesTests"/>.
/// </summary>
public sealed class TreeDependenciesTests : IDisposable
{
    private const string Lock = "{\n  \"name\": \"engine\",\n  \"lockfileVersion\": 3,\n  \"packages\": {\n    \"\": { \"name\": \"engine\" },\n"
        + "    \"node_modules/left-pad\": { \"version\": \"1.3.0\" }\n  }\n}\n";

    private const string WorkspaceLock = "{\n  \"name\": \"engine\",\n  \"lockfileVersion\": 3,\n  \"packages\": {\n"
        + "    \"\": { \"name\": \"engine\", \"workspaces\": [\"packages/ui\"] },\n"
        + "    \"node_modules/ui\": { \"resolved\": \"packages/ui\", \"link\": true },\n"
        + "    \"packages/ui\": { \"name\": \"ui\", \"version\": \"1.0.0\" },\n"
        + "    \"packages/ui/node_modules/dep\": { \"version\": \"2.0.0\" }\n  }\n}\n";

    private static readonly DateTime Locked = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-deps", Guid.NewGuid().ToString("N")[..8]);
    private string Checkout => Path.Combine(_scratch, "checkout");
    private string Tree => Path.Combine(_scratch, "tree");

    public TreeDependenciesTests()
    {
        Directory.CreateDirectory(Checkout);
        Directory.CreateDirectory(Tree);
    }

    /// <remarks>
    /// Deleted never following a link, as the look's purge deletes: .NET's recursive delete throws on a junction on Windows (it
    /// asks Windows to unmount it as a volume first), though it removes it.
    /// </remarks>
    public void Dispose()
    {
        TreeDependencies.Copying.Value = null;
        TreeSetAside.DeleteNeverFollowing(_scratch);
    }

    /// <summary>A file in both the checkout and the tree, as git would check it out in each.</summary>
    private void Tracked(string relative, string content)
    {
        Write(Checkout, relative, content);
        Write(Tree, relative, content);
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>The checkout's install of <paramref name="folder"/>'s project: a package each, and npm's hidden lockfile, written after the lockfile.</summary>
    private void Install(string folder, params string[] packages)
    {
        var modules = Path.Combine(Checkout, folder, "node_modules");
        foreach (var package in packages) Write(modules, Path.Combine(package, "index.js"), $"module.exports = '{package}';\n");
        File.SetLastWriteTimeUtc(Path.Combine(Checkout, folder, "package-lock.json"), Locked);
        Write(modules, ".package-lock.json", "{}\n");
        File.SetLastWriteTimeUtc(Path.Combine(modules, ".package-lock.json"), Locked.AddMinutes(5));
    }

    private DependencyCopy Reuse(params string[] lockfiles) =>
        TreeDependencies.Reuse(Checkout, Tree, lockfiles, ignored: _ => true, CancellationToken.None);

    /// <summary>Where a link leads, read off the link and never through it.</summary>
    private static string Resolved(string link) =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(link)!, new DirectoryInfo(link).LinkTarget!));

    /// <summary>Only what the tree holds itself: no folder the copy was made under is left beside what it made.</summary>
    private void AssertNoCopyUnderWay() =>
        Assert.DoesNotContain(Directory.EnumerateDirectories(Tree, "*", SearchOption.AllDirectories), folder => Path.GetFileName(folder).StartsWith(".daoris-", StringComparison.Ordinal));

    [Fact]
    public void A_matching_lockfile_copies_the_checkout_s_dependencies_and_a_write_in_the_tree_leaves_the_checkout_s_alone()
    {
        Tracked("package-lock.json", Lock);
        Install(".", "left-pad");
        Write(Checkout, "node_modules/.vite/deps/_metadata.json", "{\"root\":\"the checkout\"}\n");
        Write(Checkout, "node_modules/.cache/babel/x.json", "{}\n");

        var copy = Reuse("package-lock.json");

        Assert.Equal(["node_modules"], copy.Copied);
        Assert.Empty(copy.Declined);
        var copied = Path.Combine(Tree, "node_modules", "left-pad", "index.js");
        Assert.Equal("module.exports = 'left-pad';\n", File.ReadAllText(copied));
        // A cache names where it was made: never copied, since the tree is somewhere else.
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules", ".vite")));
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules", ".cache")));
        // npm trusts its hidden lockfile only where no folder of the install is newer than it.
        var hidden = File.GetLastWriteTimeUtc(Path.Combine(Tree, "node_modules", ".package-lock.json"));
        Assert.True(hidden >= Directory.GetLastWriteTimeUtc(Path.Combine(Tree, "node_modules", "left-pad")), "the copy's hidden lockfile is older than its folders");
        AssertNoCopyUnderWay();

        File.WriteAllText(copied, "module.exports = 'changed in the tree';\n");
        Assert.Equal("module.exports = 'left-pad';\n", File.ReadAllText(Path.Combine(Checkout, "node_modules", "left-pad", "index.js")));
    }

    [Fact]
    public void A_lockfile_that_moved_copies_nothing_and_says_so()
    {
        Write(Checkout, "package-lock.json", Lock);
        Write(Tree, "package-lock.json", Lock.Replace("1.3.0", "1.4.0", StringComparison.Ordinal));
        Install(".", "left-pad");

        var copy = Reuse("package-lock.json");

        Assert.Empty(copy.Copied);
        Assert.Equal([new Uncopied("node_modules", UncopiedWhy.Moved)], copy.Declined);
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
        Assert.Null(copy.Told);
    }

    [Theory]
    [InlineData("pnpm-lock.yaml")]
    [InlineData("yarn.lock")]
    public void A_project_another_manager_installs_copies_nothing(string lockfile)
    {
        Tracked("package-lock.json", Lock);
        Tracked(lockfile, "lockfileVersion: 9\n");
        Install(".", "left-pad");

        var copy = Reuse("package-lock.json", lockfile);

        Assert.Empty(copy.Copied);
        Assert.Equal([new Uncopied("node_modules", UncopiedWhy.Manager)], copy.Declined);
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
    }

    [Fact]
    public void A_repository_with_no_npm_lockfile_copies_nothing_and_says_why_only_where_another_manager_installs()
    {
        Tracked("pnpm-lock.yaml", "lockfileVersion: 9\n");
        Write(Checkout, "node_modules/left-pad/index.js", "module.exports = 'left-pad';\n");

        Assert.Equal([new Uncopied("node_modules", UncopiedWhy.Manager)], Reuse("pnpm-lock.yaml").Declined);
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
        // A repository with no Node project has nothing to copy and nothing to say.
        Assert.Equal(DependencyCopy.None, Reuse());
    }

    [Theory]
    [InlineData("none installed")]
    [InlineData("no hidden lockfile")]
    [InlineData("installed before the lockfile changed")]
    public void A_checkout_with_no_install_at_its_lockfile_copies_nothing(string how)
    {
        Tracked("package-lock.json", Lock);
        if (how != "none installed")
        {
            Install(".", "left-pad");
            var hidden = Path.Combine(Checkout, "node_modules", ".package-lock.json");
            if (how == "no hidden lockfile") File.Delete(hidden);
            else File.SetLastWriteTimeUtc(hidden, Locked.AddMinutes(-5));
        }

        var copy = Reuse("package-lock.json");

        Assert.Equal([new Uncopied("node_modules", UncopiedWhy.Uninstalled)], copy.Declined);
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
    }

    [Fact]
    public void A_folder_git_does_not_ignore_is_never_copied_since_it_would_read_as_the_tree_s_work()
    {
        Tracked("package-lock.json", Lock);
        Install(".", "left-pad");

        var copy = TreeDependencies.Reuse(Checkout, Tree, ["package-lock.json"], ignored: _ => false, CancellationToken.None);

        Assert.Equal([new Uncopied("node_modules", UncopiedWhy.Unignored)], copy.Declined);
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
    }

    [Fact]
    public void Each_project_is_judged_on_its_own()
    {
        Tracked("package-lock.json", Lock);
        Install(".", "left-pad");
        Write(Checkout, "web/package-lock.json", Lock);
        Write(Tree, "web/package-lock.json", Lock.Replace("1.3.0", "1.4.0", StringComparison.Ordinal));
        Install("web", "left-pad");

        var copy = Reuse("package-lock.json", "web/package-lock.json");

        Assert.Equal(["node_modules"], copy.Copied);
        Assert.Equal([new Uncopied("web/node_modules", UncopiedWhy.Moved)], copy.Declined);
        Assert.True(File.Exists(Path.Combine(Tree, "node_modules", "left-pad", "index.js")));
        Assert.False(Directory.Exists(Path.Combine(Tree, "web", "node_modules")));
    }

    /// <summary>
    /// npm links a workspace into <c>node_modules</c>, on Windows as a junction to its absolute path in the checkout. Copied as it
    /// stands it would lead the tree into the checkout; the copy's link leads to the tree's own workspace, and the workspace's own
    /// <c>node_modules</c> is copied too.
    /// </summary>
    [Fact]
    public void A_workspace_link_is_copied_as_a_link_to_the_tree_s_own_workspace_never_the_checkout_s()
    {
        Tracked("package-lock.json", WorkspaceLock);
        Tracked("packages/ui/index.js", "module.exports = 'ui';\n");
        Install(".", "left-pad");
        Write(Checkout, "packages/ui/node_modules/dep/index.js", "module.exports = 'dep';\n");
        TreeDependencies.Link(Path.Combine(Checkout, "node_modules", "ui"), Path.Combine(Checkout, "packages", "ui"));

        var copy = Reuse("package-lock.json");

        Assert.Equal(["node_modules", "packages/ui/node_modules"], copy.Copied);
        var link = Path.Combine(Tree, "node_modules", "ui");
        Assert.True(new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint), "the workspace is copied as a link");
        Assert.Equal(Path.Combine(Tree, "packages", "ui"), Resolved(link), ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal("module.exports = 'dep';\n", File.ReadAllText(Path.Combine(Tree, "packages", "ui", "node_modules", "dep", "index.js")));

        File.WriteAllText(Path.Combine(link, "index.js"), "module.exports = 'changed in the tree';\n");
        Assert.Equal("module.exports = 'changed in the tree';\n", File.ReadAllText(Path.Combine(Tree, "packages", "ui", "index.js")));
        Assert.Equal("module.exports = 'ui';\n", File.ReadAllText(Path.Combine(Checkout, "packages", "ui", "index.js")));
    }

    [Fact]
    public void A_workspace_whose_own_lockfile_moved_copies_nothing_of_its_project()
    {
        Tracked("package-lock.json", WorkspaceLock);
        Tracked("packages/ui/index.js", "module.exports = 'ui';\n");
        Write(Checkout, "packages/ui/package-lock.json", Lock);
        Write(Tree, "packages/ui/package-lock.json", Lock.Replace("1.3.0", "1.4.0", StringComparison.Ordinal));
        Install(".", "left-pad");
        TreeDependencies.Link(Path.Combine(Checkout, "node_modules", "ui"), Path.Combine(Checkout, "packages", "ui"));

        var copy = Reuse("package-lock.json", "packages/ui/package-lock.json");

        Assert.Empty(copy.Copied);
        Assert.Equal([new Uncopied("node_modules", UncopiedWhy.Workspace, Workspace: "packages/ui")], copy.Declined);
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
    }

    [Fact]
    public void A_link_that_leads_outside_the_checkout_copies_nothing_and_leaves_no_half_copy()
    {
        Tracked("package-lock.json", Lock);
        Install(".", "left-pad", "zebra");
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Write(elsewhere, "index.js", "module.exports = 'linked';\n");
        TreeDependencies.Link(Path.Combine(Checkout, "node_modules", "linked"), elsewhere);

        var copy = Reuse("package-lock.json");

        Assert.Equal([new Uncopied("node_modules", UncopiedWhy.Link)], copy.Declined);
        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
        AssertNoCopyUnderWay();
        Assert.Equal("module.exports = 'linked';\n", File.ReadAllText(Path.Combine(elsewhere, "index.js")));
    }

    [Fact]
    public void A_copy_cut_off_leaves_no_half_copy_and_the_checkout_as_it_was()
    {
        Tracked("package-lock.json", WorkspaceLock);
        Tracked("packages/ui/index.js", "module.exports = 'ui';\n");
        Install(".", "a", "b", "c", "d", "e", "f");
        TreeDependencies.Link(Path.Combine(Checkout, "node_modules", "ui"), Path.Combine(Checkout, "packages", "ui"));
        using var closing = new CancellationTokenSource();
        var seen = 0;
        TreeDependencies.Copying.Value = _ =>
        {
            if (Interlocked.Increment(ref seen) == 2) closing.Cancel();
        };

        Assert.ThrowsAny<OperationCanceledException>(
            () => TreeDependencies.Reuse(Checkout, Tree, ["package-lock.json"], ignored: _ => true, closing.Token));

        Assert.False(Directory.Exists(Path.Combine(Tree, "node_modules")));
        AssertNoCopyUnderWay();
        Assert.Equal("module.exports = 'ui';\n", File.ReadAllText(Path.Combine(Checkout, "packages", "ui", "index.js")));
        Assert.Equal(6, Directory.GetDirectories(Path.Combine(Checkout, "node_modules")).Length - 1);
    }

    /// <summary>
    /// The look's purge deletes a tree moved aside never following a link (TIDIEDLINK1): a copied workspace link goes as a link, and
    /// the checkout's workspace, and its own link to it, stand.
    /// </summary>
    [Fact]
    public void A_tree_holding_a_copied_workspace_link_is_deleted_without_reaching_what_the_checkout_s_link_leads_to()
    {
        Tracked("package-lock.json", WorkspaceLock);
        Tracked("packages/ui/index.js", "module.exports = 'ui';\n");
        Install(".", "left-pad");
        TreeDependencies.Link(Path.Combine(Checkout, "node_modules", "ui"), Path.Combine(Checkout, "packages", "ui"));
        Assert.Equal(["node_modules"], Reuse("package-lock.json").Copied);

        TreeSetAside.DeleteNeverFollowing(Tree);

        Assert.False(Directory.Exists(Tree));
        Assert.Equal("module.exports = 'ui';\n", File.ReadAllText(Path.Combine(Checkout, "packages", "ui", "index.js")));
        Assert.Equal("module.exports = 'ui';\n", File.ReadAllText(Path.Combine(Checkout, "node_modules", "ui", "index.js")));
    }

    [Fact]
    public void A_link_made_here_is_one_a_reader_finds_as_a_link_to_its_target()
    {
        var target = Path.Combine(_scratch, "target");
        Write(target, "index.js", "x\n");
        var link = Path.Combine(_scratch, "link");

        TreeDependencies.Link(link, target);

        Assert.True(new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal(target, Resolved(link), ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal("x\n", File.ReadAllText(Path.Combine(link, "index.js")));
    }

    /// <summary>The session's agent is told what was copied, and to skip the <c>npm ci</c> that would wipe it; nothing where nothing was.</summary>
    [Fact]
    public void The_agent_is_told_what_was_copied_and_to_skip_npm_ci_before_how_to_take_the_quest()
    {
        var copy = new DependencyCopy(["node_modules", "web/node_modules"], [new Uncopied("tools/node_modules", UncopiedWhy.Moved)]);

        Assert.Equal(
            "Daoris copied this tree's installed dependencies from the repository's checkout, whose lockfile matches this tree's: "
            + "`node_modules`, `web/node_modules`. They are ready to use: do not run `npm ci` where they are, which deletes them and "
            + "installs everything again, unless that folder's `package-lock.json` changes.",
            copy.Told);
        Assert.Null(new DependencyCopy([], [new Uncopied("node_modules", UncopiedWhy.Moved)]).Told);

        var target = TargetPromptGoldenTests.Bare with { Dependencies = copy.Told };
        var text = TargetPrompt.Compose(target);
        Assert.Contains($"\n{copy.Told}\n", text);
        Assert.True(text.IndexOf(copy.Told!, StringComparison.Ordinal) < text.IndexOf("First take the quest", StringComparison.Ordinal));
        var close = Assert.Single(TargetPrompt.Composed(target).Account.Sections, section => section.Name == HandedSections.Close);
        Assert.Equal(HandedSources.DriverWords, close.Source);
        Assert.Equal(text.Length, TargetPrompt.Composed(target).Account.Sections.Sum(section => section.Chars));
        Assert.Equal(TargetPrompt.Compose(TargetPromptGoldenTests.Bare), TargetPrompt.Compose(TargetPromptGoldenTests.Bare with { Dependencies = null }));
    }
}
