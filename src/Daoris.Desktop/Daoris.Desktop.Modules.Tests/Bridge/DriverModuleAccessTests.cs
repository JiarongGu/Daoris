using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A repository's session access setup over the bridge (AUTONOMY1a, D184 point 1): <c>SET_ACCESS</c>, the setup page's half of
/// <c>daoris-driver access</c>, over the same keys in the same file (D50); the state answering each level as set; a pause or a value the
/// setup does not have refused in the driver's words with nothing written. Edits to <c>driver.json</c> only, so no service and no process.
/// </summary>
public sealed class DriverModuleAccessTests : DriverModuleBridge
{
    /// <summary>The route sets a repository's, a workspace's and this machine's level, and the state answers each as set.</summary>
    [Fact]
    public async Task The_route_sets_each_level_and_the_state_answers_what_is_set()
    {
        var module = Module();
        var empty = await AnswerAsync(module, "STATE");
        Assert.Empty(empty.GetProperty("access").EnumerateArray());
        Assert.Equal("""{"full":false,"pauses":[]}""", empty.GetProperty("machineAccess").GetRawText());

        await AnswerAsync(module, "SET_ACCESS", new { repository = "engine", access = new { goAheads = "never", permissions = "Asking" } });
        await AnswerAsync(module, "SET_ACCESS", new { workspace = "shop", access = "full" });
        var state = await AnswerAsync(module, "SET_ACCESS", new { machine = true, access = new { landing = "rule" } });

        Assert.Equal(
            """[{"repository":"engine","setting":{"full":false,"pauses":[{"pause":"permissions","value":"asking"},{"pause":"goAheads","value":"never"}]}}]""",
            state.GetProperty("access").GetRawText());
        Assert.Equal("""[{"workspace":"shop","setting":{"full":true,"pauses":[]}}]""", state.GetProperty("workspaceAccess").GetRawText());
        Assert.Equal("""{"full":false,"pauses":[{"pause":"landing","value":"rule"}]}""", state.GetProperty("machineAccess").GetRawText());

        var resolved = AccessRules.Resolve(DriverConfig.Load(DriverConfigPath), "engine", "shop");
        Assert.Equal(("asking", "repository"), (resolved.Value(AccessRules.Permissions), resolved.Source(AccessRules.Permissions)));
        Assert.Equal(("intake", "workspace"), (resolved.Value(AccessRules.Routing), resolved.Source(AccessRules.Routing)));

        // No access, or a null one, clears the level.
        var cleared = await AnswerAsync(module, "SET_ACCESS", new { repository = "ENGINE" });
        Assert.Empty(cleared.GetProperty("access").EnumerateArray());
    }

    /// <summary>One change at exactly one level, every pause and value the setup has: anything else is the driver's refusal, nothing written.</summary>
    [Fact]
    public async Task A_pause_or_value_it_does_not_have_is_refused_and_nothing_is_written()
    {
        var module = Module();
        await AnswerAsync(module, "SET_ACCESS", new { repository = "engine", access = new { landing = "rule" } });
        var before = File.ReadAllText(DriverConfigPath);

        var pause = await RefusalAsync(module, "SET_ACCESS", new { repository = "engine", access = new { plan = "always" } });
        var value = await RefusalAsync(module, "SET_ACCESS", new { repository = "engine", access = new { permissions = "sometimes" } });
        var level = await RefusalAsync(module, "SET_ACCESS", new { access = "full" });
        var word = await RefusalAsync(module, "SET_ACCESS", new { machine = true, access = "everything" });

        Assert.Contains(AccessRules.UnknownPause("plan"), pause);
        Assert.Contains(AccessRules.UnknownValue(AccessRules.Pause(AccessRules.Permissions), "sometimes"), value);
        Assert.Contains("name exactly one", level);
        Assert.Contains("an access setup is `full`, or the pauses it names", word);
        Assert.Equal(before, File.ReadAllText(DriverConfigPath));
    }

    /// <summary>
    /// 🔴 The route and the terminal agree (D50): what one sets the other reads, through the same keys, and both refuse a value the setup
    /// does not have in the same words.
    /// </summary>
    [Fact]
    public async Task The_route_and_the_terminal_set_the_same_keys_and_refuse_alike()
    {
        var module = Module();
        string Terminal(int exit, params string[] args)
        {
            var output = new StringWriter();
            Assert.Equal(exit, AccessCommand.Run(AccessCommand.Read(args, out _)!, DriverConfigPath, new AccessPlace("shop", Known: true), output));
            return output.ToString();
        }

        Terminal(0, "--workspace", "shop", "full");
        var state = await AnswerAsync(module, "STATE");
        Assert.Equal("""[{"workspace":"shop","setting":{"full":true,"pauses":[]}}]""", state.GetProperty("workspaceAccess").GetRawText());

        await AnswerAsync(module, "SET_ACCESS", new { repository = "engine", access = new { permissions = "asking" } });
        Assert.Contains("`engine`'s own: permissions asking.", Terminal(0, "engine"));
        Assert.Contains("  routing      intake        its workspace, `shop`", Terminal(0, "engine"));

        var routed = await RefusalAsync(module, "SET_ACCESS", new { repository = "engine", access = new { landing = "sometimes" } });
        var typed = Terminal(1, "engine", "landing=sometimes");
        Assert.Contains(typed.Trim()["access: ".Length..], routed);
    }
}
