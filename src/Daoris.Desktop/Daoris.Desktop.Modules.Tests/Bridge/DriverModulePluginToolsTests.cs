using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A plugin's tools over the bridge (ADOSIGNIN1): the page's read declares them, the person's Check finds and reads them, and the
/// person's press of a check's fix opens a console and reads the tool again. A stand-in answers for every program and every window.
/// </summary>
public sealed class DriverModulePluginToolsTests : DriverModuleBridge
{
    private const string Manifest = """
        { "id": "acme.land", "name": "Acme land", "version": "1.0.0",
          "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] },
          "tools": [
            { "id": "pwsh", "install": { "windows": "winget install -e --id Microsoft.PowerShell" },
              "ready": [
                { "run": ["pwsh", "account", "show"], "says": "Signed in", "fix": "pwsh login", "signin": true },
                { "run": ["pwsh", "extension", "show"], "says": "Extension added", "fix": "rm -rf /" } ] } ] }
        """;

    private void Install()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, "acme.land");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), Manifest);
    }

    private DriverModule Standin(List<string> asked, bool signedIn = false) => new(Bus, Loop())
    {
        ToolChecks = (_, tools, _) =>
        {
            asked.Add("check");
            return Task.FromResult<IReadOnlyList<PluginToolFound>>(
                [.. tools.Select(tool => new PluginToolFound(tool, true, "pwsh", ToolWay.System, "2.70.0", "PowerShell 2.70.0, the system's.",
                    [.. tool.Ready.Select(check => new ReadyAnswer(check, false, $"not ready: {check.Says}."))]))]);
        },
        ToolSetUps = (_, tools, tool, check, _) =>
        {
            asked.Add($"fix {tool} {check}");
            var declared = tools.Single(each => each.Id == tool);
            var (words, problem) = PluginTools.FixWords(tools, declared.Ready[check - 1]);
            if (words is null) return Task.FromResult(new ToolSetUp("fix_not_declared", problem!, null, null, null));
            return Task.FromResult(new ToolSetUp(null, "ready: Signed in.", 0, null,
                new PluginToolFound(declared, true, "pwsh", ToolWay.System, "2.70.0", "PowerShell 2.70.0, the system's.",
                    [.. declared.Ready.Select(each => new ReadyAnswer(each, signedIn, signedIn ? $"ready: {each.Says}." : $"not ready: {each.Says}."))])));
        },
    };

    [Fact]
    public async Task The_plugins_page_declares_its_tools_and_runs_nothing()
    {
        Install();
        var asked = new List<string>();

        var module = Standin(asked);
        var listed = (await AnswerAsync(module, "PLUGINS")).GetProperty("plugins").EnumerateArray().Single();
        Assert.Equal("pwsh", listed.GetProperty("tools")[0].GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, listed.GetProperty("tools")[0].GetProperty("found").ValueKind);

        var page = await AnswerAsync(module, "PLUGIN", new { id = "acme.land" });

        var tool = Assert.Single(page.GetProperty("tools").EnumerateArray());
        Assert.Equal("pwsh", tool.GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, tool.GetProperty("found").ValueKind);
        var checks = tool.GetProperty("checks").EnumerateArray().ToList();
        Assert.Equal([true, false], checks.Select(check => check.GetProperty("press").GetBoolean()));
        Assert.Equal([true, false], checks.Select(check => check.GetProperty("signIn").GetBoolean()));
        Assert.Contains("`rm` is not a tool this plugin declares", checks[1].GetProperty("refusal").GetString());
        Assert.Empty(asked);
    }

    [Fact]
    public async Task The_checks_press_reads_each_tool_and_the_fix_press_opens_the_console_then_answers_the_standing_read_again()
    {
        Install();
        var asked = new List<string>();
        var module = Standin(asked, signedIn: true);

        var checked_ = await AnswerAsync(module, "PLUGIN_TOOLS", new { id = "acme.land" });
        Assert.True(checked_.GetProperty("tools")[0].GetProperty("found").GetBoolean());
        Assert.False(checked_.GetProperty("tools")[0].GetProperty("checks")[0].GetProperty("ready").GetBoolean());

        var fixed_ = await AnswerAsync(module, "PLUGIN_TOOL_FIX", new { id = "acme.land", tool = "pwsh", check = 1 });

        Assert.True(fixed_.GetProperty("ran").GetBoolean());
        Assert.Equal(0, fixed_.GetProperty("exitCode").GetInt32());
        Assert.True(fixed_.GetProperty("standing").GetProperty("checks")[0].GetProperty("ready").GetBoolean());
        Assert.Equal(["check", "fix pwsh 1"], asked);
    }

    [Fact]
    public async Task A_fix_naming_another_command_is_answered_as_a_refusal_and_runs_nothing()
    {
        Install();

        var answer = await AnswerAsync(Standin([]), "PLUGIN_TOOL_FIX", new { id = "acme.land", tool = "pwsh", check = 2 });

        Assert.False(answer.GetProperty("ran").GetBoolean());
        Assert.Equal("fix_not_declared", answer.GetProperty("why").GetString());
        Assert.Contains("`rm` is not a tool this plugin declares", answer.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_plugin_that_is_not_here_is_refused_for_both_presses()
    {
        Assert.Contains("no plugin `acme.gone`", await RefusalAsync(Module(), "PLUGIN_TOOLS", new { id = "acme.gone" }));
        Assert.Contains("no plugin `acme.gone`", await RefusalAsync(Module(), "PLUGIN_TOOL_FIX", new { id = "acme.gone", tool = "pwsh", check = 1 }));
    }
}
