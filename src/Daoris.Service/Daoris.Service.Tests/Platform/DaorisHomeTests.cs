using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Where every machine-local file lives (D63): one home, named by <c>DAORIS_HOME</c>, and no default
/// under the user profile. This is the service side's copy of a three-way twin — the CLI and the
/// driver hold the same contract in their own languages, sharing no code, the way the remotes map's
/// three copies do (WSP3).
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
            Path.Combine("D:/somewhere/data", "knowledge.db"),
            DaorisHome.File(Env(("DAORIS_HOME", "D:/somewhere/data")), HomeEntry.Knowledge));
        Assert.Null(DaorisHome.File(Env(), HomeEntry.Knowledge));
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
}
