using System.Text.Json;

namespace Daoris.Driver.Tests.Platform;

/// <summary>
/// The folders Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3), LISTED and nothing more: a folder
/// is Daoris's only where its name EQUALS a working directory in which one of Daoris's own session records shows a session ran;
/// a room no record shows a session in claims nothing; what may still be used is said. There is no press (OWNSPACE2e).
/// 🔴 Every home here is a scratch one handed in: no test reads or touches the real tool home or Daoris home.
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

    private static object Record(string id, string? tree, string state = "completed", string? profile = "work", string repository = "repo", string? ask = null, string workspace = "main") =>
        new { id, tree, state, profile, repository, ask, workspace };

    private static string Records(params object[] rows) => JsonSerializer.Serialize(rows);

    [Fact]
    public void Flatten_keeps_letters_and_digits_and_turns_every_other_character_into_a_dash()
    {
        // Example paths are spelled in pieces, so no machine path sits in a tracked file.
        Assert.Equal("C--Work-people-my-app-2", ToolHomeLeftovers.Flatten("C:" + @"\Work\people\my.app_2"));
        Assert.Equal("-srv-people-x", ToolHomeLeftovers.Flatten("/" + "srv/people/x"));
    }

    [Fact]
    public void List_names_only_the_exact_working_directories_Daoris_s_records_show_a_session_ran_in_and_the_records_that_name_them()
    {
        var (home, tool, projects) = Homes();
        var treeA = Tree(home, "s-1a2b3c4d");
        var treeB = Tree(home, "s-5e6f7a8b");
        var help = HelpRoom.PathOf(home);
        var origins = ToolHomeLeftovers.Origins(home, Records(
            Record("a", treeA), Record("b", treeB, profile: null, state: "failed"), Record("h", null, repository: "daoris:help", workspace: "default")));
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
        Assert.Equal(["a"], listing.Folders.Single(folder => folder.Name == mine[0]).By);
        Assert.Equal(["h"], listing.Folders.Single(folder => folder.Name == mine[2]).By);
    }

    /// <summary>A help record's tree is its own room (HOMEROOMS1): a named workspace's room folder is claimed for it, not the default room's.</summary>
    [Fact]
    public void A_named_workspace_s_help_room_is_claimed_for_its_own_record_alone()
    {
        var (home, tool, projects) = Homes();
        var alpha = HelpRoom.PathOf(home, "alpha");
        var origins = ToolHomeLeftovers.Origins(home, JsonSerializer.Serialize(new object[]
        {
            new { id = "h1", repository = "daoris:help", state = "completed", profile = "work", workspace = "alpha" },
            new { id = "h2", repository = "daoris:help", state = "completed", profile = "work" },
        }));
        Project(projects, ToolHomeLeftovers.Flatten(alpha), Old);
        Project(projects, ToolHomeLeftovers.Flatten(HelpRoom.PathOf(home)), Old);

        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, origins));

        Assert.Equal(["h1"], listing.Folders.Single(folder => folder.Name == ToolHomeLeftovers.Flatten(alpha)).By);
        Assert.Equal(["h2"], listing.Folders.Single(folder => folder.Name == ToolHomeLeftovers.Flatten(HelpRoom.PathOf(home))).By);
    }

    [Fact]
    public void A_room_no_record_shows_a_session_in_claims_nothing_so_a_look_alike_folder_is_not_Daoris_s()
    {
        var (home, tool, projects) = Homes();
        // The help room and an intake room are on the disk, and the person has a `daoris-help`-like folder; no record of use.
        Directory.CreateDirectory(HelpRoom.PathOf(home));
        Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.Intake, "main"));
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", Tree(home, "s-00001111"))));
        Project(projects, ToolHomeLeftovers.Flatten(HelpRoom.PathOf(home)), Old);
        Project(projects, ToolHomeLeftovers.Flatten(DaorisHome.Entry(home, HomeEntry.Intake, "main")), Old);

        Assert.DoesNotContain(origins!, origin => origin.Source is "help" or "intake");
        Assert.Empty(ToolHomeLeftovers.List(WorldOf(home, tool, origins)).Folders);
    }

    [Fact]
    public void Where_the_records_could_not_be_read_nothing_is_claimed_and_the_list_says_why()
    {
        var (home, tool, projects) = Homes();
        Project(projects, ToolHomeLeftovers.Flatten(HelpRoom.PathOf(home)), Old);

        Assert.Null(ToolHomeLeftovers.Origins(home, null));
        Assert.Null(ToolHomeLeftovers.Origins(home, "{ not json"));
        var listing = ToolHomeLeftovers.List(WorldOf(home, tool, null));

        Assert.Empty(listing.Folders);
        Assert.Equal("records-unread", listing.Why);
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
    public void A_session_that_is_still_live_is_listed_as_one_that_would_stay_whatever_its_age()
    {
        var (home, tool, projects) = Homes();
        var tree = Tree(home, "s-11112222");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("a", tree, state: "awaiting-person")));
        Project(projects, ToolHomeLeftovers.Flatten(tree), TimeSpan.FromDays(30));

        Assert.Equal("session", Assert.Single(ToolHomeLeftovers.List(WorldOf(home, tool, origins)).Folders).Keeps);
    }

    [Fact]
    public void A_legacy_record_with_no_account_and_a_kept_conversation_would_stay_but_an_account_s_would_not()
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
    public void A_conversation_record_that_cannot_be_read_is_not_absent_and_marks_the_folder()
    {
        var (home, tool, projects) = Homes();
        var legacy = Tree(home, "s-dddd7777");
        Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.Sessions));
        File.WriteAllText(new HarnessConversations(home).PathOf("legacy"), "{ this is not json");
        var origins = ToolHomeLeftovers.Origins(home, Records(Record("legacy", legacy, profile: null)));
        Project(projects, ToolHomeLeftovers.Flatten(legacy), Old);

        Assert.Equal("record-unread", Assert.Single(ToolHomeLeftovers.List(WorldOf(home, tool, origins)).Folders).Keeps);
    }

    [Fact]
    public void An_ancestor_that_is_a_link_lists_nothing_under_it()
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
        Project(Path.Combine(real, ".claude", "projects"), ToolHomeLeftovers.Flatten(tree), Old);
        var listing = ToolHomeLeftovers.List(WorldOf(home, Path.Combine(linked, ".claude"), ToolHomeLeftovers.Origins(home, Records(Record("a", tree)))));

        Assert.Equal("linked", listing.Why);
        Assert.Empty(listing.Folders);
    }

    [Fact]
    public void A_walk_that_could_not_read_a_folder_is_partial_and_not_a_whole_measure()
    {
        Assert.True(WiringSizes.MeasureFolder(Path.Combine(_root, "missing"), WiringBudget.Default, CancellationToken.None).Partial);
        var (home, _, projects) = Homes();
        var folder = Project(projects, "x", Old);
        File.WriteAllBytes(Path.Combine(folder, "two.jsonl"), new byte[3]);
        Assert.True(WiringSizes.MeasureFolder(folder, new WiringBudget(1, TimeSpan.FromMinutes(1)), CancellationToken.None).Partial);
        Assert.False(WiringSizes.MeasureFolder(folder, WiringBudget.Default, CancellationToken.None).Partial);
        Assert.NotNull(home);
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
    public void The_wiring_answers_the_listed_folders_with_their_records_a_size_and_a_time_only_once_measured()
    {
        var (home, tool, projects) = Homes();
        var tree = Tree(home, "s-dddd4444");
        var world = WorldOf(home, tool, ToolHomeLeftovers.Origins(home, Records(Record("rec-1", tree)))) with { Sizes = new WiringSizes() };
        Project(projects, ToolHomeLeftovers.Flatten(tree), Old);

        var before = SystemWiring.Read(world)["toolHome"]!["folders"]!.AsArray().Single()!;
        Assert.Null(before["bytes"]);
        Assert.Equal("not-measured", (string?)before["bytesWhy"]);
        Assert.Equal("rec-1", (string?)before["by"]![0]);

        world.Sizes.MeasureNow(world.Home!, world.Home, WiringBudget.Default, CancellationToken.None, ToolHomeLeftovers.List(world).Folders);
        var after = SystemWiring.Read(world)["toolHome"]!["folders"]!.AsArray().Single()!;
        Assert.Equal(10, (long?)after["bytes"]);
        Assert.NotNull(after["newest"]);
    }
}
