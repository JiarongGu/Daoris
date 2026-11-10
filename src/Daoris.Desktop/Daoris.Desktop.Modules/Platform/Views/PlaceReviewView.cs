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

        var notes = new List<string>();
        if (edit.Put is { Procedure: { } procedure })
        {
            if (!scoped.Circle)
            {
                var root = machine.Facts.Registered.FirstOrDefault(each => string.Equals(each.Repository, scoped.Repository, StringComparison.OrdinalIgnoreCase)).Root;
                if (root is not { Length: > 0 })
                {
                    notes.Add($"Not checked: `{scoped.Repository}` has no checkout on this machine; a set-up step sits until its tree holds `{procedure}`.");
                }
                else if (!ReviewRules.Holds(root, procedure)) return PressViewed.Refused(ReviewRules.NotHeld(scoped.Repository!, procedure));
                else notes.Add($"`{procedure}` is in `{scoped.Repository}`'s checkout here.");
            }
            else notes.AddRange(ReviewRules.Lacking(machine.Facts.Registered, edit, procedure).Select(lacking => ReviewRules.SitsUntil(lacking, procedure)));
        }

        var whose = scoped.Circle ? $"each repository of workspace `{scoped.Workspace}` that sets none of its own" : $"`{scoped.Repository}`";
        var head = edit.Clear
            ? scoped.Circle
                ? $"Clears workspace `{scoped.Workspace}`'s review rule: each repository there keeps its own, else none."
                : $"Clears `{scoped.Repository}`'s review rule: it takes its workspace's again, else none."
            : edit.None ? $"Declares that `{scoped.Repository}` has no review environment, whatever its workspace says."
            : edit.Drop is not null ? $"Stops declaring `{edit.Drop}` for {whose}."
            : edit.Put is null ? $"Makes {(scoped.Circle ? $"workspace `{scoped.Workspace}`'s" : $"`{scoped.Repository}`'s")} review rule {(edit.Required == true ? "required" : "not required")}."
            : $"Declares the review environment `{edit.Put.Name}` for {whose}.";
        var map = scoped.Circle ? after.WorkspaceReviews : after.Reviews;
        List<string> said = map.TryGetValue((scoped.Circle ? scoped.Workspace : scoped.Repository)!, out var standing)
            ? [.. ReviewRules.Says(standing), .. ReviewRules.Gate(standing)]
            : [];
        var facts = scoped.Facts();
        facts["describe"] = string.Join(" ", [head, .. said, .. notes]);
        return PressViewed.Shown(new PressView("review", facts, $"daoris place review {scoped.Words} {Words(edit)}"));
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
