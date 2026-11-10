using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A repository's switch on this machine as the card shows it: <c>daoris place drive|undrive</c> (<c>SET_DRIVABLE</c>), <c>hold|resume</c>
/// (<c>SET_HOLD</c>) and <c>trees on|off</c> (<c>SET_TREES</c>). Refused before any card where the repository is not registered here; one
/// already set as asked is the route's own grounds' to answer.
/// </summary>
public sealed class PlaceSwitchView : IPressView
{
    private readonly string _flag;
    private readonly (string Verb, string Kind) _on;
    private readonly (string Verb, string Kind) _off;
    private readonly string? _word;

    /// <param name="route">The route the switch is pressed on.</param>
    /// <param name="flag">The payload's boolean that says which way it is set.</param>
    /// <param name="on">The verb and card kind of setting it on.</param>
    /// <param name="off">The verb and card kind of setting it off.</param>
    /// <param name="word">The word the verb ends with in place of two verbs (<c>trees</c> takes <c>on</c> or <c>off</c>); null where each way is a verb.</param>
    private PlaceSwitchView(string route, string flag, (string, string) on, (string, string) off, string? word)
    {
        (Route, _flag, _on, _off, _word) = (route, flag, on, off, word);
    }

    public static PlaceSwitchView Drivable() => new("SET_DRIVABLE", "drivable", ("drive", "place-drive"), ("undrive", "place-undrive"), null);

    public static PlaceSwitchView Hold() => new("SET_HOLD", "held", ("hold", "place-hold"), ("resume", "place-resume"), null);

    public static PlaceSwitchView Trees() => new("SET_TREES", "ownTree", ("trees", "place-trees-on"), ("trees", "place-trees-off"), "on|off");

    public string Module => DriverModule.Name;

    public string Route { get; }

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var named = PressPayload.Text(payload, "repository");
        if (named.Length == 0) return PressViewed.Refused("a switch names the repository, as Repositories lists it.");
        var registry = await world.RegistryAsync(cancellationToken).ConfigureAwait(false);
        if (registry.FirstOrDefault(row => string.Equals(row.Repository, named, StringComparison.OrdinalIgnoreCase)) is not { } found)
        {
            return PressViewed.Refused($"`{named}` is not registered on this machine — use a repository's name as Repositories lists it.");
        }

        var on = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(_flag, out var flag) && flag.ValueKind == JsonValueKind.True;
        var (verb, kind) = on ? _on : _off;
        var command = $"daoris place {verb} {ShellWord.Of(found.Repository, ShellWord.Name)}" + (_word is null ? "" : on ? " on" : " off");
        return PressViewed.Shown(new PressView(kind, new Dictionary<string, string>(StringComparer.Ordinal) { ["repository"] = found.Repository }, command));
    }
}
