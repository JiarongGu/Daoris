using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris's access mode over the bridge (HELPMODE1a2, D183 point 2): <c>SET_HELP_MODE</c>, the screen's half of
/// <c>daoris-driver help mode</c>, over the same key in the same file (D50); the state answering it as <c>helpMode</c>; a mode the
/// driver does not know refused with nothing written. Edits to <c>driver.json</c> only, so no service and no process.
/// </summary>
public sealed class DriverModuleHelpModeTests : DriverModuleBridge
{
    /// <summary>
    /// 🔴 The route sets the key the terminal sets, and the state answers it: restricted until the person names full, any case taken,
    /// and taken back by naming restricted, which writes the default by taking the key out.
    /// </summary>
    [Fact]
    public async Task The_route_sets_the_mode_the_state_answers()
    {
        var module = Module();
        Assert.Equal(HelpModes.Restricted, (await AnswerAsync(module, "STATE")).GetProperty("helpMode").GetString());

        var full = await AnswerAsync(module, "SET_HELP_MODE", new { mode = "Full" });

        Assert.Equal(HelpModes.Full, full.GetProperty("helpMode").GetString());
        Assert.Equal(HelpModes.Full, DriverConfig.Load(DriverConfigPath).HelpMode);

        var restricted = await AnswerAsync(module, "SET_HELP_MODE", new { mode = HelpModes.Restricted });

        Assert.Equal(HelpModes.Restricted, restricted.GetProperty("helpMode").GetString());
        Assert.DoesNotContain("helpMode", File.ReadAllText(DriverConfigPath));
    }

    /// <summary>A mode the driver does not know, or none, is refused in the driver's words, and nothing is written.</summary>
    [Fact]
    public async Task A_mode_it_does_not_know_is_refused_and_nothing_is_written()
    {
        var unknown = await RefusalAsync(Module(), "SET_HELP_MODE", new { mode = "everything" });
        var none = await RefusalAsync(Module(), "SET_HELP_MODE", new { mode = (string?)null });

        Assert.Contains("`restricted` or `full`", unknown);
        Assert.Contains("`everything`", unknown);
        Assert.Contains("`restricted` or `full`", none);
        Assert.Equal(HelpModes.Restricted, DriverConfig.Load(DriverConfigPath).HelpMode);
    }

    /// <summary>
    /// 🔴 The route and the terminal agree (D50): what one sets the other reads, through the one key. The terminal's full is the
    /// state's full, and the route's restricted is what the terminal says.
    /// </summary>
    [Fact]
    public async Task The_route_and_the_terminal_set_the_same_key()
    {
        var module = Module();
        string Terminal(params string[] args)
        {
            var output = new StringWriter();
            Assert.Equal(0, HelpCommand.Mode(HelpCommand.Read(["mode", .. args], out _)!, DriverConfigPath, output));
            return output.ToString();
        }

        Terminal("full");
        Assert.Equal(HelpModes.Full, (await AnswerAsync(module, "STATE")).GetProperty("helpMode").GetString());

        await AnswerAsync(module, "SET_HELP_MODE", new { mode = HelpModes.Restricted });
        Assert.StartsWith("help: Ask Daoris runs restricted", Terminal());

        await AnswerAsync(module, "SET_HELP_MODE", new { mode = HelpModes.Full });
        Assert.Contains("already runs with full access", Terminal("full"));
    }
}
