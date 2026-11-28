using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>The baseline verb's options: each flag takes its value, and a flag left without one, or one it does not know, is its usage.</summary>
public sealed class SessionLifeConsoleTests
{
    [Fact]
    public void Each_flag_takes_its_value_and_one_left_without_a_value_is_refused_not_passed_over()
    {
        Assert.Equal(("copy.db", "markers", 7), SessionLifeConsole.Options(["baseline", "--store", "copy.db", "--markers", "markers", "--count", "7"], "home.db"));
        Assert.Equal(("home.db", null, 50), SessionLifeConsole.Options(["baseline"], "home.db"));
        Assert.Null(SessionLifeConsole.Options(["baseline", "--store"], "home.db"));
        Assert.Null(SessionLifeConsole.Options(["baseline", "--count", "7", "--markers"], "home.db"));
        Assert.Null(SessionLifeConsole.Options(["baseline", "--count", "0"], "home.db"));
        Assert.Null(SessionLifeConsole.Options(["baseline", "--stor", "copy.db"], "home.db"));
        Assert.Null(SessionLifeConsole.Options(["fold"], "home.db"));
    }
}
