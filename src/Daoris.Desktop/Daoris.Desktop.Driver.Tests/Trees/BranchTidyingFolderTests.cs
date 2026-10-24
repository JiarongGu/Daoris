using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY1 to AUTOTIDY2c over real git, the folders: a tree's folder moved aside whole and purged after its wait, one something
/// holds open or a link reaches, the folders under the trees git knows no tree in, a tree whose opening a kill cut off, and a
/// removal's ends. Split from <see cref="BranchTidyingProcessTests"/> (PROC1g).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class BranchTidyingFolderTests : BranchTidyingFixture
{
    /// <summary>
    /// A tree reached through a link under the trees home is somewhere else: here its repository's folder was moved away after
    /// the tree was opened, and a link left in its place, so git still lists the tree under the trees home. The look keeps it,
    /// saying so, and its folder stays where the link points.
    /// </summary>
    [Fact]
    public async Task A_tree_reached_through_a_link_under_the_trees_home_stays()
    {
        var root = await RepositoryAsync("engine");
        var linked = await LandedTreeAsync(new SessionTrees(_home), root);
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        Directory.Move(DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine"), Path.Combine(elsewhere, "engine"));
        await JunctionAsync(DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine"), Path.Combine(elsewhere, "engine"));
        _ledger.Register("engine", root);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Contains(linked.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(elsewhere, "engine", Path.GetFileName(linked.Path))));
        Assert.Equal([$"tidy  engine: `{linked.Branch}` stays, since its tree is reached through a link under the trees home."], said);
    }

    /// <summary>
    /// A session names its tree by the path its start opened, through the link, where git lists where the tree really is: it
    /// holds the tree all the same, since the busy match compares resolved paths, at the press's list and the look's alike.
    /// </summary>
    [Fact]
    public async Task A_session_naming_its_tree_through_a_link_holds_it()
    {
        var root = await RepositoryAsync("engine");
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        await JunctionAsync(DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine"), elsewhere);
        var trees = new SessionTrees(_home);
        var linked = await LandedTreeAsync(trees, root);

        var plan = await trees.SweepPlanAsync([("engine", "default", root)], new HashSet<string> { linked.Path });

        Assert.Equal(SweepKind.InUse, plan.Single(item => item.Branch == linked.Branch).Kind);
    }

    /// <summary>
    /// 🔴 The look never deletes a tree's folder: it moves it aside whole, so a write that lands after the last look is kept.
    /// Here everything the tree held is in the folder moved aside, and a file written there afterwards stays with it.
    /// </summary>
    [Fact]
    public async Task A_tree_moved_aside_keeps_everything_and_what_is_written_after()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        var held = Directory.GetFiles(landed.Path, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(landed.Path, file)).ToList();
        _ledger.Register("engine", root);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        var aside = Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine")));
        Assert.Equal([$"{Removed(landed)}{aside}, and goes once it is left untouched for 14 days."], said);
        Assert.False(Directory.Exists(landed.Path));
        Assert.All(held, file => Assert.True(File.Exists(Path.Combine(aside, file)), file));
        await File.WriteAllTextAsync(Path.Combine(aside, "late.db"), "a write that came after the last look\n");
        Assert.True(File.Exists(Path.Combine(aside, "late.db")));
        Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 On Windows a folder whose files something holds open cannot be renamed: the tree is in use, so the branch, its tree and
    /// git's record of it all stay, the look says so once, and a later look takes it once nothing holds it. Elsewhere the rename
    /// goes, and what the holder reads or writes is in the folder moved aside.
    /// </summary>
    [Fact]
    public async Task A_tree_something_holds_open_stays_until_nothing_does()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        IReadOnlyList<string> said;
        IReadOnlyList<string> again;
        using (new FileStream(Path.Combine(landed.Path, "README.md"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            said = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
            again = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.StartsWith(Removed(landed), Assert.Single(said), StringComparison.Ordinal);
            return;
        }

        Assert.StartsWith(
            $"tidy  engine: `{landed.Branch}` stays, since something on this machine is using its tree, so its folder could not be moved aside",
            Assert.Single(said), StringComparison.Ordinal);
        Assert.Empty(again);
        Assert.Contains(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(landed.Path, "README.md")));
        Assert.Contains(Path.GetFileName(landed.Path), await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);
        Assert.Equal(TidyKept.Busy, Assert.Single(Events("branch.kept")).GetProperty("why").GetString());

        var free = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(landed), Assert.Single(free), StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTOTIDY2a: a branch on the line by content keeps its commits on a ref of their own before its tree's folder moves; where
    /// something holds the folder open the branch stays, and that ref is let go with the attempt, as the press's refused removal
    /// lets it go, so no ref is left for a branch that still stands. Once nothing holds the folder, the look takes it, kept.
    /// </summary>
    [Fact]
    public async Task A_content_held_tree_something_holds_open_leaves_no_ref_of_its_commits()
    {
        var root = await RepositoryAsync("engine");
        var squashed = await TreeWithWorkAsync(new SessionTrees(_home), root);
        await GitAsync(root, "merge", "--squash", squashed.Branch);
        await GitAsync(root, "commit", "--quiet", "-m", "the work, squashed");
        _ledger.Register("engine", root);
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        var removed = $"tidy  engine: removed `{squashed.Branch}`, since its work is on `main` by content (a squash merge); its tree's folder was moved aside to ";

        IReadOnlyList<string> said;
        using (new FileStream(Path.Combine(squashed.Path, "README.md"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            said = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.StartsWith(removed, Assert.Single(said), StringComparison.Ordinal);
            return;
        }

        Assert.StartsWith($"tidy  engine: `{squashed.Branch}` stays, since something on this machine is using its tree", Assert.Single(said), StringComparison.Ordinal);
        Assert.Contains(squashed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Empty((await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/")).Trim());

        var free = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(removed, Assert.Single(free), StringComparison.Ordinal);
        Assert.Contains(squashed.Branch, await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A folder moved aside goes once it has waited fourteen days with nothing written in it since its move; one written in after
    /// its move stays, said once, for the person to look at; and none goes before its wait.
    /// </summary>
    [Fact]
    public async Task The_purge_deletes_a_folder_left_untouched_and_keeps_one_written_after_its_move()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var untouched = await LandedTreeAsync(trees, root);
        var touched = await LandedTreeAsync(trees, root);
        _ledger.Register("engine", root);
        await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine"));
        var quiet = asides.Single(folder => Path.GetFileName(folder).StartsWith(Path.GetFileName(untouched.Path) + "-", StringComparison.Ordinal));
        var written = asides.Single(folder => Path.GetFileName(folder).StartsWith(Path.GetFileName(touched.Path) + "-", StringComparison.Ordinal));
        await File.WriteAllTextAsync(Path.Combine(written, "late.db"), "a write that came after the move\n");
        using var log = Log();

        // KEEPSIZE1b: their records kept at the first look's Keeping pass, finished trees' folders go an hour after, not at their days.
        var early = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow.AddMinutes(30) }
            .LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        var later = new BranchTidying(_home) { Pace = TimeSpan.Zero, Clock = () => DateTimeOffset.UtcNow.AddHours(2) };
        var said = await later.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        var again = await later.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Empty(early);
        Assert.Equal(2, said.Count);
        Assert.Contains(said, line => line.StartsWith($"tidy  deleted the folder moved aside to {quiet} on ", StringComparison.Ordinal));
        Assert.Contains(said, line => line.StartsWith($"tidy  the folder moved aside to {written} on ", StringComparison.Ordinal)
                                      && line.Contains("stays, since something in it was written after it was moved aside: late.db.", StringComparison.Ordinal));
        Assert.Empty(again);
        Assert.False(Directory.Exists(quiet));
        Assert.True(File.Exists(Path.Combine(written, "late.db")));
        Assert.Single(Events("tidied.purged"));
        Assert.Equal(SetAsideKept.Written, Assert.Single(Events("tidied.kept")).GetProperty("why").GetString());
    }

    /// <summary>
    /// 🔴 TIDIEDLINK1, D88's note: a folder moved aside whose build folder holds links, as npm's workspaces and pnpm leave under
    /// <c>node_modules</c>, is deleted after its wait with each link taken as a link: what one points at outside the folder stays,
    /// every file in it as it was, and one pointing inside goes with the folder. A link anywhere else keeps its folder, said as
    /// before, and what it points at is untouched.
    /// </summary>
    [Fact]
    public async Task The_purge_takes_a_link_under_a_build_folder_as_a_link_and_keeps_a_folder_with_one_elsewhere()
    {
        var engine = DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine");
        var moved = DateTimeOffset.UtcNow.AddMinutes(1);
        var stamp = moved.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var outside = Path.Combine(_scratch, "elsewhere", "web");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "index.js"), "module.exports = 'the target';\n");
        File.SetAttributes(Path.Combine(outside, "index.js"), FileAttributes.ReadOnly);
        var built = Path.Combine(engine, $"s-0b0117a0-{stamp}");
        Directory.CreateDirectory(Path.Combine(built, "packages", "ui"));
        await File.WriteAllTextAsync(Path.Combine(built, "packages", "ui", "index.js"), "export {};\n");
        await File.WriteAllTextAsync(Path.Combine(built, "README.md"), "# engine\n");
        // A read-only file, as git's objects are: cleared before its delete, as before.
        File.SetAttributes(Path.Combine(built, "README.md"), FileAttributes.ReadOnly);
        await JunctionAsync(Path.Combine(built, "node_modules", "@fixture", "web"), outside);
        await JunctionAsync(Path.Combine(built, "node_modules", "ui"), Path.Combine(built, "packages", "ui"));
        var data = Path.Combine(_scratch, "elsewhere", "data");
        Directory.CreateDirectory(data);
        await File.WriteAllTextAsync(Path.Combine(data, "notes.md"), "the person's\n");
        var linked = Path.Combine(engine, $"s-0d47a000-{stamp}");
        Directory.CreateDirectory(linked);
        await JunctionAsync(Path.Combine(linked, "data"), data);

        var purged = new SessionTrees(_home).PurgeSetAside(moved.AddDays(15), KeepKinds.SetAside.Days);

        Assert.Equal(2, purged.Count);
        Assert.True(purged.Single(each => each.Folder == built).Purged, purged.Single(each => each.Folder == built).Kept);
        Assert.False(Directory.Exists(built));
        Assert.Equal("module.exports = 'the target';\n", await File.ReadAllTextAsync(Path.Combine(outside, "index.js")));
        Assert.True(File.GetAttributes(Path.Combine(outside, "index.js")).HasFlag(FileAttributes.ReadOnly));
        var kept = purged.Single(each => each.Folder == linked);
        Assert.False(kept.Purged);
        Assert.Equal(SetAsideKept.Linked, kept.Code);
        Assert.Equal("it holds a link, at data", kept.Kept);
        Assert.True(Directory.Exists(Path.Combine(linked, "data")));
        Assert.Equal("the person's\n", await File.ReadAllTextAsync(Path.Combine(data, "notes.md")));
        File.SetAttributes(Path.Combine(outside, "index.js"), FileAttributes.Normal);
    }

    /// <summary>
    /// 🔴 AUTOTIDY2b: a tree whose own ignored <c>node_modules</c> holds paths past Windows' 260 characters, as an install leaves
    /// one, goes by the look as any other tree: its folder moved aside whole, the deep file in it, git's record of the tree let
    /// go, and fourteen days later the folder deleted, the deep file with it. On the owner's install such a tree's removal met
    /// "Filename too long", and a purge that cannot delete keeps the folder for good.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_paths_pass_260_characters_is_moved_aside_and_purged()
    {
        var root = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\nnode_modules/\n");
        await GitAsync(root, "commit", "--quiet", "-am", "ignore the installed packages");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        var deep = Path.Combine([landed.Path, "node_modules", .. Enumerable.Repeat("a-package-folder-long-enough-to-matter", 7)]);
        Directory.CreateDirectory(deep);
        var file = Path.Combine(deep, "index.js");
        await File.WriteAllTextAsync(file, "module.exports = {};\n");
        Assert.True(file.Length > 260, $"{file.Length} characters");
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(landed), Assert.Single(said), StringComparison.Ordinal);
        var aside = Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine")));
        Assert.False(Directory.Exists(landed.Path));
        Assert.True(File.Exists(Path.Combine(aside, Path.GetRelativePath(landed.Path, file))));
        Assert.DoesNotContain(Path.GetFileName(landed.Path), await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);
        Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);

        // KEEPSIZE1b: its record kept at the first look's Keeping pass, a finished tree's folder goes an hour after, not at its days.
        var later = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow.AddHours(2) }
            .LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith($"tidy  deleted the folder moved aside to {aside} on ", Assert.Single(later), StringComparison.Ordinal);
        Assert.False(Directory.Exists(aside));
        Assert.Single(Events("tidied.purged"));
        Assert.Empty(Events("tidied.kept"));
    }

    /// <summary>
    /// 🔴 A look closed in the middle of a removal finishes it before it lets go of the repository: the token is cancelled as the
    /// branch is about to be deleted, the repository is still held then, and the delete still runs to its end.
    /// </summary>
    [Fact]
    public async Task A_look_closed_mid_removal_finishes_it_before_letting_go()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        using var closing = new CancellationTokenSource();
        bool? heldThen = null;
        var trees = new SessionTrees(_home)
        {
            BeforeDeleting = async _ =>
            {
                await closing.CancelAsync();
                using var starting = TreeLock.TryStarting(_home, "default", "engine");
                heldThen = starting is null;
            },
        };

        var pass = await trees.TidyEmptyAsync(root, "engine", "default", _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()), closing.Token)
            .WaitAsync(Bound);

        Assert.True(heldThen);
        Assert.True(Assert.Single(pass.Results).Removed);
        Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(landed.Path), await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A list of the working trees git could not give is never "no tree here": the look judges nothing in the repository and
    /// says why, the press's list says each branch is unread, and the press removes nothing.
    /// </summary>
    [Fact]
    public async Task A_worktree_list_git_could_not_give_keeps_every_branch()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        await GitAsync(root, "branch", "daoris/s-treeless");
        var trees = new SessionTrees(_home) { ListFails = _ => true };
        var nobody = new HashSet<string>();

        var pass = await trees.TidyEmptyAsync(root, "engine", "default", _ => Task.FromResult<IReadOnlySet<string>>(nobody), _closing.Token)
            .WaitAsync(Bound);
        var plan = await trees.SweepPlanAsync([("engine", "default", root)], nobody);
        var swept = await trees.SweepAsync([("engine", "default", root)], nobody);

        Assert.Equal(TidyKept.Unread, pass.Held);
        Assert.Empty(pass.Results);
        Assert.Equal(2, plan.Count);
        Assert.All(plan, item => Assert.True(item.Unread, item.Branch));
        Assert.DoesNotContain(swept, result => result.Removed);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.Contains(landed.Branch, branches, StringComparison.Ordinal);
        Assert.Contains("daoris/s-treeless", branches, StringComparison.Ordinal);
        Assert.True(Directory.Exists(landed.Path));
    }

    /// <summary>
    /// 🔴 AUTOTIDY2c, the owner's install of 2026-10-15: folders under a repository's trees that git lists no tree for (an empty
    /// one a removed tree left, with its branch still here, and folders a session wrote beside its tree) are moved aside whole
    /// once left quiet, said and logged, and the trees list names them moved aside. The branch whose folder that was is never kept
    /// as unreadable; made by hand, with no record of Daoris's, its owner is unknown and it stays for the press (FABRIC-END4a, the
    /// design §C3 and §D3). A tree stays, and so does a folder a running session names.
    /// </summary>
    [Fact]
    public async Task Folders_under_the_trees_that_are_no_tree_are_moved_aside_and_listed_so()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var working = await TreeWithWorkAsync(trees, root);
        var engine = DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine");
        var leftover = Path.Combine(engine, "s-1f315a22");
        Directory.CreateDirectory(leftover);
        await GitAsync(root, "branch", "daoris/s-1f315a22");
        Directory.CreateDirectory(Path.Combine(engine, "devtools"));
        await File.WriteAllTextAsync(Path.Combine(engine, "devtools", "tool.mjs"), "export {};\n");
        Directory.CreateDirectory(Path.Combine(engine, ".tmp"));
        var named = Path.Combine(engine, "scratch");
        Directory.CreateDirectory(named);
        _ledger.Register("engine", root);
        _ledger.Working("s-running", "engine", named);
        using var log = Log();

        var said = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow.AddHours(1) }
            .LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine"));
        string Aside(string name) => asides.Single(folder => Path.GetFileName(folder).StartsWith(name + "-", StringComparison.Ordinal));
        Assert.Equal(3, asides.Length);
        Assert.Equal("export {};\n", await File.ReadAllTextAsync(Path.Combine(Aside("devtools"), "tool.mjs")));
        Assert.Equal(
            new[] { "s-1f315a22", "devtools", ".tmp" }.Select(name =>
                $"tidy  engine: `{name}`, a folder under its trees that git knows no tree in, was moved aside to {Aside(name)}, and goes once it is left untouched for 14 days.")
            .Order(StringComparer.Ordinal),
            said.Order(StringComparer.Ordinal));
        Assert.Equal([working.Path, named], Directory.GetDirectories(engine).Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.Contains("daoris/s-1f315a22", branches, StringComparison.Ordinal);
        Assert.Contains(working.Branch, branches, StringComparison.Ordinal);
        var tidied = Events("branch.tidied");
        Assert.Equal(3, tidied.Length);
        var strays = tidied.Where(data => data.GetProperty("kind").GetString() == StrayKind.Folder).ToList();
        Assert.Equal(3, strays.Count);
        Assert.All(strays, data =>
        {
            Assert.Equal(JsonValueKind.Null, data.GetProperty("branch").ValueKind);
            Assert.True(data.GetProperty("tree").GetBoolean());
            // Under the trees' folder, `work/trees/` in a new home (HOMELAYOUT3), said relative to the home.
            var aside = Path.GetRelativePath(_home, DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine")).Replace('\\', '/');
            Assert.StartsWith(aside + "/", data.GetProperty("folder").GetString(), StringComparison.Ordinal);
        });
        Assert.Empty(Events("branch.kept"));

        var listed = await trees.ListAsync();

        Assert.Equal(working.Branch, listed.Single(tree => tree.Path == working.Path).Branch);
        Assert.Equal("(not a tree)", listed.Single(tree => tree.Path == named).Branch);
        var moved = listed.Where(tree => tree.MovedAside is not null).ToList();
        Assert.Equal(asides.Order(StringComparer.Ordinal), moved.Select(tree => tree.Path).Order(StringComparer.Ordinal));
        Assert.All(moved, tree => Assert.Equal(("default", "engine", "(moved aside)"), (tree.Workspace, tree.Repository, tree.Branch)));
    }

    /// <summary>
    /// A folder something wrote in lately may be one a start is making or a session is using: it waits, saying nothing, until it
    /// has been left quiet (<see cref="TreeStrays.Quiet"/>), and is then moved aside.
    /// </summary>
    [Fact]
    public async Task A_folder_written_in_lately_waits_for_a_later_look()
    {
        var root = await RepositoryAsync("engine");
        var devtools = DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "devtools");
        Directory.CreateDirectory(devtools);
        await File.WriteAllTextAsync(Path.Combine(devtools, "tool.mjs"), "export {};\n");
        _ledger.Register("engine", root);

        var now = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Empty(now);
        Assert.True(File.Exists(Path.Combine(devtools, "tool.mjs")));

        var later = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow + TreeStrays.Quiet + TimeSpan.FromMinutes(1) }
            .LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith("tidy  engine: `devtools`, a folder under its trees that git knows no tree in, was moved aside to ", Assert.Single(later), StringComparison.Ordinal);
        Assert.False(Directory.Exists(devtools));
    }

    /// <summary>
    /// 🔴 OPENCUT1, the owner's install of 2026-10-15 (<c>s-0031408d</c>): a tree whose opening a kill cut off is locked, half
    /// checked out, on a branch no record names. Where its lock is git's own from an add that never finished, or Daoris's naming a
    /// process that no longer runs, the look lets go of the lock, moves its folder aside, lets git forget it and removes its
    /// branch, which holds nothing of its own. A lock a person made, or one a Daoris process still running holds, keeps its tree.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_opening_a_kill_cut_off_is_moved_aside_with_its_branch()
    {
        var root = await RepositoryAsync("engine");
        var engine = DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine");
        var cut = Path.Combine(engine, "s-0031408d");
        await GitAsync(root, "worktree", "add", "--quiet", "-b", "daoris/s-0031408d", cut, "main");
        await GitAsync(root, "worktree", "lock", "--reason", TreeOpenings.GitsOwn, cut);
        await GitAsync(cut, "rm", "--cached", "-r", "--quiet", ".");
        var ended = Path.Combine(engine, "s-0e0ded00");
        await GitAsync(root, "worktree", "add", "--quiet", "--lock", "--reason", TreeOpenings.LockReason(await EndedProcessAsync()), "-b", "daoris/s-0e0ded00", ended, "main");
        var opening = Path.Combine(engine, "s-0be11000");
        await GitAsync(root, "worktree", "add", "--quiet", "--lock", "--reason", TreeOpenings.LockReason(Environment.ProcessId), "-b", "daoris/s-0be11000", opening, "main");
        var persons = Path.Combine(engine, "s-9e450000");
        await GitAsync(root, "worktree", "add", "--quiet", "--lock", "--reason", "a person's", "-b", "daoris/s-9e450000", persons, "main");
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine"));
        string Aside(string name) => asides.Single(folder => Path.GetFileName(folder).StartsWith(name + "-", StringComparison.Ordinal));
        Assert.Equal(2, asides.Length);
        Assert.True(File.Exists(Path.Combine(Aside("s-0031408d"), "README.md")));
        Assert.Equal(
            [
                $"tidy  engine: removed `daoris/s-0031408d`, whose tree's opening was cut off: git's own lock from the start of its add was "
                + $"left on it; its tree's folder was moved aside to {Aside("s-0031408d")}, and goes once it is left untouched for 14 days.",
                $"tidy  engine: removed `daoris/s-0e0ded00`, whose tree's opening was cut off: the Daoris process that opened it no longer "
                + $"runs; its tree's folder was moved aside to {Aside("s-0e0ded00")}, and goes once it is left untouched for 14 days.",
            ],
            said.Order(StringComparer.Ordinal));
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain("daoris/s-0031408d", branches, StringComparison.Ordinal);
        Assert.DoesNotContain("daoris/s-0e0ded00", branches, StringComparison.Ordinal);
        Assert.Contains("daoris/s-0be11000", branches, StringComparison.Ordinal);
        Assert.Contains("daoris/s-9e450000", branches, StringComparison.Ordinal);
        var worktrees = (await GitAsync(root, "worktree", "list", "--porcelain")).Replace('\\', '/');
        Assert.DoesNotContain("s-0031408d", worktrees, StringComparison.Ordinal);
        Assert.DoesNotContain("s-0e0ded00", worktrees, StringComparison.Ordinal);
        Assert.Contains($"locked {TreeOpenings.LockReason(Environment.ProcessId)}", worktrees, StringComparison.Ordinal);
        Assert.Contains("locked a person's", worktrees, StringComparison.Ordinal);
        Assert.Equal(
            ["daoris/s-0031408d", "daoris/s-0e0ded00"],
            Events("branch.tidied").Where(data => data.GetProperty("kind").GetString() == StrayKind.Opening)
                .Select(data => data.GetProperty("branch").GetString()).Order(StringComparer.Ordinal));
        Assert.Empty(Events("branch.kept"));

        var listed = await new SessionTrees(_home).ListAsync();

        Assert.Equal(2, listed.Count(tree => tree.MovedAside is not null));
        Assert.Equal(["daoris/s-0be11000", "daoris/s-9e450000"], listed.Where(tree => tree.MovedAside is null).Select(tree => tree.Branch).Order(StringComparer.Ordinal));
    }
}
