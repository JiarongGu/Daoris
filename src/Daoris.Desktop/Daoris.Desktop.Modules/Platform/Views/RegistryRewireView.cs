using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris repository wire &lt;repository&gt; --workspace &lt;name&gt;</c> as the card shows it: a registered repository moved to a
/// workspace, one row of this machine's registry, no file touched. Refused before any card where the repository is not registered
/// here or is in that workspace already; a workspace no repository is in yet is started by the move, which the card says.
/// </summary>
public sealed class RegistryRewireView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "REGISTRY_REWIRE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var named = Text(payload, "repository");
        if (named.Length == 0) return PressViewed.Refused("a move names the repository, as Repositories lists it.");
        var asked = Text(payload, "workspace");
        if (asked.Length == 0) return PressViewed.Refused($"a move names the workspace to move `{named}` to.");

        var registry = await world.RegistryAsync(cancellationToken).ConfigureAwait(false);
        if (registry.FirstOrDefault(row => string.Equals(row.Repository, named, StringComparison.OrdinalIgnoreCase)) is not { } found)
        {
            return PressViewed.Refused($"`{named}` is not registered on this machine — use a repository's name as Repositories lists it.");
        }

        // The service's own normalizing and comparing, as the route stores and the registry compares a workspace's name.
        var to = RemoteTarget.Workspace(asked);
        var from = RemoteTarget.Workspace(found.Workspace);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return PressViewed.Refused($"`{found.Repository}` is already in workspace `{from}`, so there is nothing to move.");
        }

        var starts = !registry.Any(row => string.Equals(RemoteTarget.Workspace(row.Workspace), to, StringComparison.OrdinalIgnoreCase));
        return PressViewed.Shown(new PressView(
            starts ? "repository-move-starts" : "repository-move",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["repository"] = found.Repository, ["from"] = from, ["to"] = to },
            $"daoris repository wire {ShellWord.Of(found.Repository, ShellWord.Name)} --workspace {ShellWord.Of(to, ShellWord.Workspace)}"));
    }

    private static string Text(JsonElement? payload, string name) => PressPayload.Text(payload, name);
}
