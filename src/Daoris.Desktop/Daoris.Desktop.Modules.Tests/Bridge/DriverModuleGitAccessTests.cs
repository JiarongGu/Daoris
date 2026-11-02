using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Git access over the bridge (GITAUTH1f, D198 points 8, 10 and 12): <c>SET_GIT_ACCESS</c>, the page's half of <c>daoris-driver git
/// access</c>, over the same keys in the same file, separate from <c>SET_ACCESS</c>; the state answering each level as set; a class or a
/// value there is not, and Dangerous, refused in the driver's words with nothing written.
/// </summary>
public sealed class DriverModuleGitAccessTests : DriverModuleBridge
{
    [Fact]
    public async Task The_route_sets_each_level_and_the_state_answers_what_is_set_and_touches_no_access()
    {
        var module = Module();
        await AnswerAsync(module, "SET_ACCESS", new { repository = "engine", access = "full" });

        await AnswerAsync(module, "SET_GIT_ACCESS", new { repository = "engine", access = new { write = "Ask" } });
        await AnswerAsync(module, "SET_GIT_ACCESS", new { workspace = "shop", oneValue = "deny" });
        var state = await AnswerAsync(module, "SET_GIT_ACCESS", new { machine = true, access = new { read = "ask" } });

        Assert.Equal("""[{"repository":"engine","classes":[{"class":"write","value":"ask"}]}]""", state.GetProperty("gitAccess").GetRawText());
        Assert.Equal("""[{"workspace":"shop","classes":[{"class":"read","value":"deny"},{"class":"write","value":"deny"}]}]""", state.GetProperty("workspaceGitAccess").GetRawText());
        Assert.Equal("""[{"class":"read","value":"ask"}]""", state.GetProperty("machineGitAccess").GetRawText());
        Assert.Equal("""[{"repository":"engine","setting":{"full":true,"pauses":[]}}]""", state.GetProperty("access").GetRawText());

        var cleared = await AnswerAsync(module, "SET_GIT_ACCESS", new { repository = "ENGINE" });
        Assert.Empty(cleared.GetProperty("gitAccess").EnumerateArray());
    }

    [Fact]
    public async Task A_class_or_value_it_does_not_have_and_dangerous_are_refused_and_nothing_is_written()
    {
        var module = Module();
        await AnswerAsync(module, "SET_GIT_ACCESS", new { repository = "engine", access = new { write = "ask" } });
        var before = File.ReadAllText(DriverConfigPath);

        var cls = await RefusalAsync(module, "SET_GIT_ACCESS", new { repository = "engine", access = new { push = "ask" } });
        var value = await RefusalAsync(module, "SET_GIT_ACCESS", new { repository = "engine", access = new { read = "sometimes" } });
        var dangerous = await RefusalAsync(module, "SET_GIT_ACCESS", new { repository = "engine", access = new { dangerous = "allow" } });
        var level = await RefusalAsync(module, "SET_GIT_ACCESS", new { oneValue = "ask" });

        Assert.Contains("not a class of git access", cls);
        Assert.Contains("not `sometimes`", value);
        Assert.Contains("dangerous-unavailable", dangerous);
        Assert.Contains("name exactly one", level);
        Assert.Equal(before, File.ReadAllText(DriverConfigPath));
    }
}
