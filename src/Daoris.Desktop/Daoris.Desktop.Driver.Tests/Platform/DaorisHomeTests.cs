using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where every machine-local file lives (D63): one home, named by <c>DAORIS_HOME</c>, and no default
/// under the user profile. The driver's copy of a three-way twin — the CLI and the service hold the
/// same contract in their own languages, sharing no code (WSP3's shape).
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
    /// 🔴 FINDDAORIS1 (D171 point 1): a terminal outside Daoris's own panel set nothing, and the install's launcher sets
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
    /// The host takes the install's home as its own process's <c>DAORIS_HOME</c> where none is named, as the desktop does
    /// (D63), so its machine log, its files and every session it starts read the same machine; a home already named is left.
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
    /// 🔴 D172 point 5 (HOMELAYOUT2, the install layout design §6.2) with its review note: an entry is read and written at its new
    /// place if that holds what the entry is, else at its old place if that is there, else at its new place; where both hold it,
    /// neither is chosen, and the refusal names both. What a place holds: a file that is not empty and, for JSON, parses; a folder
    /// that is not empty; or, for an entry whose place is known by the names it holds, one of those names. So an empty or
    /// half-made new place never shadows a populated old one, and an old one that does not read is still read where it is, for
    /// its reader to say so. A row is an entry's new and old place, its names, what is on the disk (a file holds <c>{}</c> for
    /// JSON and a letter otherwise, what follows <c>=</c> where one is written; a trailing <c>/</c> is an empty folder), what is
    /// asked, and where that is (empty: the home itself), or <c>refused</c>. A <c>*</c> is an item, filled from what is asked one
    /// part each; what is asked beyond the items is joined under the item found, and asked short of them is the folder holding
    /// them, never refused since both places hold items by design. The CLI's <c>home.test.ts</c> holds these rows and the
    /// service's to its own, cell for cell.
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
        var home = Scratch();
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

    /// <summary>
    /// A list of an entry's items reads both places (the design §6.2): the new place first, then the old, once each, asked
    /// without the disk. The CLI's <c>home.test.ts</c> holds these rows and the service's to its own, cell for cell.
    /// </summary>
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

    /// <summary>
    /// D172's table, one entry each, held cell for cell by the CLI's <c>home.test.ts</c> against the service's and its own. Here:
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
    /// USAGECAP1a1 (D179 point 2): every usage reading kept, in a folder of its own beside <c>windows.json</c>, a record of the
    /// machine's own (the install layout design §2's seventh test: no key, no agent works in it, nothing fetched, no process
    /// holds it, not the machine log), its files asked by name under it.
    /// </summary>
    [Fact]
    public void The_usage_history_is_a_data_folder_of_its_own_its_files_asked_under_it()
    {
        Assert.Same(HomeEntry.UsageHistory, HomeEntry.Named("usage-history"));
        Assert.Equal(HomeCategory.Data, HomeEntry.UsageHistory.Category);
        Assert.Equal("usage-history", HomeEntry.UsageHistory.New);
        var home = Path.Combine(AppContext.BaseDirectory, "usage-history-home");
        Assert.Equal(
            Path.Combine(DaorisHome.Entry(home, HomeEntry.UsageHistory), "epochs.json"),
            DaorisHome.Entry(home, HomeEntry.UsageHistory, "epochs.json"));
    }

    /// <summary>
    /// HELPMODE1c2a (D183's note): Ask Daoris's configured access, a data file of the person's choices at the home's root, named
    /// with no <c>help-</c> prefix, since a workspace's room folder is matched by <c>help-*</c>.
    /// </summary>
    [Fact]
    public void Ask_Daoris_configured_access_is_a_data_file_no_room_is_matched_by()
    {
        Assert.Same(HomeEntry.ConfiguredAccess, HomeEntry.Named("configured-access"));
        Assert.Equal(HomeCategory.Data, HomeEntry.ConfiguredAccess.Category);
        Assert.Equal("configured-access.json", HomeEntry.ConfiguredAccess.New);
        Assert.DoesNotMatch("^help", HomeEntry.ConfiguredAccess.New);
    }

    /// <summary>A file asked of the environment's home is the entry, by D172 point 5, or nothing with no home.</summary>
    [Fact]
    public void An_entry_asked_of_the_environment_is_the_entry_or_none_with_no_home()
    {
        Assert.Equal(
            Path.Combine("D:/somewhere/data", "driver.json"),
            DaorisHome.File(Env(("DAORIS_HOME", "D:/somewhere/data")), HomeEntry.DriverConfig));
        Assert.Null(DaorisHome.File(Env(), HomeEntry.DriverConfig));
        var error = Assert.Throws<DriverException>(() => DaorisHome.Require(Env(), HomeEntry.Harnesses));
        Assert.Contains("harnesses.json", error.Message);
    }

    private static string Scratch()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-home-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        return home;
    }

    /// <summary>An install's shape on disk (D93): the marker at its root, and the application under <c>app/</c>.</summary>
    internal static string Install()
    {
        var install = Path.Combine(Path.GetTempPath(), "daoris-install-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(install, "app", "daoris-driver"));
        File.WriteAllText(Path.Combine(install, "INSTALLED.md"), "# Daoris — installed desktop\n");
        return install;
    }
}
