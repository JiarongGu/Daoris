using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place review</c> as the card shows it (<c>SET_REVIEW</c>): where a repository's, or a whole workspace's, work is reviewed
/// before it lands. Judged by the rule table as the route is (<see cref="ReviewRules.Apply"/>) against the rule the machine holds now,
/// and an environment's procedure looked for in the checkouts it reaches, as the route looks; the card says what the rule lets a step do
/// and what the landing's gate does with it. A refusal is the table's own, back to the terminal with no card.
/// </summary>
public sealed class PlaceReviewView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_REVIEW";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, "a review rule", true, cancellationToken).ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);

        var edit = ReviewRules.EditOf(payload) with { Repository = scoped.Repository, Workspace = scoped.Workspace };
        if (edit is { Put: null, Drop: null, Required: null, None: false, Clear: false })
        {
            return PressViewed.Refused("a review change declares an environment, `none`, drops one, says required or not, or clears — `daoris place review --help`.");
        }

        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        DriverConfig after;
        try
        {
            after = ReviewRules.Apply(machine.Config, edit);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var notes = new List<PressSaid>();
        if (edit.Put is { Procedure: { } procedure })
        {
            if (!scoped.Circle)
            {
                var root = machine.Facts.Registered.FirstOrDefault(each => string.Equals(each.Repository, scoped.Repository, StringComparison.OrdinalIgnoreCase)).Root;
                if (root is not { Length: > 0 }) notes.Add(new PressSaid("review-unchecked", ("repository", scoped.Repository!), ("procedure", procedure)));
                else if (!ReviewRules.Holds(root, procedure)) return PressViewed.Refused(ReviewRules.NotHeld(scoped.Repository!, procedure));
                else notes.Add(new PressSaid("review-held", ("repository", scoped.Repository!), ("procedure", procedure)));
            }
            else notes.AddRange(ReviewRules.Lacking(machine.Facts.Registered, edit, procedure)
                .Select(lacking => new PressSaid("review-sits-until", ("repository", lacking), ("procedure", procedure))));
        }

        var prefix = scoped.Circle ? "review-workspace-" : "review-";
        var facts = scoped.Facts();
        string kind;
        if (edit.Clear) kind = prefix + "clear";
        else if (edit.None) kind = "review-none";
        else if (edit.Drop is not null)
        {
            kind = prefix + "drop";
            facts["environment"] = edit.Drop;
        }
        else if (edit.Put is null) kind = prefix + (edit.Required == true ? "required" : "not-required");
        else
        {
            kind = prefix + "declare";
            facts["environment"] = edit.Put.Name ?? "";
        }

        var map = scoped.Circle ? after.WorkspaceReviews : after.Reviews;
        var also = map.TryGetValue((scoped.Circle ? scoped.Workspace : scoped.Repository)!, out var standing) ? Said(standing) : [];
        return PressViewed.Shown(new PressView(kind, facts, $"daoris place review {scoped.Words} {Words(edit)}", [.. also, .. notes]));
    }

    private static List<PressSaid> Said(ReviewRule rule)
    {
        if (rule.IsNone) return [new PressSaid("review-none-here")];
        var names = rule.Environments.Select(each => each.Name).ToList();
        var said = new List<PressSaid>();
        if (rule.Required)
        {
            said.Add(new PressSaid("review-shown-required", ("environment", names[0])));
            if (names.Count > 1) said.Add(new PressSaid("review-instead", ("environments", string.Join(", ", names.Skip(1)))));
        }
        else said.Add(new PressSaid("review-optional", ("environments", string.Join(", ", names))));

        foreach (var environment in rule.Environments)
        {
            if (environment.Kind != "local")
            {
                said.Add(new PressSaid("review-procedure", ("environment", environment.Name), ("procedure", environment.Procedure)));
                continue;
            }

            said.Add(new PressSaid("review-local", ("address", environment.Address ?? "")));
            if (environment.Run is { } run) said.Add(new PressSaid("review-local-run", ("run", run)));
        }

        said.Add(new PressSaid("review-waiting"));
        return said;
    }

    private static string Words(ReviewEdit edit)
    {
        if (edit.Clear) return "--clear";
        var words = new List<string>();
        if (edit.None) words.Add("none");
        if (edit.Drop is not null) words.AddRange(["--drop", ShellWord.Of(edit.Drop, "<environment>")]);
        if (edit.Put is { } put)
        {
            words.Add(ShellWord.Of(put.Name ?? "", "<environment>"));
            if (put.Kind is not null) words.AddRange(["--kind", put.Kind]);
            if (put.Procedure is not null) words.AddRange(["--procedure", ShellWord.Of(put.Procedure, "<procedure>")]);
            if (put.Address is not null) words.AddRange(["--address", ShellWord.Of(put.Address, "<address>")]);
            if (put.Run is not null) words.AddRange(["--run", ShellWord.Of(put.Run, "<command>")]);
        }

        if (edit.Required is { } required) words.Add(required ? "--required" : "--not-required");
        return string.Join(' ', words);
    }
}
