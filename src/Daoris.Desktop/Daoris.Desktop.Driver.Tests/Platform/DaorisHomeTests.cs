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

    [Fact]
    public void A_file_under_the_home_is_the_home_joined_or_null_when_there_is_none()
    {
        Assert.Equal(
            Path.Combine("D:/somewhere/data", "driver.json"),
            DaorisHome.File(Env(("DAORIS_HOME", "D:/somewhere/data")), "driver.json"));
        Assert.Null(DaorisHome.File(Env(), "driver.json"));
    }

    /// <summary>
    /// The management class REFUSES rather than guessing: a driver with no home and no per-file
    /// override names what to set, in one sentence, instead of writing somewhere nobody pointed it.
    /// </summary>
    [Fact]
    public void Requiring_a_file_with_no_home_is_a_refusal_that_names_the_variable()
    {
        var error = Assert.Throws<DriverException>(() => DaorisHome.Require(Env(), "driver.json"));
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

    /// <summary>An install's shape on disk (D93): the marker at its root, and the application under <c>app/</c>.</summary>
    internal static string Install()
    {
        var install = Path.Combine(Path.GetTempPath(), "daoris-install-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(install, "app", "daoris-driver"));
        File.WriteAllText(Path.Combine(install, "INSTALLED.md"), "# Daoris — installed desktop\n");
        return install;
    }
}
