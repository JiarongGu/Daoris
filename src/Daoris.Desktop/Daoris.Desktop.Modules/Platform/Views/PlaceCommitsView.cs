using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place commits</c> as the card shows it (<c>SET_COMMITS</c>): how a repository's, or each workspace repository's, work is
/// committed, and the convention its messages follow. Judged by the rule table as the route is (<see cref="CommitRules.Apply"/>) against
/// the rule the machine holds now; the card says how work is then committed and quotes the convention as it will be kept. A refusal is the
/// table's own, back to the terminal with no card.
/// </summary>
public sealed class PlaceCommitsView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_COMMITS";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, "a commit rule", true, cancellationToken).ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);

        var edit = (payload is { } body ? CommitRules.EditOf(body) : new CommitEdit()) with { Repository = scoped.Repository, Workspace = scoped.Workspace };
        if (!edit.Clear && edit.Set is not { Names: true })
        {
            return PressViewed.Refused("a commit change names `--mode`, `--convention`, `--no-convention` or `--clear` — `daoris place commits --help`.");
        }

        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        DriverConfig after;
        try
        {
            after = CommitRules.Apply(machine.Config, edit);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var whose = scoped.Circle ? $"each repository of workspace `{scoped.Workspace}` that sets none of its own" : $"`{scoped.Repository}`";
        var describe = (scoped.Circle ? after.WorkspaceCommits : after.Commits).TryGetValue((scoped.Circle ? scoped.Workspace : scoped.Repository)!, out var rule)
            ? $"Sets the commit rule for {whose}: `{rule.Mode}`. " + CommitRules.Says(rule.Mode) + (rule.Convention is { } convention ? $" Commit messages follow this convention: \"{convention}\"" : "")
            : scoped.Circle
                ? $"Clears workspace `{scoped.Workspace}`'s commit rule: each repository there keeps its own, else work is committed as it goes."
                : $"Clears `{scoped.Repository}`'s commit rule: it takes its workspace's again, else work is committed as it goes.";
        var facts = scoped.Facts();
        facts["describe"] = describe;
        return PressViewed.Shown(new PressView("commits", facts, $"daoris place commits {scoped.Words} {Words(edit)}"));
    }

    private static string Words(CommitEdit edit)
    {
        if (edit.Clear) return "--clear";
        var words = new List<string>();
        if (edit.Set!.Mode is { } mode) words.AddRange(["--mode", mode]);
        if (edit.Set.Convention is string { Length: 0 }) words.Add("--no-convention");
        else if (edit.Set.Convention is string convention) words.AddRange(["--convention", ShellWord.Of(convention, "<convention>")]);
        return string.Join(' ', words);
    }
}
