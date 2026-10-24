using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1b's destructive paths over real git, each interleaved where a real stop, a real refusal or a real race lands (the keep-size design
/// §3.1 to §3.5 as amended after Codex's reads; <see cref="SetAsideFaults"/>): the exact tip's holder moved between its protection and the
/// branch's delete, a tracked file deleted between the judgement and the move, git's let-go and the folder's put-back both refused, a stop
/// after the move and after a removal's rename, a stop at <c>judged</c>, and the press's run posting first and resuming before it removes.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class KeepSizeStopsGitTests : KeepSizeFixture
{
    /// <summary>
    /// 🔴 Codex's reads (the design's finding 1, the branch's finding 2): the exact tip's holder rests in the branch delete's own transaction.
    /// The ref just written at the tip removed before the delete refuses it: the branch stands with the commits, the folder is <c>refused</c>,
    /// never armed. Then a real resume: the branch with no tree deleted by the look marks it <c>deleted</c>, and its record is kept.
    /// </summary>
    [Fact]
    public async Task A_tip_ref_removed_before_the_delete_refuses_it_and_the_refusal_resumes()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/squashed");
        await SquashAsync(root, tree.Branch);
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "feature/squashed");
        var tip = (await GitAsync(root, "rev-parse", tree.Branch)).Trim();

        TidyPass pass;
        using (SetAsideFaults.Within(new SetAsideFault
               {
                   AfterTipHeld = async held => await GitAsync(held, "update-ref", "-d", $"refs/daoris/discarded/{tree.Branch}"),
               }))
        {
            pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        }

        var refused = Assert.Single(pass.Results, result => result.Item.Branch == tree.Branch);
        Assert.False(refused.Removed);
        var aside = Assert.IsType<string>(refused.MovedTo);
        Assert.Equal(SlimStates.Refused, Manifest(aside).State);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", tree.Branch)).Trim());
        var now = DateTimeOffset.UtcNow;
        Assert.Empty(await PurgeAsync(trees, now.AddHours(2), root));

        // The next look: its tip's ref written again, the branch with no tree deleted, the manifest marked, the record kept.
        await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        await RecordAsync(trees, root, now);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
    }

    /// <summary>
    /// 🔴 The same, where the line's own branch is the holder: the line rewound between its read and the delete refuses the delete.
    /// </summary>
    [Fact]
    public async Task The_line_rewound_before_the_delete_refuses_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var before = (await GitAsync(root, "rev-parse", "main")).Trim();
        var tree = await LandedTreeAsync(trees, root);

        TidyPass pass;
        using (SetAsideFaults.Within(new SetAsideFault { AfterTipHeld = async held => await GitAsync(held, "update-ref", "refs/heads/main", before) }))
        {
            pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        }

        var refused = Assert.Single(pass.Results, result => result.Item.Branch == tree.Branch);
        Assert.False(refused.Removed);
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal(SlimStates.Refused, Manifest(refused.MovedTo!).State);
    }

    /// <summary>
    /// 🔴 Codex's read of the design, finding 2: a tracked file at the tree's top deleted after the judgement read it and before the move leaves
    /// no later time on anything that stays; git's status read again in the moved folder finds it, and the folder is put back, the tree and
    /// the branch kept.
    /// </summary>
    [Fact]
    public async Task A_tracked_file_deleted_between_the_judgement_and_the_move_keeps_the_tree()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);

        TidyPass pass;
        using (SetAsideFaults.Within(new SetAsideFault { BeforeMove = folder => File.Delete(Path.Combine(folder, "README.md")) }))
        {
            pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        }

        var kept = Assert.Single(pass.Results, result => result.Item.Branch == tree.Branch);
        Assert.False(kept.Removed);
        Assert.Contains("changed between the judgement and the move", kept.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(tree.Path));
        Assert.False(File.Exists(Path.Combine(tree.Path, "README.md")));
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(trees.SetAsideRoot, "default", "engine"), "*" + SlimManifest.Suffix));
    }

    /// <summary>
    /// 🔴 Codex's read of the design, finding 4: git's let-go refused and the folder's put-back refused too. The manifest stays at <c>judged</c>
    /// beside the folder aside, its facts whole; the next look removes the registration whose folder is gone and its branch, marks it
    /// <c>deleted</c>, and its record is kept.
    /// </summary>
    [Fact]
    public async Task A_failed_put_back_keeps_the_manifest_and_resumes_to_the_record()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);

        TidyPass pass;
        using (SetAsideFaults.Within(new SetAsideFault { RefuseLetGo = true, RefusePutBack = true }))
        {
            pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        }

        var kept = Assert.Single(pass.Results, result => result.Item.Branch == tree.Branch);
        Assert.False(kept.Removed);
        var aside = Assert.Single(Directory.GetDirectories(Path.Combine(trees.SetAsideRoot, "default", "engine")));
        Assert.Equal(SlimStates.Judged, Manifest(aside).State);
        Assert.False(Directory.Exists(tree.Path));

        await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
    }

    /// <summary>
    /// 🔴 Row 1b's stop after a step: the process stops right after the move is marked <c>moved</c>, before the branch's delete. The next look
    /// deletes the branch with no tree, marks the manifest <c>deleted</c>, and its record is kept.
    /// </summary>
    [Fact]
    public async Task A_stop_after_the_move_resumes_to_the_record()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);

        using (SetAsideFaults.Within(new SetAsideFault { StopAfterMove = true }))
        {
            await Assert.ThrowsAsync<SetAsideStop>(() => trees.TidyEmptyAsync(root, "engine", "default", NoneRunning));
        }

        var aside = Assert.Single(Directory.GetDirectories(Path.Combine(trees.SetAsideRoot, "default", "engine")));
        Assert.Equal(SlimStates.Moved, Manifest(aside).State);
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());

        await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        Assert.Equal(SlimStates.Deleted, Manifest(aside).State);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
    }

    /// <summary>
    /// 🔴 Codex's read of the design, finding 5, and Opus finding 19: a stop right after a removal's rename into <c>.going</c>, before its delete.
    /// The next purge puts the folder back whole, its manifest beside it all along, and says why; a file written into it since then keeps it
    /// at the next, said <c>written</c>.
    /// </summary>
    [Fact]
    public async Task A_stop_after_the_rename_puts_the_folder_back_and_a_late_write_keeps_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        var files = Directory.GetFiles(aside, "*", SearchOption.AllDirectories).Length;

        using (SetAsideFaults.Within(new SetAsideFault { StopAfterRename = true }))
        {
            await Assert.ThrowsAsync<SetAsideStop>(() => PurgeAsync(trees, now.AddMinutes(61), root));
        }

        Assert.False(Directory.Exists(aside));
        Assert.True(File.Exists(SlimManifest.PathFor(aside)));
        Assert.Empty(trees.ListSetAside(KeepPolicy.Default, now));

        var back = Assert.Single(await PurgeAsync(trees, now.AddMinutes(10), root));
        Assert.Equal((aside, false, SetAsideKept.Returned), (back.Folder, back.Purged, back.Code));
        Assert.Contains("before its delete began", back.Kept, StringComparison.Ordinal);
        Assert.Equal(files, Directory.GetFiles(aside, "*", SearchOption.AllDirectories).Length);

        await File.WriteAllTextAsync(Path.Combine(aside, "late.txt"), "written after it was put back\n");
        var written = Assert.Single(await PurgeAsync(trees, now.AddMinutes(62), root));
        Assert.Equal((false, SetAsideKept.Written), (written.Purged, written.Code));
    }

    /// <summary>
    /// 🔴 The last fixes' check, finding 1: a stop inside the hour's delete, its first file gone and its record saying the delete began. A file
    /// written into what remains puts it back at the next purge, said, the rest of it intact, and its manifest ends <c>unmarked</c>.
    /// </summary>
    [Fact]
    public async Task A_stop_inside_the_hours_delete_puts_back_what_remains_where_a_file_was_written()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        // Recorded an hour ago by the clock, so the removal begins now and a file written after it is a write.
        await RecordAsync(trees, root, DateTimeOffset.UtcNow.AddMinutes(-61));
        var files = Files(aside);

        using (SetAsideFaults.Within(new SetAsideFault { StopInDelete = true }))
        {
            await Assert.ThrowsAsync<SetAsideStop>(() => PurgeAsync(trees, DateTimeOffset.UtcNow, root));
        }

        var going = GoingOf(trees, aside);
        var remains = Files(going);
        Assert.Equal(files.Count - 1, remains.Count);
        Assert.Subset(files.ToHashSet(), remains.ToHashSet());
        await File.WriteAllTextAsync(Path.Combine(going, "late.txt"), "written while its delete stood stopped\n");

        var back = Assert.Single(await PurgeAsync(trees, DateTimeOffset.UtcNow, root));

        Assert.Equal((aside, false, SetAsideKept.Returned), (back.Folder, back.Purged, back.Code));
        Assert.Contains("written after its removal began (late.txt)", back.Kept, StringComparison.Ordinal);
        Assert.Equal(remains.Append("late.txt").Order(StringComparer.Ordinal), Files(aside));
        Assert.Equal(SlimStates.Unmarked, Manifest(aside).State);
        Assert.False(Directory.Exists(going));
        Assert.False(File.Exists(GoingRecord.PathFor(going)));
    }

    /// <summary>
    /// 🔴 The last fixes' check, finding 1: a stop inside the hour's delete, then the tip's ref dropped. The next purge's holding check fails, so
    /// what remains goes back, said, and its manifest is <c>unmarked</c>: it waits its days.
    /// </summary>
    [Fact]
    public async Task A_stop_inside_the_hours_delete_puts_back_what_remains_where_its_ref_was_dropped()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/squashed");
        await SquashAsync(root, tree.Branch);
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "feature/squashed");
        var aside = await SetAsideAsync(trees, root);
        var reference = Manifest(aside).Finished!.Ref!;
        await RecordAsync(trees, root, DateTimeOffset.UtcNow.AddMinutes(-61));
        var files = Files(aside);

        using (SetAsideFaults.Within(new SetAsideFault { StopInDelete = true }))
        {
            await Assert.ThrowsAsync<SetAsideStop>(() => PurgeAsync(trees, DateTimeOffset.UtcNow, root));
        }

        await GitAsync(root, "update-ref", "-d", reference);
        var back = Assert.Single(await PurgeAsync(trees, DateTimeOffset.UtcNow, root));

        Assert.Equal((aside, false, SetAsideKept.Returned), (back.Folder, back.Purged, back.Code));
        Assert.Contains("is gone, so nothing holds its tip", back.Kept, StringComparison.Ordinal);
        Assert.Contains("what remains was put back", back.Kept, StringComparison.Ordinal);
        Assert.Equal(files.Count - 1, Files(aside).Count);
        Assert.Equal(SlimStates.Unmarked, Manifest(aside).State);
        Assert.False(Directory.Exists(GoingOf(trees, aside)));
    }

    /// <summary>
    /// 🔴 The last fixes' check, finding 1: a stop inside the days' delete with nothing written since. The next purge finishes it with no
    /// holding check, a registry or none, and its manifest and its removal's record go with it.
    /// </summary>
    [Fact]
    public async Task A_stop_inside_the_days_delete_finishes_where_nothing_was_written()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        (Manifest(aside) with { State = SlimStates.Unmarked }).Write(SlimManifest.PathFor(aside));
        var due = DateTimeOffset.UtcNow.AddDays(15);

        using (SetAsideFaults.Within(new SetAsideFault { StopInDelete = true }))
        {
            await Assert.ThrowsAsync<SetAsideStop>(() => PurgeAsync(trees, due));
        }

        var going = GoingOf(trees, aside);
        Assert.True(Directory.Exists(going));
        Assert.True(File.Exists(SlimManifest.PathFor(aside)));

        var gone = Assert.Single(await PurgeAsync(trees, due));

        Assert.Equal((aside, true, false), (gone.Folder, gone.Purged, gone.Hour));
        Assert.False(Directory.Exists(going));
        Assert.False(Directory.Exists(aside));
        Assert.False(File.Exists(SlimManifest.PathFor(aside)));
        Assert.False(File.Exists(GoingRecord.PathFor(going)));
    }

    /// <summary>
    /// 🔴 Round 3's finding 1: a stop at <c>judged</c> with its folder present keeps its manifest through the slim and reaches its record once
    /// its branch is gone.
    /// </summary>
    [Fact]
    public async Task A_stop_at_judged_keeps_its_manifest_and_reaches_its_record()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        (Manifest(aside) with { State = SlimStates.Judged }).Write(SlimManifest.PathFor(aside));
        var now = DateTimeOffset.UtcNow;

        await PurgeAsync(trees, now.AddHours(2), root);
        Assert.Equal(SlimStates.Judged, Manifest(aside).State);
        await RecordAsync(trees, root, now);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
    }

    /// <summary>
    /// 🔴 The press's run (the keep-size design §5): before it removes, it resumes a folder stopped at <c>judged</c> and posts a record due
    /// first; then it removes the folders as listed. Through <see cref="SpaceRuns"/>, the press's own door.
    /// </summary>
    [Fact]
    public async Task The_press_resumes_and_posts_first_then_removes_as_listed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        (Manifest(aside) with { State = SlimStates.Judged }).Write(SlimManifest.PathFor(aside));
        var world = new SpaceFreeWorld(_home)
        {
            Sizes = new SpaceSizes(),
            Registry = _ => Task.FromResult<IReadOnlyList<RepoView>>([new RepoView("engine", true, root, "default")]),
            PostRecord = (post, ct) =>
            {
                Assert.True(Directory.Exists(aside), "the record is posted before its folder goes");
                return PostAsync(post, ct);
            },
        };
        var listed = Assert.Single(trees.ListSetAside(KeepPolicy.Default, DateTimeOffset.UtcNow));
        var asked = new SpaceAsked("set-aside", listed.Id, Early: true, "early", Own: false);

        var start = new SpaceRuns().Start(world, KeepingRun.Press, [asked]);
        await Assert.IsType<SpaceRun>(start.Run).Finished.WaitAsync(TimeSpan.FromMinutes(2));

        Assert.Single(_posts);
        Assert.False(Directory.Exists(aside));
    }

    /// <summary>🔴 Codex finding 6 of round 2: a manifest that cannot be written keeps the tree and the branch, and the tip's ref with them goes.</summary>
    [Fact]
    public async Task A_manifest_not_written_keeps_the_tree_and_the_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);
        // Where the manifest goes is a file, so neither it nor the folder beside it can be made.
        Directory.CreateDirectory(Path.Combine(trees.SetAsideRoot, "default"));
        await File.WriteAllTextAsync(Path.Combine(trees.SetAsideRoot, "default", "engine"), "in the way\n");

        var pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);

        Assert.DoesNotContain(pass.Results, result => result.Removed);
        Assert.True(Directory.Exists(tree.Path));
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
    }

    /// <summary>
    /// 🔴 Opus claim 6: a line held only by <c>origin</c> is no hold that stays, so the tip's ref is written, on the look's path and on the press's.
    /// </summary>
    [Fact]
    public async Task A_line_held_only_by_origin_gets_the_tips_ref_on_both_paths()
    {
        var root = await RepositoryAsync("engine");
        var origin = Path.Combine(_scratch, "origin.git");
        await GitAsync(_scratch, "clone", "--quiet", "--bare", root, origin);
        await GitAsync(root, "remote", "add", "origin", origin);
        await GitAsync(root, "fetch", "--quiet", "origin");
        var trees = new SessionTrees(_home);
        var looked = await TreeWithWorkAsync(trees, root);
        var pressed = await TreeWithWorkAsync(trees, root);
        // Both merged onto origin's line by one commit there, and the local line left before it: origin alone holds them.
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "-m", "both", looked.Branch, pressed.Branch);
        await GitAsync(root, "push", "--quiet", "origin", "main");
        await GitAsync(root, "reset", "--quiet", "--hard", "HEAD~1");
        await GitAsync(root, "fetch", "--quiet", "origin");

        var tips = new Dictionary<string, string>
        {
            [looked.Branch] = (await GitAsync(root, "rev-parse", looked.Branch)).Trim(),
            [pressed.Branch] = (await GitAsync(root, "rev-parse", pressed.Branch)).Trim(),
        };

        // The press's path first: *Clean up…* of the one, its tip on a ref of its own.
        var removed = await trees.CleanAsync([("engine", "default", root)], new HashSet<string>(), new HashSet<string> { $"engine:{pressed.Branch}" });
        Assert.Contains(removed.Sessions, result => result.Removed && result.Item.Branch == pressed.Branch);
        Assert.Equal(tips[pressed.Branch], (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{pressed.Branch}")).Trim());

        // The look's path: the other, its tip on a ref of its own, named in its manifest.
        var aside = await SetAsideAsync(trees, root);
        Assert.Equal(looked.Branch, Manifest(aside).Branch);
        Assert.Equal($"refs/daoris/discarded/{looked.Branch}", Manifest(aside).Finished!.Ref);
        Assert.Equal(tips[looked.Branch], (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{looked.Branch}")).Trim());
    }

    /// <summary>
    /// 🔴 Opus finding 19: a file held open in a recorded folder keeps it whole, <c>busy</c>, since it is renamed before anything is deleted,
    /// and the purge keeps that failure in <c>purge-failed.json</c>, which the inventory reads.
    /// </summary>
    [Fact]
    public async Task A_file_held_open_keeps_the_folder_whole()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await LandedTreeAsync(trees, root);
        var aside = await SetAsideAsync(trees, root);
        var now = DateTimeOffset.UtcNow;
        await RecordAsync(trees, root, now);
        var files = Directory.GetFiles(aside, "*", SearchOption.AllDirectories).Length;

        SetAside kept;
        using (File.Open(Path.Combine(aside, "README.md"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            kept = Assert.Single(await PurgeAsync(trees, now.AddMinutes(61), root));
        }

        Assert.Equal((false, TidyKept.Busy), (kept.Purged, kept.Code));
        Assert.Equal(files, Directory.GetFiles(aside, "*", SearchOption.AllDirectories).Length);
        Assert.Contains(Path.GetFullPath(aside), trees.SetAsideFailed().Keys, StringComparer.OrdinalIgnoreCase);
    }
}
