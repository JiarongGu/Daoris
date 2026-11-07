using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The switch step and the stash step: their plans, their effects and their postconditions. Each
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

    /// <summary>A stash step that applies the stash commit <paramref name="id"/> (never pops it) to a clean checkout whose HEAD is <paramref name="head"/>.</summary>
    public static ActStepPlan StashApply(string id, string head)
    {
        var said = new JsonObject { ["mode"] = "apply", ["id"] = id }.ToJsonString();
        var read = new JsonObject { ["mode"] = "apply", ["id"] = id, ["head"] = head }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Stash, said, read);
    }

    private static bool Applies(JsonNode plan) => plan["mode"]?.GetValue<string>() == "apply";

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
        if (plan["mode"]?.GetValue<string>() == "drop") return await StashDropSteps.EndAsync(held, launch, step).ConfigureAwait(false);
        if (Applies(plan))
        {
            var applied = await launch.HeldGitAsync(launch.Root, step.Ordinal, ["stash", "apply", plan["id"]!.GetValue<string>()], null, null).ConfigureAwait(false);
            // A conflict exits 1 with the files in conflict; the stash is not dropped. The checkout decides the outcome, never the exit code.
            var said = string.Join(' ', new[] { applied.Stderr, applied.Stdout }.Select(each => each.Trim()).Where(each => each.Length > 0));
            return applied.End(applied.Exit == 0 ? "git applied the stash" : $"git applied the stash with a stop ({ContentHolds.Said(said)})");
        }

        var who =plan["author"]!.AsArray().Select(each => each!.GetValue<string>()).ToList();
        List<string> arguments = [.. who, "stash", "push"];
        if (plan["untracked"]!.GetValue<bool>()) arguments.Add("--include-untracked");
        if (plan["message"] is JsonValue { } message && message.TryGetValue<string>(out var text))
        {
            arguments.Add("--message");
            arguments.Add(text);
        }

        var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, arguments, null, null).ConfigureAwait(false);
        var words = string.Join(' ', new[] { run.Stderr, run.Stdout }.Select(each => each.Trim()).Where(each => each.Length > 0));
        // The commit saved stays in the step's words: it is what says, later, that Daoris made this stash (a Drop is offered for those alone).
        var saved = run.Exit == 0 ? (await StashTipAsync(launch.Root, CancellationToken.None).ConfigureAwait(false)).Id : null;
        return run.End(run.Exit == 0 ? $"git saved the stash{(saved is null ? "" : " " + saved)}" : $"git refused the stash ({ContentHolds.Said(words)})");
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
        if (plan["mode"]?.GetValue<string>() == "drop") return await StashDropSteps.ReadAsync(root, plan, ct).ConfigureAwait(false);
        if (Applies(plan)) return await ReadApplyAsync(root, plan["id"]!.GetValue<string>(), ct).ConfigureAwait(false);
        var (head, before) = (plan["head"]!.GetValue<string>(), plan["stash"]?.GetValue<string>());
        var now = await StashTipAsync(root, ct).ConfigureAwait(false);
        if (now.Unread) return null;
        if (now.Id == before) return ActOutcomes.NotDone;
        if (now.Id is null) return ActOutcomes.Unknown;
        return await ActCommits.ParentsAsync(root, now.Id, ct).ConfigureAwait(false) is { } parents && parents.Count > 0 && parents[0] == head
            ? ActOutcomes.Done
            : ActOutcomes.Unknown;
    }

    /// <summary>
    /// An Apply is done where the clean checkout it found now holds tracked changes, a file in conflict or the stash's untracked files; not done
    /// where it stays as clean; null where git could not say.
    /// </summary>
    private static async Task<string?> ReadApplyAsync(string root, string id, CancellationToken ct)
    {
        var (status, changed, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "status", "--porcelain=v1", "-z", "--untracked-files=no", "--no-renames"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (status != 0) return null;
        if (changed.Length > 0) return ActOutcomes.Done;
        var untracked = await UntrackedOfAsync(root, id, ct).ConfigureAwait(false);
        if (untracked is null) return null;
        return untracked.Any(path => Exists(root, path)) ? ActOutcomes.Done : ActOutcomes.NotDone;
    }

    /// <summary>The paths the stash <paramref name="id"/> holds untracked (empty where it holds none); null where git could not say.</summary>
    public static async Task<IReadOnlyList<string>?> UntrackedOfAsync(string root, string id, CancellationToken ct)
    {
        var (has, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", id + "^3"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (has == 1) return [];
        if (has != 0) return null;
        var (code, listed, _) = await WorkingTree.GitAsync(root, ["ls-tree", "-r", "--name-only", "-z", id + "^3"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        return code == 0 ? [.. listed.Split('\0', StringSplitOptions.RemoveEmptyEntries)] : null;
    }

    public static bool Exists(string root, string path)
    {
        var full = Path.Combine(root, path);
        return File.Exists(full) || Directory.Exists(full);
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
