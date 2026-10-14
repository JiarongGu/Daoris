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
    public void A_file_under_the_home_is_the_home_joined_or_null_when_there_is_none()
    {
        Assert.Equal(
            Path.Combine("D:/somewhere/data", "knowledge.db"),
            DaorisHome.File(Env(("DAORIS_HOME", "D:/somewhere/data")), "knowledge.db"));
        Assert.Null(DaorisHome.File(Env(), "knowledge.db"));
    }
}
