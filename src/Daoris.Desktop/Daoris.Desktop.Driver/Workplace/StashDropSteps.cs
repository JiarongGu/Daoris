using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The step of a stash Drop: one <c>stash drop</c> of the entry whose commit is the id asked, found
/// on the shared list at the moment it runs. The stash reflog is beyond any checkout lock, so the entry's place is never trusted from the
/// plan: git names the commit it dropped, and where that is not the commit asked the entry is stored again at once and the step says so.
/// What it did is read from the list, never from the exit code.
/// </summary>
internal static partial class StashDropSteps
{
    /// <summary>A stash step that drops the stash commit <paramref name="id"/>, whose message was <paramref name="message"/>.</summary>
    public static ActStepPlan Step(string id, string message)
    {
        var said = new JsonObject { ["mode"] = "drop", ["id"] = id, ["message"] = message }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Stash, said, new JsonObject { ["mode"] = "drop", ["id"] = id }.ToJsonString());
    }

    public static async Task<ActStepEnd> EndAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var plan = JsonNode.Parse(step.Arguments)!;
        var (id, message) = (plan["id"]!.GetValue<string>(), plan["message"]?.GetValue<string>() ?? "");
        if (await StashActs.ListAsync(launch.Root, CancellationToken.None).ConfigureAwait(false) is not { } listed)
        {
            return new ActStepEnd(1, false, "git could not read the stash list, so nothing was dropped", true);
        }

        var at = listed.ToList().FindIndex(each => each.Id == id);
        if (at < 0) return new ActStepEnd(1, false, "the stash is not on the list now, so nothing was dropped", true);

        var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, ["stash", "drop", $"stash@{{{at}}}"], null, null).ConfigureAwait(false);
        var words = string.Join(' ', new[] { run.Stderr, run.Stdout }.Select(each => each.Trim()).Where(each => each.Length > 0));
        if (run.Exit != 0 || run.Unended) return run.End($"git dropped no stash ({ContentHolds.Said(words)})");

        var named = Dropped().Match(run.Stdout).Groups["id"].Value;
        if (named.Length == 0 || named == id) return run.End($"git dropped the stash `{id}`");

        // Another tree saved or dropped in between, so the place moved: put back what was dropped, by its commit.
        var other = listed.FirstOrDefault(each => each.Id == named).Message ?? "";
        var back = await launch.HeldGitAsync(launch.Root, step.Ordinal, ["stash", "store", "-m", other, named], null, null).ConfigureAwait(false);
        return new ActStepEnd(1, false, back.Exit == 0
            ? $"the shared stash moved while this ran, and git dropped `{named}`, which was stored again; the stash asked for was not dropped"
            : $"the shared stash moved while this ran, and git dropped `{named}`, which is kept by that commit id; the stash asked for was not dropped", true);
    }

    /// <summary>Done where the commit is no longer on the shared list, not done where it stands; null where git could not say.</summary>
    public static async Task<string?> ReadAsync(string root, JsonNode plan, CancellationToken ct)
    {
        if (await StashActs.ListAsync(root, ct).ConfigureAwait(false) is not { } listed) return null;
        return listed.Any(each => each.Id == plan["id"]!.GetValue<string>()) ? ActOutcomes.NotDone : ActOutcomes.Done;
    }

    [GeneratedRegex(@"\((?<id>[0-9a-f]{40,64})\)")]
    private static partial Regex Dropped();
}
