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
}
