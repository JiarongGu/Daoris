namespace Daoris.Driver.Tests.Platform;

/// <summary>
/// The project folders Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3): listed as Daoris's by
/// the folder names Daoris's own working roots make, never the person's own, and removed only by the person's press, each judged
/// again. 🔴 Every home here is a scratch one handed in: no test reads or touches the real tool home or the real Daoris home.
/// </summary>
public sealed class ToolHomeLeftoversTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-toolhome-tests", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

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

    private static WiringWorld WorldOf(string home, string? tool, Func<string, string?>? process = null) => new()
    {
        Home = home,
        BaseDirectory = home,
        Process = process ?? (_ => null),
        ToolHome = tool,
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

    private static readonly TimeSpan Old = TimeSpan.FromHours(3);

    /// <summary>A mixed tool home: Daoris's tree, room and intake folders, and three the person's own.</summary>
    private (WiringWorld World, string Projects, string[] Mine, string[] Theirs) Mixed()
    {
        var (home, tool, projects) = Homes();
        string[] mine =
        [
            ToolHomeLeftovers.Flatten(DaorisHome.Entry(home, HomeEntry.Trees, "main", "repo", "s-1a2b3c4d")),
            ToolHomeLeftovers.Flatten(DaorisHome.Entry(home, HomeEntry.HelpRoom)),
            ToolHomeLeftovers.Flatten(DaorisHome.Entry(home, HomeEntry.Intake, "q-9")),
        ];
        string[] theirs =
        [
            ToolHomeLeftovers.Flatten(Path.Combine(_root, "person", "dev", "my-app")),
            // Beside Daoris's root, not under one: the home's own name is no working root.
            ToolHomeLeftovers.Flatten(home) + "-other",
            // A name that only starts like a root (no separator after it).
            ToolHomeLeftovers.Flatten(Path.Combine(home, "treesx")),
        ];
        foreach (var name in mine.Concat(theirs)) Project(projects, name, Old);
        return (WorldOf(home, tool), projects, mine, theirs);
    }

    [Fact]
    public void Flatten_keeps_letters_and_digits_and_turns_every_other_character_into_a_dash()
    {
        // Example paths are spelled in pieces, so no machine path sits in a tracked file.
        Assert.Equal("C--Work-people-my-app-2", ToolHomeLeftovers.Flatten("C:" + @"\Work\people\my.app_2"));
        Assert.Equal("-srv-people-x", ToolHomeLeftovers.Flatten("/" + "srv/people/x"));
    }

    [Fact]
    public void List_names_only_the_folders_Daoris_sessions_made_from_a_mixed_home()
    {
        var (world, _, mine, theirs) = Mixed();

        var listed = ToolHomeLeftovers.List(world);

        Assert.Equal(mine.Order(StringComparer.Ordinal), listed.Select(folder => folder.Name).Order(StringComparer.Ordinal));
        foreach (var name in theirs) Assert.DoesNotContain(listed, folder => folder.Name == name);
    }

    [Fact]
    public void List_is_empty_where_there_is_no_projects_folder_or_no_home()
    {
        var (home, tool, projects) = Homes();
        Directory.Delete(projects);
        Assert.Empty(ToolHomeLeftovers.List(WorldOf(home, tool)));
        Assert.Empty(ToolHomeLeftovers.List(new WiringWorld { Home = null, BaseDirectory = _root, Process = _ => null, ToolHome = tool }));
    }

    [Fact]
    public void The_tool_home_is_the_variable_the_person_set_else_the_profile_folder()
    {
        var named = Path.Combine(_root, "named");
        var profile = Path.Combine(_root, "profile");

        Assert.Equal((named, "variable"), ToolHomeLeftovers.OwnHome(name => name == ToolHomeLeftovers.Variable ? named : null, null, profile));
        // The account's stored variable counts where this process's names none: it is what a new terminal gets.
        Assert.Equal((named, "variable"), ToolHomeLeftovers.OwnHome(_ => null, name => name == ToolHomeLeftovers.Variable ? named : null, profile));
        Assert.Equal((Path.Combine(profile, ".claude"), "default"), ToolHomeLeftovers.OwnHome(_ => "  ", null, profile));
    }

    [Fact]
    public void A_world_made_by_hand_names_no_tool_home_and_reads_none()
    {
        var (home, _, _) = Homes();
        var world = WorldOf(home, null);

        Assert.Null(ToolHomeLeftovers.ToolHomeOf(world).Path);
        Assert.Empty(ToolHomeLeftovers.List(world));
        var read = SystemWiring.Read(world)["toolHome"]!;
        Assert.Null(read["path"]);
        Assert.Equal("no-tool-home", (string?)read["pathWhy"]);
    }

    [Fact]
    public void The_wiring_answers_the_listed_folders_with_a_size_and_a_time_only_once_measured()
    {
        var (world, _, mine, _) = Mixed();
        world = world with { Sizes = new WiringSizes() };

        var before = SystemWiring.Read(world)["toolHome"]!;
        var row = before["folders"]!.AsArray().Single(each => (string?)each!["name"] == mine[0])!;
        Assert.Null(row["bytes"]);
        Assert.Equal("not-measured", (string?)row["bytesWhy"]);

        world.Sizes.MeasureNow(world.Home!, world.Home, WiringBudget.Default, CancellationToken.None, ToolHomeLeftovers.List(world));
        var after = SystemWiring.Read(world)["toolHome"]!["folders"]!.AsArray().Single(each => (string?)each!["name"] == mine[0])!;
        Assert.Equal(10, (long?)after["bytes"]);
        Assert.NotNull(after["newest"]);
    }

    [Fact]
    public void Measure_gives_each_folder_its_bytes_and_its_newest_file()
    {
        var (world, projects, mine, _) = Mixed();
        File.WriteAllBytes(Path.Combine(projects, mine[0], "more.jsonl"), new byte[5]);
        File.SetLastWriteTimeUtc(Path.Combine(projects, mine[0], "more.jsonl"), (Now - TimeSpan.FromHours(1)).UtcDateTime);

        var measured = ToolHomeLeftovers.Measure(ToolHomeLeftovers.List(world), WiringBudget.Default, CancellationToken.None);

        Assert.Equal(15, measured[mine[0]].Bytes);
        Assert.Equal(Now - TimeSpan.FromHours(1), measured[mine[0]].Newest);
        Assert.Equal(10, measured[mine[1]].Bytes);
    }

    [Fact]
    public void Clear_removes_exactly_the_folders_named_and_leaves_the_persons_own()
    {
        var (world, projects, mine, theirs) = Mixed();

        var outcome = ToolHomeLeftovers.Clear(world, [mine[0], mine[2]], Now);

        Assert.Equal([mine[0], mine[2]], outcome.Cleared.Select(done => done.Name));
        Assert.Equal(10, outcome.Cleared[0].Bytes);
        Assert.Empty(outcome.Kept);
        Assert.False(Directory.Exists(Path.Combine(projects, mine[0])));
        Assert.True(Directory.Exists(Path.Combine(projects, mine[1])));
        Assert.False(Directory.Exists(Path.Combine(projects, mine[2])));
        foreach (var name in theirs) Assert.True(Directory.Exists(Path.Combine(projects, name)));
    }

    [Fact]
    public void Clear_refuses_a_folder_that_does_not_match_Daoris_whatever_the_press_names()
    {
        var (world, projects, _, theirs) = Mixed();

        var outcome = ToolHomeLeftovers.Clear(world, [theirs[0], theirs[1], "..", Path.Combine("a", "b"), "gone"], Now);

        Assert.Empty(outcome.Cleared);
        Assert.All(outcome.Kept, kept => Assert.Equal("not-listed", kept.Why));
        Assert.Equal(5, outcome.Kept.Count);
        Assert.True(Directory.Exists(Path.Combine(projects, theirs[0])));
        Assert.True(Directory.Exists(Path.Combine(projects, theirs[1])));
    }

    [Fact]
    public void Clear_judges_each_again_and_keeps_one_a_session_wrote_in_lately()
    {
        var (world, projects, mine, _) = Mixed();
        // The list showed it; a session wrote into it since.
        File.SetLastWriteTimeUtc(Path.Combine(projects, mine[1], "session.jsonl"), (Now - TimeSpan.FromMinutes(2)).UtcDateTime);

        var outcome = ToolHomeLeftovers.Clear(world, [mine[0], mine[1]], Now);

        Assert.Equal([mine[0]], outcome.Cleared.Select(done => done.Name));
        var kept = Assert.Single(outcome.Kept);
        Assert.Equal((mine[1], "in-use"), (kept.Name, kept.Why));
        Assert.True(Directory.Exists(Path.Combine(projects, mine[1])));
    }

    [Fact]
    public void Clear_never_follows_a_link_out_of_the_projects_folder()
    {
        var (world, projects, mine, _) = Mixed();
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "keep.txt"), "x");
        var link = Path.Combine(projects, mine[0] + "-link");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return; // A machine that cannot make a link has nothing to prove here.
        }

        var listed = ToolHomeLeftovers.List(world);
        var outcome = ToolHomeLeftovers.Clear(world, [Path.GetFileName(link)], Now);

        Assert.DoesNotContain(listed, folder => folder.Name == Path.GetFileName(link));
        Assert.Empty(outcome.Cleared);
        Assert.True(File.Exists(Path.Combine(outside, "keep.txt")));
    }
}
