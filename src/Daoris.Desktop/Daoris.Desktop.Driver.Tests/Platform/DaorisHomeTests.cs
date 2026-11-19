using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where every machine-local file lives: one home, named by <c>DAORIS_HOME</c>, and no default
/// under the user profile. The driver's copy of a three-way twin — the CLI and the service hold the
/// same contract in their own languages, sharing no code.
/// </summary>
public sealed class DaorisHomeTests
{
    /// <summary>The install's own folder and launcher the home compares are the names the launcher's staged build keeps.</summary>
    [Fact]
    public void The_install_names_are_the_staged_builds()
    {
        Assert.Equal(StagedBuild.Folder, DaorisHome.InstallUpdate);
        Assert.Equal(StagedBuild.Launcher, DaorisHome.InstallLauncher);
    }

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
        Assert.Null(DaorisHome.Resolve(Env()));
        Assert.DoesNotContain(".daoris", DaorisHome.Sentence);
        Assert.Contains("DAORIS_HOME", DaorisHome.Sentence);
    }

    /// <summary>
    /// The management class REFUSES rather than guessing: a driver with no home and no per-file
    /// override names what to set, in one sentence, instead of writing somewhere nobody pointed it.
    /// </summary>
    [Fact]
    public void Requiring_a_file_with_no_home_is_a_refusal_that_names_the_variable()
    {
        var error = Assert.Throws<DriverException>(() => DaorisHome.Require(Env(), HomeEntry.DriverConfig));
        Assert.Contains("DAORIS_HOME", error.Message);
        Assert.Contains("driver.json", error.Message);
    }

    /// <summary>
    /// 🔴 A terminal outside Daoris's own panel set nothing, and the install's launcher sets
    /// nothing either, so a program the install carries finds its home where it runs: the install's own <c>data</c>
    /// folder, beside the <c>app</c> folder it runs from, however deep inside it (the driver host is
    /// <c>app/daoris-driver/</c>). A workspace build is no install, and finds none.
    /// </summary>
    [Fact]
    public void A_program_an_install_carries_finds_the_installs_data_folder()
    {
        var install = Install();
        try
        {
            var data = Path.Combine(install, "data");
            Assert.Equal(data, DaorisHome.OfInstall(Path.Combine(install, "app")));
            Assert.Equal(data, DaorisHome.OfInstall(Path.Combine(install, "app", "daoris-driver") + Path.DirectorySeparatorChar));

            // The variable still wins: the dev loop, a rehearsal, a second home.
            Assert.Equal("D:/somewhere/data", DaorisHome.Resolve(Env(("DAORIS_HOME", "D:/somewhere/data")), Path.Combine(install, "app")));
            Assert.Equal(data, DaorisHome.Resolve(Env(), Path.Combine(install, "app", "daoris-driver")));

            // An `app` folder with no marker above it is not an install, nor is the install's root itself.
            File.Delete(Path.Combine(install, "INSTALLED.md"));
            Assert.Null(DaorisHome.OfInstall(Path.Combine(install, "app", "daoris-driver")));
            Assert.Null(DaorisHome.Resolve(Env(), Path.Combine(install, "app", "daoris-driver")));
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
    /// The host takes the install's home as its own process's <c>DAORIS_HOME</c> where none is named, as the desktop does,
    /// so its machine log, its files and every session it starts read the same machine; a home already named is left.
    /// </summary>
    [Fact]
    public void Adopting_the_installs_home_sets_it_only_where_none_is_named()
    {
        var install = Install();
        try
        {
            var set = new Dictionary<string, string>();
            var app = Path.Combine(install, "app", "daoris-driver");

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
    /// 🔴 An entry is at its place under the Daoris folder, whatever is on the disk. A row is an entry's place, its names, what is
    /// asked and where that is (empty: the home itself). A <c>*</c> is an item, filled from what is asked one part each; what is
    /// asked beyond the items is joined under the item, and asked short of them is the folder holding them. The CLI's
    /// <c>home.test.ts</c> holds these rows and the service's to its own, cell for cell.
    /// </summary>
    [Theory]
    [InlineData("data/a.json", "", "", "data/a.json")]
    [InlineData("data/s", "", "x.log", "data/s/x.log")]
    [InlineData("work/t/*/*", "", "a/one", "work/t/a/one")]
    [InlineData("work/t/*/*", "", "a/one/notes.md", "work/t/a/one/notes.md")]
    [InlineData("work/t/*/*", "", "a", "work/t/a")]
    [InlineData("work/t/*/*", "", "", "work/t")]
    [InlineData("work/f/*", "", "one/b.png", "work/f/one/b.png")]
    [InlineData("run/s/*.pid", "", "one", "run/s/one.pid")]
    [InlineData("work/rooms/help", "CLAUDE.md", "", "work/rooms/help")]
    public void An_entry_is_where_it_is(string next, string holds, string asked, string expected)
    {
        var home = Scratch();
        try
        {
            var entry = new HomeEntry("probe", HomeCategory.Data, next, holds.Split('|', StringSplitOptions.RemoveEmptyEntries));
            var asking = asked.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(
                Path.Combine([home, .. expected.Split('/', StringSplitOptions.RemoveEmptyEntries)]),
                DaorisHome.Entry(home, entry, asking));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>
    /// 🔴 A home names its Daoris folder. An install's folder (it holds <c>INSTALLED.md</c>) and its <c>data/</c> name the same
    /// install; any other home is its own Daoris folder, a home named <c>data</c> with no install above it among them. A row is
    /// the shape on the disk (<c>install</c>, the marker at the root, or <c>root</c>), the home as named (empty: the root itself,
    /// or <c>data</c>), the entry by name, what is asked, and where that is under the root. The CLI's <c>home.test.ts</c> holds these
    /// rows and the service's to its own, cell for cell.
    /// </summary>
    [Theory]
    [InlineData("install", "data", "driver", "", "data/driver.json")]
    [InlineData("install", "", "driver", "", "data/driver.json")]
    [InlineData("install", "data", "keys", "", "profiles/keys.json")]
    [InlineData("install", "data", "logs", "", "logs")]
    [InlineData("install", "data", "window", "", "profiles/window")]
    [InlineData("install", "data", "agent-homes", "claude-code/new", "profiles/agents/claude-code/new")]
    [InlineData("install", "data", "home-lease", "", "run/home.lease")]
    [InlineData("root", "", "driver", "", "data/driver.json")]
    [InlineData("root", "", "running", "", "run/running.json")]
    [InlineData("root", "", "logs", "", "logs")]
    [InlineData("root", "data", "driver", "", "data/data/driver.json")]
    public void A_home_names_its_folder(string shape, string homeAs, string name, string asked, string expected)
    {
        var root = Scratch();
        try
        {
            if (shape == "install") File.WriteAllText(Path.Combine(root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
            var home = homeAs.Length == 0 ? root : Path.Combine(root, homeAs);
            var entry = HomeEntry.Named(name)!;
            var asking = asked.Split('/', StringSplitOptions.RemoveEmptyEntries);
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
    /// The home a file of it is in (<c>DriverConfig.HomeOf</c>, <c>HarnessSettings.HomeOf</c>, the in-app browser's
    /// profile): the Daoris folder where the file is at the entry's place, spelled as an install's <c>data/</c> where that folder
    /// is an install, as every install's home is. A row is the shape, the file under
    /// the root, the entry, and the home under the root (empty: the root itself). The CLI's <c>home.test.ts</c> holds these rows.
    /// </summary>
    [Theory]
    [InlineData("install", "data/driver.json", "driver", "data")]
    [InlineData("install", "data/harnesses.json", "harnesses", "data")]
    [InlineData("root", "data/driver.json", "driver", "")]
    [InlineData("install", "profiles/browser/engine", "browser-engine", "data")]
    [InlineData("root", "profiles/browser/engine", "browser-engine", "")]
    public void A_home_is_found_from_its_entry(string shape, string file, string name, string expected)
    {
        var root = Scratch();
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

    /// <summary>
    /// 🔴 Every new place is under the top folder its category names, and the logs are the
    /// <c>logs</c> folder itself, so the Daoris folder holds the seven categories and nothing else of Daoris's at its top.
    /// </summary>
    [Fact]
    public void Each_new_place_is_under_its_categorys_folder()
    {
        Assert.All(HomeEntry.Table, entry =>
        {
            var folder = entry.Category.ToString().ToLowerInvariant();
            Assert.True(entry.New == folder || entry.New.StartsWith(folder + "/", StringComparison.Ordinal), $"{entry.Name}: {entry.New}");
        });
    }

    /// <summary>
    /// The layout's table, one entry each, held cell for cell by the CLI's <c>home.test.ts</c> against the service's and its own. Here:
    /// a name says one entry, and an entry is found by it.
    /// </summary>
    [Fact]
    public void Each_entry_is_named_once_and_found_by_its_name()
    {
        Assert.Equal(HomeEntry.Table.Count, HomeEntry.Table.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(HomeEntry.Table, entry => Assert.Same(entry, HomeEntry.Named(entry.Name)));
        Assert.Null(HomeEntry.Named("nothing"));
    }

    /// <summary>
    /// Every usage reading kept, in a folder of its own beside <c>windows.json</c>, a record of the
    /// machine's own (no key, no agent works in it, nothing fetched, no process
    /// holds it, not the machine log), its files asked by name under it.
    /// </summary>
    [Fact]
    public void The_usage_history_is_a_data_folder_of_its_own_its_files_asked_under_it()
    {
        Assert.Same(HomeEntry.UsageHistory, HomeEntry.Named("usage-history"));
        Assert.Equal(HomeCategory.Data, HomeEntry.UsageHistory.Category);
        Assert.Equal("data/usage-history", HomeEntry.UsageHistory.New);
        var home = Path.Combine(AppContext.BaseDirectory, "usage-history-home");
        Assert.Equal(
            Path.Combine(DaorisHome.Entry(home, HomeEntry.UsageHistory), "epochs.json"),
            DaorisHome.Entry(home, HomeEntry.UsageHistory, "epochs.json"));
    }

    /// <summary>
    /// The record of the keeping rules' runs, a file of the machine's own in
    /// <c>data/</c>, after the usage history, as the three twins hold it.
    /// </summary>
    [Fact]
    public void The_keeping_record_is_a_data_file_after_the_usage_history()
    {
        Assert.Same(HomeEntry.Keeping, HomeEntry.Named("keeping"));
        Assert.Equal((HomeCategory.Data, "data/keeping.json"), (HomeEntry.Keeping.Category, HomeEntry.Keeping.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.UsageHistory) + 1, table.IndexOf(HomeEntry.Keeping));
    }

    /// <summary>
    /// What ended, failed or went unanswered, a file of the machine's own in
    /// <c>data/</c>, after the keeping record, as the three twins hold it.
    /// </summary>
    [Fact]
    public void The_notices_record_is_a_data_file_after_the_keeping_record()
    {
        Assert.Same(HomeEntry.Notices, HomeEntry.Named("notices"));
        Assert.Equal((HomeCategory.Data, "data/notices.json"), (HomeEntry.Notices.Category, HomeEntry.Notices.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.Keeping) + 1, table.IndexOf(HomeEntry.Notices));
    }

    /// <summary>
    /// What each agent last offered, a data file of its own after the git identities, as the three twins hold it.
    /// </summary>
    [Fact]
    public void The_agent_offers_are_a_data_file_after_the_git_identities()
    {
        Assert.Same(HomeEntry.AgentOffers, HomeEntry.Named("agent-offers"));
        Assert.Equal(
            (HomeCategory.Data, "data/agent-offers.json"),
            (HomeEntry.AgentOffers.Category, HomeEntry.AgentOffers.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.GitIdentities) + 1, table.IndexOf(HomeEntry.AgentOffers));
    }

    /// <summary>
    /// The desktop's own store, a data file of its own after the knowledge index, as the
    /// three twins hold it.
    /// </summary>
    [Fact]
    public void The_desktop_store_is_a_data_file_after_the_knowledge_index()
    {
        Assert.Same(HomeEntry.DesktopDb, HomeEntry.Named("desktop-db"));
        Assert.Equal(
            (HomeCategory.Data, "data/desktop.db"),
            (HomeEntry.DesktopDb.Category, HomeEntry.DesktopDb.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.Knowledge) + 1, table.IndexOf(HomeEntry.DesktopDb));
    }

    /// <summary>
    /// A terminal's presses waiting for the person, a data folder of its own after the
    /// agent offers, as the three twins hold it; kept in <c>data/</c>, which is backed up, never under <c>run/</c>, which anyone may
    /// clear while Daoris is closed. New since the layout, so its old place is its new one.
    /// </summary>
    [Fact]
    public void The_waiting_presses_are_a_data_folder_after_the_agent_offers()
    {
        Assert.Same(HomeEntry.PipeWaiting, HomeEntry.Named("pipe-waiting"));
        Assert.Equal(
            (HomeCategory.Data, "data/pipe-waiting"),
            (HomeEntry.PipeWaiting.Category, HomeEntry.PipeWaiting.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.AgentOffers) + 1, table.IndexOf(HomeEntry.PipeWaiting));
    }

    /// <summary>
    /// AGENTSPLACE1b2 (the Agents-place design §4): a place's apply receipts, a data folder of its own after the act files, as the three
    /// twins hold it. New since the layout, so its old place is its new one.
    /// </summary>
    [Fact]
    public void The_place_receipts_are_a_data_folder_after_the_act_files()
    {
        Assert.Same(HomeEntry.PlaceReceipts, HomeEntry.Named("place-receipts"));
        Assert.Equal(
            (HomeCategory.Data, "data/place-receipts"),
            (HomeEntry.PlaceReceipts.Category, HomeEntry.PlaceReceipts.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.ActFiles) + 1, table.IndexOf(HomeEntry.PlaceReceipts));
    }

    /// <summary>The steward desk's history, a data file of its own after the place receipts, as the three twins hold it.</summary>
    [Fact]
    public void COMMS1l_the_steward_desk_is_a_data_file_after_the_place_receipts()
    {
        Assert.Same(HomeEntry.StewardDesk, HomeEntry.Named("steward-desk"));
        Assert.Equal(
            (HomeCategory.Data, "data/steward-desk.json"),
            (HomeEntry.StewardDesk.Category, HomeEntry.StewardDesk.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.PlaceReceipts) + 1, table.IndexOf(HomeEntry.StewardDesk));
    }

    /// <summary>The newest build that has run on the home, a data file of its own after the steward desk, as the three twins hold it.</summary>
    [Fact]
    public void The_last_build_is_a_data_file_after_the_steward_desk()
    {
        Assert.Same(HomeEntry.LastBuild, HomeEntry.Named("last-build"));
        Assert.Equal(
            (HomeCategory.Data, "data/last-build.json"),
            (HomeEntry.LastBuild.Category, HomeEntry.LastBuild.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.StewardDesk) + 1, table.IndexOf(HomeEntry.LastBuild));
    }

    /// <summary>
    /// The keys of a terminal's sent presses, kept as long as the press's card lives, a
    /// profiles folder after the window's, as the three twins hold it. A file that holds a key is a sign-in, and never under
    /// <c>run/</c>, which anyone may clear while Daoris is closed. New since the layout, so its old place is its new one.
    /// </summary>
    [Fact]
    public void The_sent_presses_keys_are_a_profiles_folder_after_the_window_s()
    {
        Assert.Same(HomeEntry.PipeSent, HomeEntry.Named("pipe-sent"));
        Assert.Equal(
            (HomeCategory.Profiles, "profiles/pipe-sent"),
            (HomeEntry.PipeSent.Category, HomeEntry.PipeSent.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.Window) + 1, table.IndexOf(HomeEntry.PipeSent));
    }

    /// <summary>
    /// Ask Daoris's configured access, a data file of the person's choices at the home's root, named
    /// with no <c>help-</c> prefix, since a workspace's room folder is matched by <c>help-*</c>.
    /// </summary>
    [Fact]
    public void Ask_Daoris_configured_access_is_a_data_file_no_room_is_matched_by()
    {
        Assert.Same(HomeEntry.ConfiguredAccess, HomeEntry.Named("configured-access"));
        Assert.Equal(HomeCategory.Data, HomeEntry.ConfiguredAccess.Category);
        Assert.Equal("data/configured-access.json", HomeEntry.ConfiguredAccess.New);
        Assert.DoesNotMatch("^data/help", HomeEntry.ConfiguredAccess.New);
    }

    /// <summary>A file asked of the environment's home is the entry, or nothing with no home.</summary>
    [Fact]
    public void An_entry_asked_of_the_environment_is_the_entry_or_none_with_no_home()
    {
        Assert.Equal(
            Path.Combine("X:/daoris", "data", "driver.json"),
            DaorisHome.File(Env(("DAORIS_HOME", "X:/daoris")), HomeEntry.DriverConfig));
        Assert.Null(DaorisHome.File(Env(), HomeEntry.DriverConfig));
        var error = Assert.Throws<DriverException>(() => DaorisHome.Require(Env(), HomeEntry.Harnesses));
        Assert.Contains("harnesses.json", error.Message);
    }

    private static string Scratch()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-home-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        return home;
    }

    /// <summary>An install's shape on disk: the marker at its root, and the application under <c>app/</c>.</summary>
    internal static string Install()
    {
        var install = Path.Combine(TestHomes.Root, "daoris-install-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(install, "app", "daoris-driver"));
        File.WriteAllText(Path.Combine(install, "INSTALLED.md"), "# Daoris — installed desktop\n");
        return install;
    }
}
