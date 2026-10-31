using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The switch step and the stash step: their plans, their effects and their postconditions (GITAPP2k, D204; the git-writes design §5, §8). Each
/// effect is one git run once in the act's job, so a <c>post-checkout</c> hook and its children end with it; none is run again, and what
/// each did is read from <c>HEAD</c> or from <c>refs/stash</c>, never from the exit code.
/// </summary>
internal static class ActWorkSteps
{
    /// <summary>A switch step: the branch HEAD named (null when detached) and the commit it stood on, and the branch asked (full ref names).</summary>
    public static ActStepPlan Switch(string? from, string head, string to)
    {
        var said = new JsonObject { ["to"] = to }.ToJsonString();
        var read = new JsonObject { ["from"] = from, ["head"] = head, ["to"] = to }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Switch, said, read);
    }

    /// <summary>A stash step: <paramref name="author"/> as <c>-c</c> words, whether untracked files go, the message (null for git's own), the tip HEAD stood on and the shared stash's tip.</summary>
    public static ActStepPlan Stash(IReadOnlyList<string> author, bool untracked, string? message, string head, string? stash)
    {
        var said = new JsonObject
        {
            ["author"] = new JsonArray([.. author.Select(each => (JsonNode)each)]), ["untracked"] = untracked, ["message"] = message,
        }.ToJsonString();
        var read = new JsonObject { ["head"] = head, ["stash"] = stash, ["untracked"] = untracked }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Stash, said, read);
    }

    public static async Task<ActStepEnd> SwitchAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var to = JsonNode.Parse(step.Arguments)!["to"]!.GetValue<string>();
        var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, ["switch", "--no-guess", GitRefActs.ShortName(to)], null, null).ConfigureAwait(false);
        var words = string.Join(' ', new[] { run.Stderr, run.Stdout }.Select(each => each.Trim()).Where(each => each.Length > 0));
        return run.End(run.Exit == 0 ? "git switched the checkout" : $"git refused the switch ({ContentHolds.Said(words)})");
    }

    public static async Task<ActStepEnd> StashAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var plan = JsonNode.Parse(step.Arguments)!;
        var who = plan["author"]!.AsArray().Select(each => each!.GetValue<string>()).ToList();
        List<string> arguments = [.. who, "stash", "push"];
        if (plan["untracked"]!.GetValue<bool>()) arguments.Add("--include-untracked");
        if (plan["message"] is JsonValue { } message && message.TryGetValue<string>(out var text))
        {
            arguments.Add("--message");
            arguments.Add(text);
        }

        var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, arguments, null, null).ConfigureAwait(false);
        var words = string.Join(' ', new[] { run.Stderr, run.Stdout }.Select(each => each.Trim()).Where(each => each.Length > 0));
        return run.End(run.Exit == 0 ? "git saved the stash" : $"git refused the stash ({ContentHolds.Said(words)})");
    }

    /// <summary>Done where HEAD names the branch asked, not done where it stands as it did, else unknown; null where git could not say.</summary>
    public static async Task<string?> ReadSwitchAsync(string root, JsonNode plan, CancellationToken ct)
    {
        var (from, head, to) = (plan["from"]?.GetValue<string>(), plan["head"]!.GetValue<string>(), plan["to"]!.GetValue<string>());
        var (code, said, _) = await WorkingTree.GitAsync(root, ["symbolic-ref", "-q", "HEAD"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (code is not (0 or 1)) return null;
        if (code == 0)
        {
            var now = said.Trim();
            return now == to ? ActOutcomes.Done : now == from ? ActOutcomes.NotDone : ActOutcomes.Unknown;
        }

        var tip = await ActCommits.HeadAsync(root, ct).ConfigureAwait(false);
        if (tip.Unread) return null;
        return from is null && tip.Id == head ? ActOutcomes.NotDone : ActOutcomes.Unknown;
    }

    /// <summary>Done where <c>refs/stash</c> moved to a commit on HEAD's tip, not done where it did not move, else unknown; null where git could not say.</summary>
    public static async Task<string?> ReadStashAsync(string root, JsonNode plan, CancellationToken ct)
    {
        var (head, before) = (plan["head"]!.GetValue<string>(), plan["stash"]?.GetValue<string>());
        var now = await StashTipAsync(root, ct).ConfigureAwait(false);
        if (now.Unread) return null;
        if (now.Id == before) return ActOutcomes.NotDone;
        if (now.Id is null) return ActOutcomes.Unknown;
        return await ActCommits.ParentsAsync(root, now.Id, ct).ConfigureAwait(false) is { } parents && parents.Count > 0 && parents[0] == head
            ? ActOutcomes.Done
            : ActOutcomes.Unknown;
    }

    /// <summary>The shared stash's tip, null where none stands; <c>Unread</c> where git could not say.</summary>
    public static async Task<(string? Id, bool Unread)> StashTipAsync(string root, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "refs/stash"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        return code == 0 && WorkingTree.IsCommitId(said.Trim()) ? (said.Trim(), false) : code == 1 ? (null, false) : (null, true);
    }

    /// <summary>The hooks of <paramref name="names"/> that are present in the repository's hooks folder, in the order given.</summary>
    public static async Task<IReadOnlyList<string>> HooksAsync(string root, string[] names, CancellationToken ct)
    {
        var (code, path, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--git-path", "hooks/" + names[0]], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (code != 0 || Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, path.Trim()))) is not { } folder) return [];
        return [.. names.Where(name => File.Exists(Path.Combine(folder, name)))];
    }
}
