using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The newest build that has run on a home, which the launcher refuses an older one by. Kept only upward; an older build
/// is refused in one sentence naming both versions; the same version, a newer one, or a home with nothing kept or unreadable, starts.
/// </summary>
public sealed class HomeLastBuildTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "home-last-build", Guid.NewGuid().ToString("N")[..8], "data");

    public HomeLastBuildTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        GitSeed.Discard(Path.GetDirectoryName(_home)!);
    }

    [Fact]
    public void A_home_keeps_its_newest_build_and_never_lowers_it()
    {
        Assert.Null(HomeLastBuild.Read(_home));

        HomeLastBuild.Keep(_home, "0.4.0+abc1234");
        HomeLastBuild.Keep(_home, "0.3.9");
        Assert.Equal("0.4.0+abc1234", HomeLastBuild.Read(_home));

        HomeLastBuild.Keep(_home, "0.4.1-local.7");
        Assert.Equal("0.4.1-local.7", HomeLastBuild.Read(_home));
    }

    [Fact]
    public void An_older_build_is_refused_naming_both_versions_and_the_same_or_a_newer_one_starts()
    {
        HomeLastBuild.Keep(_home, "0.4.0");

        var refused = HomeLastBuild.Refusal(_home, "0.3.2+def5678");
        Assert.NotNull(refused);
        Assert.Contains("0.3.2+def5678", refused);
        Assert.Contains("0.4.0", refused);
        Assert.Null(HomeLastBuild.Refusal(_home, "0.4.0-local.1"));
        Assert.Null(HomeLastBuild.Refusal(_home, "0.5.0"));
        Assert.Null(HomeLastBuild.Refusal(_home, null));
    }

    [Fact]
    public void A_home_with_nothing_kept_or_a_file_that_does_not_read_refuses_nothing()
    {
        Assert.Null(HomeLastBuild.Refusal(_home, "0.1.0"));

        var kept = DaorisHome.Entry(_home, HomeEntry.LastBuild);
        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        File.WriteAllText(kept, "not json");

        Assert.Null(HomeLastBuild.Read(_home));
        Assert.Null(HomeLastBuild.Refusal(_home, "0.1.0"));
    }
}
