using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Where every machine-local file lives (D63): one home, named by <c>DAORIS_HOME</c>, and no default
/// under the user profile. This is the service side's copy of a three-way twin — the CLI and the
/// driver hold the same contract in their own languages, sharing no code, the way the remotes map's
/// three copies do (WSP3). The install rows (SERVICEHOME2) are the driver's <c>DaorisHomeTests</c>' FINDDAORIS1 rows, case
/// for case, with the service's own folders.
/// </summary>
public sealed class DaorisHomeTests
{
    private static Func<string, string?> Env(params (string Name, string? Value)[] pairs) =>
        name => pairs.FirstOrDefault(p => p.Name == name).Value;

    [Fact]
    public void The_home_is_the_variable_and_nothing_else()
    {
        Assert.Equal("D:/somewhere/data", DaorisHome.Resolve(Env(("DAORIS_HOME", "D:/somewhere/data"))));
        Assert.Null(DaorisHome.Resolve(Env()));
        Assert.Null(DaorisHome.Resolve(Env(("DAORIS_HOME", "   "))));
    }

    /// <summary>🔴 No default under the user profile — that is the decision, not an omission.</summary>
    [Fact]
    public void Absent_it_is_absent_rather_than_the_user_profile()
    {
        var home = DaorisHome.Resolve(Env());
        Assert.Null(home);
        Assert.DoesNotContain(".daoris", DaorisHome.Sentence);
        Assert.Contains("DAORIS_HOME", DaorisHome.Sentence);
    }

    [Fact]
    public void An_entry_asked_of_the_environment_is_the_entry_or_none_with_no_home()
    {
        Assert.Equal(
            Path.Combine("X:/daoris", "data", "knowledge.db"),
            DaorisHome.File(Env(("DAORIS_HOME", "X:/daoris")), HomeEntry.Knowledge));
        Assert.Null(DaorisHome.File(Env(), HomeEntry.Knowledge));
    }

    /// <summary>
    /// 🔴 D172 point 2 (HOMELAYOUT3): a home names its Daoris folder, and the old home its entries were under. The driver's
    /// <c>A_home_names_its_folder</c>, whose summary says each column, row for row; the CLI's <c>home.test.ts</c> holds both.
    /// </summary>
    [Theory]
    [InlineData("install", "data", "", "driver", "", "data/driver.json")]
    [InlineData("install", "", "", "driver", "", "data/driver.json")]
    [InlineData("install", "data", "", "keys", "", "profiles/keys.json")]
    [InlineData("install", "data", "data/keys.json", "keys", "", "data/keys.json")]
    [InlineData("install", "", "data/keys.json", "keys", "", "data/keys.json")]
    [InlineData("install", "data", "data/keys.json,profiles/keys.json", "keys", "", "refused")]
    [InlineData("install", "data", "data/logs/desktop.log", "logs", "", "data/logs")]
    [InlineData("install", "data", "", "logs", "", "logs")]
    [InlineData("install", "data", "data/chromium/", "window", "", "data")]
    [InlineData("install", "data", "", "window", "", "profiles/window")]
    [InlineData("install", "data", "data/harnesses/claude-code/work/x", "agent-homes", "claude-code/work", "data/harnesses/claude-code/work")]
    [InlineData("install", "data", "data/harnesses/claude-code/work/x", "agent-homes", "claude-code/new", "profiles/agents/claude-code/new")]
    [InlineData("install", "data", "", "home-lease", "", "run/home.lease")]
    [InlineData("root", "", "", "driver", "", "data/driver.json")]
    [InlineData("root", "", "driver.json", "driver", "", "driver.json")]
    [InlineData("root", "", "", "running", "", "run/running.json")]
    [InlineData("root", "", "", "logs", "", "logs")]
    [InlineData("root", "data", "", "driver", "", "data/data/driver.json")]
    public void A_home_names_its_folder(string shape, string homeAs, string present, string name, string asked, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "daoris-home-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            if (shape == "install") File.WriteAllText(Path.Combine(root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
            Lay(root, present);
            var home = homeAs.Length == 0 ? root : Path.Combine(root, homeAs);
            var entry = HomeEntry.Named(name)!;
            var asking = asked.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (expected == "refused")
            {
                var refusal = Assert.Throws<HomeEntryTwiceException>(() => DaorisHome.Entry(home, entry, asking));
                Assert.Equal(DaorisHome.Places(home, entry, asking), [refusal.New, refusal.Old]);
                return;
            }

            Assert.Equal(
                Path.GetFullPath(Path.Combine([root, .. expected.Split('/', StringSplitOptions.RemoveEmptyEntries)])),
                Path.GetFullPath(DaorisHome.Entry(home, entry, asking)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The home a file of it is in: the driver's <c>A_home_is_found_from_its_entry_at_either_place</c>, row for row; the CLI's
    /// <c>home.test.ts</c> holds both.
    /// </summary>
    [Theory]
    [InlineData("install", "data/driver.json", "driver", "data")]
    [InlineData("install", "data/harnesses.json", "harnesses", "data")]
    [InlineData("root", "data/driver.json", "driver", "")]
    [InlineData("root", "driver.json", "driver", "")]
    [InlineData("install", "profiles/browser/engine", "browser-engine", "data")]
    [InlineData("install", "data/browser/engine", "browser-engine", "data")]
    [InlineData("root", "profiles/browser/engine", "browser-engine", "")]
    [InlineData("root", "browser/engine", "browser-engine", "")]
    public void A_home_is_found_from_its_entry_at_either_place(string shape, string file, string name, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "daoris-home-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            if (shape == "install") File.WriteAllText(Path.Combine(root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
            var path = Path.Combine([root, .. file.Split('/')]);
            Assert.Equal(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine([root, .. expected.Split('/', StringSplitOptions.RemoveEmptyEntries)]))),
                DaorisHome.HomeOf(path, HomeEntry.Named(name)!));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>🔴 D172 §3 switched on (HOMELAYOUT3): every new place is under the top folder its category names, as the driver's.</summary>
    [Fact]
    public void Each_new_place_is_under_its_categorys_folder()
    {
        Assert.All(HomeEntry.Table, entry =>
        {
            var folder = entry.Category.ToString().ToLowerInvariant();
            Assert.True(entry.New == folder || entry.New.StartsWith(folder + "/", StringComparison.Ordinal), $"{entry.Name}: {entry.New}");
        });
    }

    /// <summary>A row's disk, as the driver's rows lay it: <c>{}</c> for JSON and a letter otherwise, what follows <c>=</c>, a trailing <c>/</c> a folder.</summary>
    private static void Lay(string root, string present)
    {
        foreach (var item in present.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var (file, text) = item.IndexOf('=') is var at and >= 0
                ? (item[..at], item[(at + 1)..])
                : (item, item.EndsWith(".json", StringComparison.Ordinal) ? "{}" : "x");
            var path = Path.Combine([root, .. file.Split('/', StringSplitOptions.RemoveEmptyEntries)]);
            if (file.EndsWith('/'))
            {
                Directory.CreateDirectory(path);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
    }

    /// <summary>
    /// 🔴 D172 point 5 (HOMELAYOUT2, the install layout design §6.2) with its review note: the driver's
    /// <c>An_entry_is_where_it_is</c>, whose summary says each column, row for row. The CLI's <c>home.test.ts</c> holds these
    /// rows and the driver's to its own, cell for cell.
    /// </summary>
    [Theory]
    [InlineData("a.json", "a.json", "", "", "", "a.json")]
    [InlineData("a.json", "a.json", "", "a.json", "", "a.json")]
    [InlineData("data/a.json", "a.json", "", "", "", "data/a.json")]
    [InlineData("data/a.json", "a.json", "", "a.json", "", "a.json")]
    [InlineData("data/a.json", "a.json", "", "data/a.json", "", "data/a.json")]
    [InlineData("data/a.json", "a.json", "", "a.json,data/a.json", "", "refused")]
    [InlineData("data/a.json", "a.json", "", "a.json,data/a.json=torn", "", "a.json")]
    [InlineData("data/a.json", "a.json", "", "a.json=torn,data/a.json", "", "data/a.json")]
    [InlineData("data/a.json", "a.json", "", "a.json=torn", "", "a.json")]
    [InlineData("data/k.db", "k.db", "", "k.db,data/k.db=", "", "k.db")]
    [InlineData("data/s", "s", "", "s/x.log", "x.log", "s/x.log")]
    [InlineData("data/s", "s", "", "s/x.log", "y.log", "s/y.log")]
    [InlineData("data/s", "s", "", "s/x.log,data/s/", "y.log", "s/y.log")]
    [InlineData("data/s", "s", "", "s/x.log,data/s/z.log", "", "refused")]
    [InlineData("work/t/*/*", "t/*/*", "", "t/a/one/HEAD", "a/one", "t/a/one")]
    [InlineData("work/t/*/*", "t/*/*", "", "t/a/one/HEAD", "a/two", "work/t/a/two")]
    [InlineData("work/t/*/*", "t/*/*", "", "t/a/one/HEAD,work/t/a/one/HEAD", "a/one", "refused")]
    [InlineData("work/t/*/*", "t/*/*", "", "t/a/one/HEAD", "a/one/notes.md", "t/a/one/notes.md")]
    [InlineData("work/t/*/*", "t/*/*", "", "t/a/one/HEAD", "a", "t/a")]
    [InlineData("work/t/*/*", "t/*/*", "", "t/a/one/HEAD,work/t/a/two/HEAD", "a", "work/t/a")]
    [InlineData("work/f/*", "s/*/files", "", "s/one/files/a.png", "one/b.png", "s/one/files/b.png")]
    [InlineData("work/f/*", "s/*/files", "", "", "one", "work/f/one")]
    [InlineData("run/s/*.pid", "s/*.pid", "", "s/one.pid", "one", "s/one.pid")]
    [InlineData("work/rooms/help", "help", "CLAUDE.md", "help/proposals/p.json", "", "work/rooms/help")]
    [InlineData("work/rooms/help", "help", "CLAUDE.md", "help/CLAUDE.md", "", "help")]
    [InlineData("work/rooms/help", "help", "CLAUDE.md", "help/CLAUDE.md,work/rooms/help/notes.md", "", "help")]
    [InlineData("profiles/window", "", "chromium|config", "config/window-state.json", "", "")]
    [InlineData("profiles/window", "", "chromium|config", "", "", "profiles/window")]
    public void An_entry_is_where_it_is(string next, string old, string holds, string present, string asked, string expected)
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-home-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            foreach (var item in present.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var (file, text) = item.IndexOf('=') is var at and >= 0
                    ? (item[..at], item[(at + 1)..])
                    : (item, item.EndsWith(".json", StringComparison.Ordinal) ? "{}" : "x");
                var path = Path.Combine([home, .. file.Split('/', StringSplitOptions.RemoveEmptyEntries)]);
                if (file.EndsWith('/'))
                {
                    Directory.CreateDirectory(path);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }

            var entry = new HomeEntry("probe", HomeCategory.Data, next, old, holds.Split('|', StringSplitOptions.RemoveEmptyEntries));
            var asking = asked.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (expected == "refused")
            {
                var refusal = Assert.Throws<HomeEntryTwiceException>(() => DaorisHome.Entry(home, entry, asking));
                Assert.Equal(DaorisHome.Places(home, entry, asking), [refusal.New, refusal.Old]);
                Assert.Equal(string.Format(DaorisHome.Twice, "probe", refusal.New, refusal.Old), refusal.Message);
                return;
            }

            Assert.Equal(
                Path.Combine([home, .. expected.Split('/', StringSplitOptions.RemoveEmptyEntries)]),
                DaorisHome.Entry(home, entry, asking));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>A list of an entry's items reads both places: the driver's <c>A_list_reads_both_places</c>, row for row.</summary>
    [Theory]
    [InlineData("a.json", "a.json", "", "a.json")]
    [InlineData("data/a.json", "a.json", "", "data/a.json|a.json")]
    [InlineData("work/t/*/*", "t/*/*", "", "work/t|t")]
    [InlineData("work/t/*/*", "t/*/*", "a", "work/t/a|t/a")]
    [InlineData("work/f/*", "s/*/files", "one", "work/f/one|s/one/files")]
    [InlineData("run/s/*.pid", "s/*.pid", "", "run/s|s")]
    public void A_list_reads_both_places(string next, string old, string asked, string expected)
    {
        var home = Path.Combine(AppContext.BaseDirectory, "places");
        var entry = new HomeEntry("probe", HomeCategory.Data, next, old);
        Assert.Equal(
            expected.Split('|').Select(place => Path.Combine([home, .. place.Split('/')])),
            DaorisHome.Places(home, entry, asked.Split('/', StringSplitOptions.RemoveEmptyEntries)));
    }

    /// <summary>D172's table, one entry each: a name says one entry, and an entry is found by it.</summary>
    [Fact]
    public void Each_entry_is_named_once_and_found_by_its_name()
    {
        Assert.Equal(HomeEntry.Table.Count, HomeEntry.Table.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(HomeEntry.Table, entry => Assert.Same(entry, HomeEntry.Named(entry.Name)));
        Assert.Null(HomeEntry.Named("nothing"));
    }

    /// <summary>USAGECAP1a1 (D179 point 2): the driver's usage history, a data folder the service's table names as its twin does.</summary>
    [Fact]
    public void The_usage_history_is_a_data_folder_of_its_own()
    {
        Assert.Same(HomeEntry.UsageHistory, HomeEntry.Named("usage-history"));
        Assert.Equal(HomeCategory.Data, HomeEntry.UsageHistory.Category);
    }

    /// <summary>KEEP1a (D195 point 2): the record of the keeping rules' runs, a data file the service's table names as its twins do.</summary>
    [Fact]
    public void The_keeping_record_is_a_data_file()
    {
        Assert.Same(HomeEntry.Keeping, HomeEntry.Named("keeping"));
        Assert.Equal((HomeCategory.Data, "data/keeping.json"), (HomeEntry.Keeping.Category, HomeEntry.Keeping.New));
    }

    /// <summary>NOTIFY2f (D196 point 6): the notices' record, a data file the service's table names as its twins do.</summary>
    [Fact]
    public void The_notices_record_is_a_data_file()
    {
        Assert.Same(HomeEntry.Notices, HomeEntry.Named("notices"));
        Assert.Equal((HomeCategory.Data, "data/notices.json"), (HomeEntry.Notices.Category, HomeEntry.Notices.New));
    }

    /// <summary>TERMTRUST1a (D201 point 6): the standing confirmation, a data file the service's table names as its twins do, and never reads.</summary>
    [Fact]
    public void The_standing_confirmation_is_a_data_file()
    {
        Assert.Same(HomeEntry.StandingConfirmation, HomeEntry.Named("standing-confirmation"));
        Assert.Equal(
            (HomeCategory.Data, "data/standing-confirmation.json"),
            (HomeEntry.StandingConfirmation.Category, HomeEntry.StandingConfirmation.New));
    }

    /// <summary>MODELROLE1c2 (D205 §9.2): what each agent last offered, a data file the service's table names as its twins do, and never reads.</summary>
    [Fact]
    public void The_agent_offers_are_a_data_file()
    {
        Assert.Same(HomeEntry.AgentOffers, HomeEntry.Named("agent-offers"));
        Assert.Equal((HomeCategory.Data, "data/agent-offers.json"), (HomeEntry.AgentOffers.Category, HomeEntry.AgentOffers.New));
    }

    /// <summary>HELPMODE1c2a (D183's note): Ask Daoris's configured access, a data file the service's table names as its twin does.</summary>
    [Fact]
    public void Ask_Daoris_configured_access_is_a_data_file()
    {
        Assert.Same(HomeEntry.ConfiguredAccess, HomeEntry.Named("configured-access"));
        Assert.Equal(HomeCategory.Data, HomeEntry.ConfiguredAccess.Category);
    }

    /// <summary>
    /// 🔴 SERVICEHOME2 (D171 point 1), the driver's FINDDAORIS1 rows: a connector a person's own agent starts from its
    /// <c>.mcp.json</c>, or the HTTP host started from a terminal, is handed no variable, so a program the install carries
    /// finds its home where it runs: the install's own <c>data</c> folder, beside the <c>app</c> folder it runs from, however
    /// deep inside it (the connector is <c>app/daoris-knowledge/</c>). A workspace build is no install, and finds none.
    /// </summary>
    [Fact]
    public void A_program_an_install_carries_finds_the_installs_data_folder()
    {
        var install = Install();
        try
        {
            var data = Path.Combine(install, "data");
            Assert.Equal(data, DaorisHome.OfInstall(Path.Combine(install, "app")));
            Assert.Equal(data, DaorisHome.OfInstall(Path.Combine(install, "app", "daoris-knowledge") + Path.DirectorySeparatorChar));
            Assert.Equal(data, DaorisHome.OfInstall(Path.Combine(install, "app", "daoris-knowledge-http")));

            // The variable still wins: the dev loop, a rehearsal, a second home.
            Assert.Equal("D:/somewhere/data", DaorisHome.Resolve(Env(("DAORIS_HOME", "D:/somewhere/data")), Path.Combine(install, "app")));
            Assert.Equal(data, DaorisHome.Resolve(Env(), Path.Combine(install, "app", "daoris-knowledge")));

            // An `app` folder with no marker above it is not an install, nor is the install's root itself.
            File.Delete(Path.Combine(install, "INSTALLED.md"));
            Assert.Null(DaorisHome.OfInstall(Path.Combine(install, "app", "daoris-knowledge")));
            Assert.Null(DaorisHome.Resolve(Env(), Path.Combine(install, "app", "daoris-knowledge")));
        }
        finally
        {
            Directory.Delete(install, recursive: true);
        }
    }

    [Fact]
    public void A_workspace_build_finds_no_install()
    {
        Assert.Null(DaorisHome.OfInstall(AppContext.BaseDirectory));
        Assert.Null(DaorisHome.Resolve(Env(), AppContext.BaseDirectory));
    }

    /// <summary>
    /// A host takes the install's home as its own process's <c>DAORIS_HOME</c> where none is named, as the desktop does
    /// (D63), so its index, its machine log and every file it keeps read the same machine; a home already named is left.
    /// </summary>
    [Fact]
    public void Adopting_the_installs_home_sets_it_only_where_none_is_named()
    {
        var install = Install();
        try
        {
            var set = new Dictionary<string, string>();
            var app = Path.Combine(install, "app", "daoris-knowledge");

            Assert.Equal(Path.Combine(install, "data"), DaorisHome.Adopt(Env(), (name, value) => set[name] = value, app));
            Assert.Equal(Path.Combine(install, "data"), set["DAORIS_HOME"]);

            set.Clear();
            Assert.Null(DaorisHome.Adopt(Env(("DAORIS_HOME", "D:/somewhere/data")), (name, value) => set[name] = value, app));
            Assert.Null(DaorisHome.Adopt(Env(), (name, value) => set[name] = value, AppContext.BaseDirectory));
            Assert.Empty(set);
        }
        finally
        {
            Directory.Delete(install, recursive: true);
        }
    }

    /// <summary>
    /// Each host takes its install's home before anything reads one: the connector before its machine log opens, the HTTP
    /// host before its index is named, so the log, the index and the quest files are the install's machine.
    /// </summary>
    [Theory]
    [InlineData("Daoris.Service.Mcp", "using var log = MachineLog.Open(")]
    [InlineData("Daoris.Service.Http", "HostComposition.DefaultDatabasePath()")]
    public void Each_host_takes_its_installs_home_before_it_reads_one(string host, string firstRead)
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", host, "Program.cs"));
        var adopt = program.IndexOf("DaorisHome.Adopt(AppContext.BaseDirectory)", StringComparison.Ordinal);
        var read = program.IndexOf(firstRead, StringComparison.Ordinal);
        Assert.True(adopt > 0 && read > adopt, $"the home taken at {adopt}, first read at {read}");
    }

    /// <summary>An install's shape on disk (D93): the marker at its root, and the service's hosts under <c>app/</c>.</summary>
    private static string Install()
    {
        var install = Path.Combine(Path.GetTempPath(), "daoris-install-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(install, "app", "daoris-knowledge"));
        Directory.CreateDirectory(Path.Combine(install, "app", "daoris-knowledge-http"));
        File.WriteAllText(Path.Combine(install, "INSTALLED.md"), "# Daoris — installed desktop\n");
        return install;
    }

    /// <summary>Walk up to the workspace root — the tests run from `bin/Debug/net10.0`.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
