using System.Text.Json;

namespace Daoris.Driver.Tests.Platform;

/// <summary>
/// The folders Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3; the second maker's review): a
/// folder is Daoris's only where its name EQUALS a working directory Daoris's own records name, flattened as Claude Code
/// flattens it; one a session may still use is kept whatever its age; the press moves a folder aside for fourteen days and
/// never deletes it. 🔴 Every home here is a scratch one handed in: no test reads or touches the real tool home or Daoris home.
/// </summary>
public sealed class ToolHomeLeftoversTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-toolhome-tests", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Old = TimeSpan.FromHours(3);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A scratch folder something still holds is left for the OS to clean.
        }
    }

    private (string Home, string ToolHome, string Projects) Homes()
    {
        var home = Path.Combine(_root, "install", "data");
        var tool = Path.Combine(_root, "person", ".claude");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(Path.Combine(tool, "projects"));
        return (home, tool, Path.Combine(tool, "projects"));
    }

    private static WiringWorld WorldOf(string home, string? tool, IReadOnlyList<ToolOrigin>? origins = null) => new()
    {
        Home = home,
        BaseDirectory = home,
        Process = _ => null,
        ToolHome = tool,
        ToolOrigins = origins,
    };

    /// <summary>A project folder with one file, both written <paramref name="age"/> before <see cref="Now"/>.</summary>
    private static string Project(string projects, string name, TimeSpan age, int bytes = 10)
    {
        var folder = Path.Combine(projects, name);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "session.jsonl");
        File.WriteAllBytes(file, new byte[bytes]);
        var at = (Now - age).UtcDateTime;
        File.SetLastWriteTimeUtc(file, at);
        Directory.SetLastWriteTimeUtc(folder, at);
        return folder;
    }

    private static string Tree(string home, string session) => DaorisHome.Entry(home, HomeEntry.Trees, "main", "repo", session);

    private static object Record(string id, string? tree, string state = "completed", string? profile = "work", string repository = "repo", string? ask = null) =>
        new { id, tree, state, profile, repository, ask, workspace = "main" };

    private static string Records(params object[] rows) => JsonSerializer.Serialize(rows);

    private (WiringWorld World, string Projects, string Home) Mixed(params object[] rows)
    {
        var (home, tool, projects) = Homes();
        return (WorldOf(home, tool, ToolHomeLeftovers.Origins(home, Records(rows))), projects, home);
    }

    [Fact]
    public void Flatten_keeps_letters_and_digits_and_turns_every_other_character_into_a_dash()
    {
        // Example paths are spelled in pieces, so no machine path sits in a tracked file.
        Assert.Equal("C--Work-people-my-app-2", ToolHomeLeftovers.Flatten("C:" + @"\Work\people\my.app_2"));
        Assert.Equal("-srv-people-x", ToolHomeLeftovers.Flatten("/" + "srv/people/x"));
    }

    [Fact]
    public void List_names_only_the_exact_working_directories_Daoris_s_records_hold_from_a_mixed_home()
    {
        var (home, tool, projects) = Homes();
        var treeA = Tree(home, "s-1a2b3c4d");
        var treeB = Tree(home, "s-5e6f7a8b");
        var help = HelpRoom.PathOf(home);
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", treeA), Record("b", treeB, profile: null, state: "failed")));
        string[] mine = [ToolHomeLeftovers.Flatten(treeA), ToolHomeLeftovers.Flatten(treeB), ToolHomeLeftovers.Flatten(help)];
        string[] theirs =
        [
            ToolHomeLeftovers.Flatten(Path.Combine(_root, "person", "dev", "my-app")),
            // A name that only starts like a root, or like the help room: a prefix proves nothing.
            ToolHomeLeftovers.Flatten(help) + "-backup",
            ToolHomeLeftovers.Flatten(DaorisHome.Entry(home, HomeEntry.Trees)) + "-main-repo-s-0000aaaa",
            ToolHomeLeftovers.Flatten(home) + "-other-help-personal",
        ];
        foreach (var name in mine.Concat(theirs)) Project(projects, name, Old);

        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, origins));

        Assert.Equal(mine.Order(StringComparer.Ordinal), listing.Folders.Select(folder => folder.Name).Order(StringComparer.Ordinal));
        Assert.Null(listing.Why);
    }

    [Fact]
    public void A_record_whose_tree_is_outside_Daoris_s_working_roots_is_no_origin()
    {
        var (home, tool, projects) = Homes();
        var theirs = Path.Combine(_root, "person", "dev", "checkout");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", theirs)));
        Project(projects, ToolHomeLeftovers.Flatten(theirs), Old);

        Assert.DoesNotContain(ToolHomeLeftovers.List(WorldOf(home, tool, origins)).Folders, folder => folder.Name == ToolHomeLeftovers.Flatten(theirs));
    }

    [Fact]
    public void Two_paths_that_flatten_alike_are_listed_for_neither()
    {
        var (home, tool, projects) = Homes();
        // Flattening is lossy: a dot and a dash make one name, so which conversation is whose cannot be told.
        var dotted = Tree(home, "s-a.b");
        var dashed = Tree(home, "s-a-b");
        var other = Tree(home, "s-cccc1111");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", dotted), Record("b", dashed), Record("c", other)));
        Project(projects, ToolHomeLeftovers.Flatten(dashed), Old);
        Project(projects, ToolHomeLeftovers.Flatten(other), Old);

        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, origins));

        Assert.Equal([ToolHomeLeftovers.Flatten(other)], listing.Folders.Select(folder => folder.Name));
        Assert.Equal(1, listing.Ambiguous);
        var outcome = ToolHomeLeftovers.Clear(WorldOf(home, tool, origins), [new ToolAsk(ToolHomeLeftovers.Flatten(dashed), "x")], Now);
        Assert.Empty(outcome.Cleared);
        Assert.Equal("not-listed", Assert.Single(outcome.Kept).Why);
        Assert.True(Directory.Exists(Path.Combine(projects, ToolHomeLeftovers.Flatten(dashed))));
    }

    [Fact]
    public void Names_that_differ_only_in_case_are_compared_by_the_folder_s_own_rules_and_said()
    {
        var (home, tool, projects) = Homes();
        var lower = Tree(home, "s-abcdef01");
        var upper = Tree(home, "s-ABCDEF01");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", lower), Record("b", upper)));
        Project(projects, ToolHomeLeftovers.Flatten(lower), Old);

        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, origins));

        if (listing.CaseInsensitive)
        {
            // One folder could be either path's: neither is listed.
            Assert.Empty(listing.Folders);
            Assert.Equal(1, listing.Ambiguous);
        }
        else
        {
            Assert.Equal([ToolHomeLeftovers.Flatten(lower)], listing.Folders.Select(folder => folder.Name));
            Assert.Equal(0, listing.Ambiguous);
        }
    }

    [Fact]
    public void A_working_directory_with_a_letter_outside_ASCII_is_uncertain_and_not_listed()
    {
        var (home, tool, projects) = Homes();
        var odd = Tree(home, "s-café");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", odd)));
        Project(projects, ToolHomeLeftovers.Flatten(odd), Old);

        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, origins));

        Assert.Empty(listing.Folders);
        Assert.Equal(1, listing.Uncertain);
    }

    [Fact]
    public void An_idle_session_that_is_still_live_keeps_its_folder_whatever_its_age()
    {
        var tree = Tree(Homes().Home, "s-11112222");
        var (world, projects, _) = Mixed(Record("a", tree, state: "awaiting-person"));
        var name = ToolHomeLeftovers.Flatten(tree);
        Project(projects, name, TimeSpan.FromDays(30));

        var listed = Assert.Single(ToolHomeLeftovers.List(world).Folders);
        Assert.Equal("session", listed.Keeps);
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, listed.Id)], Now);

        Assert.Empty(outcome.Cleared);
        Assert.Equal(("session", name), (Assert.Single(outcome.Kept).Why, outcome.Kept[0].Name));
        Assert.True(Directory.Exists(Path.Combine(projects, name)));
    }

    [Fact]
    public void A_legacy_record_with_no_account_and_a_kept_conversation_keeps_its_folder_but_an_account_s_does_not()
    {
        var (home, tool, projects) = Homes();
        var legacy = Tree(home, "s-aaaa0001");
        var daoris = Tree(home, "s-bbbb0002");
        new HarnessConversations(home).Keep("legacy", "claude-code", "conversation-1");
        new HarnessConversations(home).Keep("daoris", "claude-code", "conversation-2");
        var origins = ToolHomeLeftovers.Origins(home, Records(
            Record("legacy", legacy, profile: null), Record("daoris", daoris, profile: "work")));
        Project(projects, ToolHomeLeftovers.Flatten(legacy), Old);
        Project(projects, ToolHomeLeftovers.Flatten(daoris), Old);

        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, origins));

        Assert.Equal("resumable", listing.Folders.Single(folder => folder.Name == ToolHomeLeftovers.Flatten(legacy)).Keeps);
        Assert.Null(listing.Folders.Single(folder => folder.Name == ToolHomeLeftovers.Flatten(daoris)).Keeps);
    }

    [Fact]
    public void Where_the_records_could_not_be_read_activity_is_unknown_and_nothing_is_removable()
    {
        var (home, tool, projects) = Homes();
        var help = ToolHomeLeftovers.Flatten(HelpRoom.PathOf(home));
        Project(projects, help, Old);
        var world = WorldOf(home, tool, ToolHomeLeftovers.Origins(home, null));

        var listed = Assert.Single(ToolHomeLeftovers.List(world).Folders);
        Assert.Equal(("records-unread", help), (listed.Keeps, listed.Name));
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(help, listed.Id)], Now);
        Assert.Empty(outcome.Cleared);
        Assert.Equal("records-unread", Assert.Single(outcome.Kept).Why);
    }

    [Fact]
    public void A_folder_that_cannot_be_fully_read_is_kept_unread_and_a_walk_that_failed_is_partial()
    {
        var tree = Tree(Homes().Home, "s-33334444");
        var (world, projects, _) = Mixed(Record("a", tree));
        var name = ToolHomeLeftovers.Flatten(tree);
        var folder = Project(projects, name, Old);
        File.WriteAllBytes(Path.Combine(folder, "two.jsonl"), new byte[3]);
        File.WriteAllBytes(Path.Combine(folder, "three.jsonl"), new byte[3]);
        var listed = Assert.Single(ToolHomeLeftovers.List(world).Folders);

        // A budget the walk cannot finish inside is a failed read, and unknown activity keeps the folder.
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, listed.Id)], Now, new ToolClearOptions { Budget = new WiringBudget(1, TimeSpan.FromMinutes(1)) });

        Assert.Equal("unread", Assert.Single(outcome.Kept).Why);
        Assert.True(Directory.Exists(folder));
        Assert.True(WiringSizes.MeasureFolder(Path.Combine(_root, "missing"), WiringBudget.Default, CancellationToken.None).Partial);
    }

    [Fact]
    public void A_folder_a_session_wrote_in_lately_stays_as_the_second_guard()
    {
        var tree = Tree(Homes().Home, "s-55556666");
        var (world, projects, _) = Mixed(Record("a", tree));
        var name = ToolHomeLeftovers.Flatten(tree);
        Project(projects, name, TimeSpan.FromMinutes(2));
        var listed = Assert.Single(ToolHomeLeftovers.List(world).Folders);

        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, listed.Id)], Now);

        Assert.Equal("in-use", Assert.Single(outcome.Kept).Why);
    }

    [Fact]
    public void A_folder_replaced_between_the_list_and_the_press_is_refused()
    {
        var tree = Tree(Homes().Home, "s-77778888");
        var (world, projects, _) = Mixed(Record("a", tree));
        var name = ToolHomeLeftovers.Flatten(tree);
        var folder = Project(projects, name, Old);
        var listed = Assert.Single(ToolHomeLeftovers.List(world).Folders);

        // The old folder stays beside, so the new one cannot take its identity.
        Directory.Move(folder, folder + "-aside");
        Project(projects, name, Old);
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, listed.Id)], Now);

        Assert.Empty(outcome.Cleared);
        Assert.Equal("changed", Assert.Single(outcome.Kept).Why);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void An_ancestor_that_is_a_link_refuses_every_folder_under_it()
    {
        var (home, _, _) = Homes();
        var real = Path.Combine(_root, "real");
        var linked = Path.Combine(_root, "linked");
        Directory.CreateDirectory(Path.Combine(real, ".claude", "projects"));
        try
        {
            Directory.CreateSymbolicLink(linked, real);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Not a skip: a machine that cannot make a link cannot prove this, and the run says so.
            Assert.Fail("this machine could not create a symbolic link, so the ancestor-link refusal is unproven here: " + error.Message);
        }

        var tree = Tree(home, "s-99990000");
        var name = ToolHomeLeftovers.Flatten(tree);
        var folder = Project(Path.Combine(real, ".claude", "projects"), name, Old);
        var world = WorldOf(home, Path.Combine(linked, ".claude"), ToolHomeLeftovers.Origins(home, Records(Record("a", tree))));

        var listing = ToolHomeLeftovers.List(world);
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, "any")], Now);

        Assert.Equal("linked", listing.Why);
        Assert.Empty(listing.Folders);
        Assert.Empty(outcome.Cleared);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void Clear_moves_exactly_the_folders_asked_aside_for_fourteen_days_and_the_purge_takes_them_after()
    {
        var home = Homes().Home;
        var treeA = Tree(home, "s-aaaa1111");
        var treeB = Tree(home, "s-bbbb2222");
        var (world, projects, _) = Mixed(Record("a", treeA), Record("b", treeB));
        var (a, b) = (ToolHomeLeftovers.Flatten(treeA), ToolHomeLeftovers.Flatten(treeB));
        Project(projects, a, Old, bytes: 10);
        Project(projects, b, Old);
        var person = Project(projects, ToolHomeLeftovers.Flatten(Path.Combine(_root, "person", "dev", "my-app")), Old);
        var listed = ToolHomeLeftovers.List(world).Folders.Single(folder => folder.Name == a);

        var now = DateTimeOffset.UtcNow;
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(a, listed.Id)], Now);

        var moved = Assert.Single(outcome.Cleared);
        Assert.Equal((a, 10), (moved.Name, moved.Bytes));
        Assert.False(Directory.Exists(Path.Combine(projects, a)));
        Assert.True(Directory.Exists(Path.Combine(projects, b)));
        Assert.True(Directory.Exists(person));
        // It went where the trees' own moved-aside folders go, so the same purge takes it, nothing deleted before.
        Assert.StartsWith(Path.Combine(DaorisHome.Entry(home, HomeEntry.Trees), ".tidied"), moved.To);
        Assert.True(File.Exists(Path.Combine(moved.To, "session.jsonl")));
        Assert.Equal(ToolHomeLeftovers.RecoveryDays, 14);

        var trees = new SessionTrees(home);
        Assert.Empty(trees.PurgeSetAside(now.AddDays(1), BranchTidying.SetAsideFor).Where(each => each.Purged));
        Assert.True(Directory.Exists(moved.To));
        Assert.Contains(trees.PurgeSetAside(now.AddDays(15), BranchTidying.SetAsideFor), each => each.Purged && each.Folder == moved.To);
        Assert.False(Directory.Exists(moved.To));
    }

    [Fact]
    public void Where_the_tool_home_and_Daoris_s_folder_are_on_different_volumes_the_folders_are_listed_and_no_press_is_offered()
    {
        var tree = Tree(Homes().Home, "s-cccc3333");
        var (world, projects, home) = Mixed(Record("a", tree));
        var name = ToolHomeLeftovers.Flatten(tree);
        var folder = Project(projects, name, Old);
        // One volume for Daoris's home, another for everything else: a rename cannot cross, and no copy is ever made.
        Func<string, string> volumeOf = path => path.StartsWith(home, StringComparison.OrdinalIgnoreCase) ? "A:" : "B:";

        var listing = ToolHomeLeftovers.List(world, volumeOf);
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, Assert.Single(listing.Folders).Id)], Now, new ToolClearOptions { VolumeOf = volumeOf });

        Assert.Equal("other-volume", listing.PressWhy);
        Assert.Empty(outcome.Cleared);
        Assert.Equal("other-volume", Assert.Single(outcome.Kept).Why);
        Assert.True(Directory.Exists(folder));
        Assert.Null(ToolHomeLeftovers.List(world).PressWhy);
    }

    [Fact]
    public void A_path_outside_Daoris_s_roots_that_flattens_like_one_inside_them_collides_and_neither_is_listed()
    {
        var (home, tool, projects) = Homes();
        var inside = Tree(home, "s-1");
        // The sibling folder `<home>-trees/…` flattens to the same name, and lies outside every working root.
        var outside = Path.Combine(home + "-trees", "main", "repo", "s-1");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", inside), Record("b", outside)));
        Project(projects, ToolHomeLeftovers.Flatten(inside), Old);

        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, origins));

        Assert.Empty(listing.Folders);
        Assert.Equal(1, listing.Ambiguous);
    }

    [Fact]
    public void A_session_that_resumes_during_the_press_keeps_its_folder_for_the_records_are_read_again_for_each()
    {
        var (home, tool, projects) = Homes();
        var (treeA, treeB) = (Tree(home, "s-aaaa5555"), Tree(home, "s-bbbb6666"));
        var (a, b) = (ToolHomeLeftovers.Flatten(treeA), ToolHomeLeftovers.Flatten(treeB));
        Project(projects, a, Old);
        Project(projects, b, Old);
        var free = ToolHomeLeftovers.Origins(home, Records(Record("a", treeA), Record("b", treeB)));
        var world = WorldOf(home, tool, free);
        var ids = ToolHomeLeftovers.List(world).Folders.ToDictionary(folder => folder.Name, folder => folder.Id);
        var reads = 0;

        // The second read finds session b live again.
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(a, ids[a]), new ToolAsk(b, ids[b])], Now, new ToolClearOptions
        {
            OriginsNow = () => ++reads == 1 ? free : ToolHomeLeftovers.Origins(home, Records(Record("a", treeA), Record("b", treeB, state: "running"))),
        });

        Assert.Equal([a], outcome.Cleared.Select(done => done.Name));
        Assert.Equal(("session", b), (Assert.Single(outcome.Kept).Why, outcome.Kept[0].Name));
        Assert.True(Directory.Exists(Path.Combine(projects, b)));
        Assert.Equal(2, reads);
    }

    [Fact]
    public void A_conversation_record_that_cannot_be_read_is_not_absent_and_keeps_the_folder()
    {
        var (home, tool, projects) = Homes();
        var legacy = Tree(home, "s-dddd7777");
        Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.Sessions));
        File.WriteAllText(new HarnessConversations(home).PathOf("legacy"), "{ this is not json");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("legacy", legacy, profile: null)));
        Project(projects, ToolHomeLeftovers.Flatten(legacy), Old);

        var listed = Assert.Single(ToolHomeLeftovers.List(WorldOf(home, tool, origins)).Folders);
        var outcome = ToolHomeLeftovers.Clear(WorldOf(home, tool, origins), [new ToolAsk(listed.Name, listed.Id)], Now);

        Assert.Equal("record-unread", listed.Keeps);
        Assert.Equal("record-unread", Assert.Single(outcome.Kept).Why);
    }

    [Fact]
    public void The_identity_reads_the_volume_and_the_whole_file_id_and_a_replacement_that_shares_a_half_is_refused()
    {
        var tree = Tree(Homes().Home, "s-eeee8888");
        var (world, projects, _) = Mixed(Record("a", tree));
        var name = ToolHomeLeftovers.Flatten(tree);
        var folder = Project(projects, name, Old);
        var listed = Assert.Single(ToolHomeLeftovers.List(world).Folders);

        // FILE_ID_INFO is a 64-bit volume serial then a 128-bit id: 24 bytes, no filler between.
        Assert.Equal((24, 0, 8), ToolHomeLeftovers.IdentityLayout);
        Assert.Matches("^[0-9a-f]{16}-[0-9a-f]{32}$", listed.Id);
        Assert.Equal(listed.Id, ToolHomeLeftovers.FileId(folder));
        // A folder that shares the volume and the low half of the id but not the high half is another folder.
        var lookalike = listed.Id[..^32] + new string('0', 16) + listed.Id[^16..];
        if (lookalike == listed.Id) lookalike = listed.Id[..^1] + (listed.Id[^1] == '0' ? '1' : '0');
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, lookalike)], Now);

        Assert.Equal("changed", Assert.Single(outcome.Kept).Why);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void A_link_anywhere_on_the_destination_s_way_is_refused_before_anything_is_created()
    {
        var tree = Tree(Homes().Home, "s-ffff9999");
        var (world, projects, home) = Mixed(Record("a", tree));
        var name = ToolHomeLeftovers.Flatten(tree);
        var folder = Project(projects, name, Old);
        var elsewhere = Path.Combine(_root, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        var trees = DaorisHome.Entry(home, HomeEntry.Trees);
        Directory.CreateDirectory(trees);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(trees, ".tidied"), elsewhere);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Fail("this machine could not create a symbolic link, so the destination-link refusal is unproven here: " + error.Message);
        }

        var listed = Assert.Single(ToolHomeLeftovers.List(world).Folders);
        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(name, listed.Id)], Now);

        Assert.Equal("linked", Assert.Single(outcome.Kept).Why);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
    }

    [Fact]
    public void The_destination_is_unique_and_made_by_the_rename_alone_so_one_that_exists_is_never_used_or_touched()
    {
        var home = Homes().Home;
        var (treeA, treeB) = (Tree(home, "s-1111aaaa"), Tree(home, "s-2222bbbb"));
        var (world, projects, _) = Mixed(Record("a", treeA), Record("b", treeB));
        var (a, b) = (ToolHomeLeftovers.Flatten(treeA), ToolHomeLeftovers.Flatten(treeB));
        Project(projects, a, Old);
        Project(projects, b, Old);
        var ids = ToolHomeLeftovers.List(world).Folders.ToDictionary(folder => folder.Name, folder => folder.Id);

        var outcome = ToolHomeLeftovers.Clear(world, [new ToolAsk(a, ids[a]), new ToolAsk(b, ids[b])], Now);

        Assert.Equal(2, outcome.Cleared.Select(done => done.To).Distinct().Count());
        // `<name>-<8 hex>-<moment>`: the moment is the last part, as the purge reads it.
        Assert.All(outcome.Cleared, done => Assert.Matches("-[0-9a-f]{8}-\\d{8}T\\d{9}Z$", done.To));
        Assert.All(outcome.Cleared, done => Assert.NotNull(TreeSetAside.MovedAt(Path.GetFileName(done.To))));

        // A destination already there is no place to move to: nothing is replaced, and the folder stays.
        var (c, treeC) = ("", Tree(home, "s-3333cccc"));
        c = ToolHomeLeftovers.Flatten(treeC);
        Project(projects, c, Old);
        var worldC = WorldOf(home, Path.GetDirectoryName(projects)!, ToolHomeLeftovers.Origins(home, Records(Record("c", treeC))));
        var taken = Path.Combine(DaorisHome.Entry(home, HomeEntry.Trees), ".tidied", "tool-home", "projects", "fixed-leaf");
        Directory.CreateDirectory(taken);
        File.WriteAllText(Path.Combine(taken, "mine.txt"), "x");
        var refused = ToolHomeLeftovers.Clear(worldC, [new ToolAsk(c, ToolHomeLeftovers.List(worldC).Folders.Single().Id)], Now,
            new ToolClearOptions { NameOf = _ => "fixed-leaf" });

        Assert.Equal("not-moved", Assert.Single(refused.Kept).Why);
        Assert.True(Directory.Exists(Path.Combine(projects, c)));
        Assert.Equal("x", File.ReadAllText(Path.Combine(taken, "mine.txt")));
    }

    [Fact]
    public void The_tool_home_is_read_from_the_process_then_the_account_then_the_profile_and_refused_where_it_is_not_the_persons()
    {
        var (home, _, _) = Homes();
        var named = Path.Combine(_root, "named");
        var profile = Path.Combine(_root, "profile");

        Assert.Equal((named, "process", null), ToolHomeLeftovers.OwnHome(name => name == ToolHomeLeftovers.Variable ? named : null, null, profile, home));
        Assert.Equal((named, "account", null), ToolHomeLeftovers.OwnHome(_ => null, name => name == ToolHomeLeftovers.Variable ? named : null, profile, home));
        Assert.Equal((Path.Combine(profile, ".claude"), "default", null), ToolHomeLeftovers.OwnHome(_ => "  ", null, profile, home));
        // A relative one is no place Daoris can name; one inside Daoris's own folder is a Daoris profile, never the person's.
        Assert.Equal((null, "process", "relative"), ToolHomeLeftovers.OwnHome(_ => "rel" + Path.DirectorySeparatorChar + "claude", null, profile, home));
        Assert.Equal((null, "process", "inside-daoris"), ToolHomeLeftovers.OwnHome(_ => Path.Combine(home, "profiles", "work"), null, profile, home));
    }

    [Fact]
    public void A_tool_home_equal_to_Daoris_s_home_or_its_install_is_refused_too()
    {
        var (home, _, _) = Homes();
        var install = Path.GetDirectoryName(home)!;
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris\n");

        Assert.Equal((null, "process", "inside-daoris"), ToolHomeLeftovers.OwnHome(_ => home, null, _root, home));
        Assert.Equal((null, "account", "inside-daoris"), ToolHomeLeftovers.OwnHome(_ => null, _ => install + Path.DirectorySeparatorChar, _root, home));
    }

    [Fact]
    public void A_world_made_by_hand_names_no_tool_home_and_the_wiring_says_why()
    {
        var (home, _, _) = Homes();
        var world = WorldOf(home, null);

        Assert.Null(ToolHomeLeftovers.ToolHomeOf(world).Path);
        Assert.Empty(ToolHomeLeftovers.List(world).Folders);
        var read = SystemWiring.Read(world)["toolHome"]!;
        Assert.Null(read["path"]);
        Assert.Equal("no-tool-home", (string?)read["pathWhy"]);
    }

    [Fact]
    public void The_wiring_answers_the_listed_folders_with_identity_a_size_and_a_time_only_once_measured()
    {
        var tree = Tree(Homes().Home, "s-dddd4444");
        var (world, projects, _) = Mixed(Record("a", tree));
        world = world with { Sizes = new WiringSizes() };
        var name = ToolHomeLeftovers.Flatten(tree);
        Project(projects, name, Old);

        var before = SystemWiring.Read(world)["toolHome"]!["folders"]!.AsArray().Single()!;
        Assert.Null(before["bytes"]);
        Assert.Equal("not-measured", (string?)before["bytesWhy"]);
        Assert.False(string.IsNullOrEmpty((string?)before["id"]));

        world.Sizes.MeasureNow(world.Home!, world.Home, WiringBudget.Default, CancellationToken.None, ToolHomeLeftovers.List(world).Folders);
        var after = SystemWiring.Read(world)["toolHome"]!["folders"]!.AsArray().Single()!;
        Assert.Equal(10, (long?)after["bytes"]);
        Assert.NotNull(after["newest"]);
    }
}
