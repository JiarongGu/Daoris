using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One agent's call of a plugin's tool: one round of the command in the plugin's own process (<see cref="PluginCommandRun"/>),
/// <c>from: agent</c>, with the session and its place. The plugin's lines are the text, its <c>data</c> the structured content, and an
/// exit other than 0 an error. A round that asks is refused and never answered, since no person answers a tool call; a required
/// argument not given is refused before any process starts.
/// </summary>
public static class PluginToolCall
{
    /// <summary>The MCP result of the call: <c>{ content, structuredContent?, isError }</c>.</summary>
    public static async Task<object> CallAsync(
        string home, PluginToolCommand tool, JsonElement? arguments, PluginToolsAt at, PluginLog log, CancellationToken ct,
        PluginCommandRun.Starter? start = null)
    {
        var plugin = tool.Plugin.Manifest.Id;
        var command = tool.Command;
        CommandArgs args;
        try
        {
            args = CommandArgs.FromJson(command.Name, command.Args, arguments);
        }
        catch (DriverException refused)
        {
            return Error(refused.Message);
        }

        if (args.Missing.Count > 0) return Error($"`{tool.Name}` needs {string.Join(", ", args.Missing.Select(each => $"`{each.Name}`"))}.");
        var call = new CommandCall(
            command.Name, CommandCall.NewRun(), args.Values, new Dictionary<string, string>(), CommandWire.Agent, at.Place, at.Language, at.Session);
        var clock = Stopwatch.StartNew();
        CommandRound round;
        try
        {
            round = await PluginCommandRun.RoundAsync(home, tool.Plugin, call, ct, start).ConfigureAwait(false);
        }
        catch (DriverException failed)
        {
            log.Commanded(plugin, command.Name, CommandWire.Agent, at.Session, exit: null, clock.ElapsedMilliseconds);
            return Error(failed.Message);
        }

        if (round.Ask is { } asked)
        {
            log.Commanded(plugin, command.Name, CommandWire.Agent, at.Session, exit: null, clock.ElapsedMilliseconds);
            return Error($"`{tool.Name}` asked for `{asked.Name}`, and no person answers a tool call: the person runs "
                + $"`daoris {plugin} {command.Name}` or its page's command instead.");
        }

        log.Commanded(plugin, command.Name, CommandWire.Agent, at.Session, round.Exit, clock.ElapsedMilliseconds);
        var text = round.Said.Count > 0 ? string.Join("\n", round.Said) : round.Data is { } data ? data.GetRawText() : $"`{tool.Name}` ended {round.Exit}.";
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["content"] = new[] { new { type = "text", text } },
            ["isError"] = round.Exit != 0,
        };
        if (round.Data is { } structured) result["structuredContent"] = structured;
        return result;
    }

    private static object Error(string text) => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["content"] = new[] { new { type = "text", text } },
        ["isError"] = true,
    };
}
