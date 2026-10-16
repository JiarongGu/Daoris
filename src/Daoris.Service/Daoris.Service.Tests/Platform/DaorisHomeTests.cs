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
    public void A_file_under_the_home_is_the_home_joined_or_null_when_there_is_none()
    {
        Assert.Equal(
            Path.Combine("D:/somewhere/data", "knowledge.db"),
            DaorisHome.File(Env(("DAORIS_HOME", "D:/somewhere/data")), "knowledge.db"));
        Assert.Null(DaorisHome.File(Env(), "knowledge.db"));
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
