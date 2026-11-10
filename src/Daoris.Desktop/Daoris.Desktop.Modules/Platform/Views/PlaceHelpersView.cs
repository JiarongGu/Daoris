using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place helpers</c> as the card shows it (<c>SET_HELPERS</c>): what a helper an agent asks for does in a repository, or in
/// each repository of a workspace that sets none of its own. Judged by the rule table as the route is (<see cref="HelperRules.Apply"/>)
/// against the rule the machine holds now; the card says the rule it leaves and what an agent's ask then does. A refusal is the table's
/// own, back to the terminal with no card.
/// </summary>
public sealed class PlaceHelpersView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_HELPERS";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, "a helpers rule", true, cancellationToken).ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);

        var edit = (payload is { } body ? HelperRules.EditOf(body) : new HelperEdit()) with { Repository = scoped.Repository, Workspace = scoped.Workspace };
        if (!edit.Clear && edit.Set is not { Names: true })
        {
            return PressViewed.Refused("a helpers change names `--mode`, `--agents` or `--clear` — `daoris place helpers --help`.");
        }

        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        DriverConfig after;
        try
        {
            after = HelperRules.Apply(machine.Config, edit);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var facts = scoped.Facts();
        var kind = (scoped.Circle ? "helpers-workspace-" : "helpers-")
            + ((scoped.Circle ? after.WorkspaceHelpers : after.Helpers).TryGetValue((scoped.Circle ? scoped.Workspace : scoped.Repository)!, out var rule) ? "set" : "clear");
        if (rule is not null)
        {
            facts["mode"] = rule.Mode;
            if (rule.Agents.Count > 0)
            {
                facts["agents"] = string.Join(", ", rule.Agents);
                kind += "-agents";
            }
        }

        return PressViewed.Shown(new PressView(kind, facts,$"daoris place helpers {scoped.Words} {Words(edit)}"));
    }

    private static string Words(HelperEdit edit)
    {
        if (edit.Clear) return "--clear";
        var words = new List<string>();
        if (edit.Set!.Mode is { } mode) words.AddRange(["--mode", mode]);
        if (edit.Set.Agents is { } agents) words.AddRange(["--agents", string.Join(',', agents)]);
        return string.Join(' ', words);
    }
}
