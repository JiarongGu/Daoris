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

        var kind = edit.Clear ? (scoped.Circle ? "opinion-workspace-clear" : "opinion-clear")
            : edit.None ? "opinion-none"
            : scoped.Circle ? "opinion-workspace-set" : "opinion-set";
        var adapters = ProviderRegistry.Built().WithPlugins(machine.Facts.Plugins);
        var also = (scoped.Circle ? after.WorkspaceOpinions : after.Opinions).TryGetValue((scoped.Circle ? scoped.Workspace : scoped.Repository)!, out var standing)
            ? Said(standing, OpinionRules.SameAgent(standing, ActorChoices.BuildAgent(machine.Config), adapters))
            : [];
        return PressViewed.Shown(new PressView(kind, scoped.Facts(), $"daoris place opinion {scoped.Words} {Words(edit)}", also));
    }

    private static List<PressSaid> Said(OpinionRule rule, IReadOnlyList<string> sameAgent)
    {
        if (rule.IsNone) return [new PressSaid("opinion-none-here")];
        var reviewers = string.Join(", ", rule.Reviewers);
        var landing = rule.On.Contains(OpinionRules.Landing);
        var steps = rule.On.Contains(OpinionRules.Steps);
        var said = new List<PressSaid> { new(landing ? "opinion-landing" : "opinion-chain", ("reviewers", reviewers)) };
        if (landing && steps) said.Add(new PressSaid("opinion-steps-too"));
        if (rule.Verify) said.Add(new PressSaid("opinion-verify"));
        said.Add(new PressSaid(rule.Required ? "opinion-required" : "opinion-not-required"));
        said.Add(new PressSaid("opinion-bound", ("minutes", rule.Bound.ToString(CultureInfo.InvariantCulture))));
        if (!rule.Recheck) said.Add(new PressSaid("opinion-no-recheck"));
        var same = rule.Reviewers.Where(reviewer => sameAgent.Contains(reviewer, StringComparer.OrdinalIgnoreCase)).ToList();
        if (same.Count > 0) said.Add(new PressSaid("opinion-same-agent", ("agents", string.Join(", ", same))));
        if (landing) said.Add(new PressSaid("opinion-waiting"));
        if (steps) said.Add(new PressSaid("opinion-step-waiting"));
        if (rule.Verify) said.Add(new PressSaid("opinion-safe-not-handed"));
        return said;
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
