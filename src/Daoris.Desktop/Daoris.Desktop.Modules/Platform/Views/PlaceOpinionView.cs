using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place opinion</c> as the card shows it (<c>SET_OPINION</c>): which other agent reads a repository's, or a whole workspace's,
/// work before it lands. Judged by the rule table as the route is (<see cref="OpinionRules.Apply"/>) against the rule the machine holds now;
/// the card says what the rule lets a reviewer do, names the reviewers of the working agent's own family (a plugin's agent judged by what
/// its plugin declares), and says what the gate does with it. A refusal is the table's own, back to the terminal with no card.
/// </summary>
public sealed class PlaceOpinionView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_OPINION";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, "a second-opinion rule", true, cancellationToken).ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);

        var edit = (payload is { } body ? OpinionRules.EditOf(body) : new OpinionEdit()) with { Repository = scoped.Repository, Workspace = scoped.Workspace };
        if (!edit.None && !edit.Clear && edit.Set is not { Names: true })
        {
            return PressViewed.Refused("a second-opinion change names `--agents`, `--on`, the switches, `none` or `--clear` — `daoris place opinion --help`.");
        }

        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        DriverConfig after;
        try
        {
            after = OpinionRules.Apply(machine.Config, edit);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var whose = scoped.Circle ? $"each repository of workspace `{scoped.Workspace}` that sets none of its own" : $"`{scoped.Repository}`";
        var head = edit.Clear
            ? scoped.Circle
                ? $"Clears workspace `{scoped.Workspace}`'s second-opinion rule: each repository there keeps its own, else none."
                : $"Clears `{scoped.Repository}`'s second-opinion rule: it takes its workspace's again, else none."
            : edit.None ? $"Declares that `{scoped.Repository}` has no second opinion, whatever its workspace says."
            : $"Sets the second opinion for {whose}.";
        var adapters = ProviderRegistry.Built().WithPlugins(machine.Facts.Plugins);
        List<string> said = (scoped.Circle ? after.WorkspaceOpinions : after.Opinions).TryGetValue((scoped.Circle ? scoped.Workspace : scoped.Repository)!, out var standing)
            ? [.. OpinionRules.Says(standing, OpinionRules.SameAgent(standing, ActorChoices.BuildAgent(machine.Config), adapters)), .. OpinionRules.Gate(standing)]
            : [];
        var facts = scoped.Facts();
        facts["describe"] = string.Join(" ", [head, .. said]);
        return PressViewed.Shown(new PressView("opinion", facts, $"daoris place opinion {scoped.Words} {Words(edit)}"));
    }

    private static string Words(OpinionEdit edit)
    {
        if (edit.Clear) return "--clear";
        if (edit.None) return "none";
        var set = edit.Set!;
        var words = new List<string>();
        if (set.Reviewers is not null) words.AddRange(["--agents", string.Join(',', set.Reviewers)]);
        if (set.On is not null) words.AddRange(["--on", string.Join(',', set.On)]);
        if (set.Required is { } required) words.Add(required ? "--required" : "--not-required");
        if (set.Verify is { } verify) words.Add(verify ? "--verify" : "--no-verify");
        if (set.Minutes is { } minutes) words.AddRange(["--minutes", minutes.ToString(CultureInfo.InvariantCulture)]);
        if (set.Recheck is { } recheck) words.Add(recheck ? "--recheck" : "--no-recheck");
        return string.Join(' ', words);
    }
}
