using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// One round of a plugin's declared command from the window: the plugin's id, the command, the run's id (absent at a run's first round,
/// then the one the first answer named), the arguments by name, the answers so far, the place in force and the viewer's language.
/// </summary>
public sealed record PluginCommandPress(
    string Id, string Command, string? Run, JsonElement? Args, JsonElement? Answers, string? Place, string? Language)
{
    /// <summary>The answers so far, each a text by its name; anything else is no answer.</summary>
    public IReadOnlyDictionary<string, string> AnswersByName() =>
        Answers is { ValueKind: JsonValueKind.Object } named
            ? named.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(each => each.Name, each => each.Value.GetString()!, StringComparer.Ordinal)
            : new Dictionary<string, string>();
}

/// <summary>
/// The window's door onto a plugin's commands (<c>PLUGIN_COMMAND</c>): one round per call, run in a process of the plugin's own started
/// for it (<see cref="PluginCommandRun"/>), <c>from: window</c>, its commands on at the place named. The page draws the round card from
/// the answer and sends the next round with the answers so far; a masked answer is never kept, logged or pushed here.
/// </summary>
public sealed class PluginCommandRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    [DriverRoute("PLUGIN_COMMAND", PipeClass.Window)]
    [Owner("PLUGIN_COMMAND", None = OwnerNone.ActsOnNothing)]
    [Publishes("PLUGIN_COMMAND", StateScopes.Plugins)]
    private async Task<object?> PluginCommandAsync(PluginCommandPress press, CancellationToken cancellationToken)
    {
        var place = PluginPlace.FromKey(string.IsNullOrWhiteSpace(press.Place) ? null : press.Place.Trim())
            ?? throw new DriverException($"`{press.Place}` is no place: `machine`, `workspace:<name>` or `repository:<id>`.");
        var (plugin, command) = PluginCommandRun.Find(_loop.Home, press.Id, press.Command, place);
        var args = CommandArgs.FromJson(command.Name, command.Args, press.Args);
        if (args.Missing.Count > 0)
            throw new DriverException($"`{command.Name}` needs {string.Join(", ", args.Missing.Select(each => $"`{each.Name}`"))}.");
        var run = press.Run is { Length: 32 } given && given.All(Uri.IsHexDigit) ? given : CommandCall.NewRun();
        var call = new CommandCall(
            command.Name, run, args.Values, press.AnswersByName(), CommandWire.Window, place,
            press.Language is "zh" ? "zh" : "en");
        var round = await PluginCommandRun.RoundAsync(_loop.Home, plugin, call, cancellationToken).ConfigureAwait(false);
        return new { Run = run, round.Said, round.Ask, round.Open, round.Code, round.Then, round.Data, round.Exit };
    }
}
