using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The act of this class, by the kind its record carries.</summary>
public static class SwitchVerbs
{
    public const string Switch = "switch";
}

/// <summary>A switch as the page pressed it: the local branch, the branch HEAD named when the page read (null when detached) and the commit it stood on.</summary>
public sealed record SwitchPress(string Branch, string? From, string Head);

/// <summary>
/// What a switch rests on, read at the plan: where HEAD is, the branch and its tip, whether tracked files are changed, the folder of another tree
/// that has the branch out, why the switch would be refused (null where it would not), and the hooks it runs.
/// </summary>
public sealed record SwitchPlan(string? From, string? Head, string To, string? ToHead, bool Dirty, string? Elsewhere, string? Why, IReadOnlyList<string> Hooks);

/// <summary>
/// **Switch**. One <c>switch</c> step of the act journal, <c>git switch --no-guess &lt;branch&gt;</c>, never
/// forced, in the act's job so a <c>post-checkout</c> hook and its children end with it. Refused where tracked files are changed, an operation is
/// in progress, the branch is out in another tree or here already, or the branch is not a local one. It is undone by switching back: the next
/// press names the branch this one left, and it is refused on the same terms.
/// </summary>
public sealed class SwitchActs(ActJournal journal)
{
    private static readonly string[] Hooks = ["post-checkout", "reference-transaction"];

    internal ActRuns Runs { get; } = new(journal);

    /// <summary>The plan: what a press of <paramref name="branch"/> would rest on, read fresh and changing nothing.</summary>
    public async Task<GitAnswer<SwitchPlan>> PlanAsync(string root, string branch, CancellationToken ct)
    {
        IReadOnlyList<string> commands = ["git rev-parse HEAD", "git symbolic-ref -q HEAD", "git status --porcelain", "git worktree list --porcelain"];
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, GitReadRefusal.NotTop, "This folder is not the top of a repository of its own, so git is not asked.") { Commands = commands };
        }

        var name = Short(branch);
        if (await StateAsync(root, name, ct).ConfigureAwait(false) is not { } state) return new(null, GitReadRefusal.NotAnswered, "git could not read this checkout: it may be busy, or git did not start.") { Commands = commands };
        var hooks = await ActWorkSteps.HooksAsync(root, Hooks, ct).ConfigureAwait(false);
        return new(new SwitchPlan(state.From is null ? null : Short(state.From), state.Head, name, state.ToHead, state.Dirty, state.Elsewhere, state.Why, hooks), GitReadRefusal.None, null) { Commands = commands };
    }

    public async Task<ActRun> RunAsync(StageContext context, SwitchPress press, CancellationToken ct)
    {
        var name = Short(press.Branch);
        if (name.Length == 0) return Refused(Said("a switch needs a branch"));
        if (await StageActs.GuardAsync(context, "this act", ct).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        var (stale, state) = await JudgeAsync(context.Checkout, press, name, ct).ConfigureAwait(false);
        if (stale is not null) return Refused(stale);
        state = state!;

        var to = "refs/heads/" + name;
        var hooks = await ActWorkSteps.HooksAsync(context.Checkout, Hooks, ct).ConfigureAwait(false);
        var plan = new JsonObject
        {
            ["verb"] = SwitchVerbs.Switch, ["from"] = state.From, ["to"] = to, ["head"] = state.Head, ["toHead"] = state.ToHead, ["switch"] = true,
            ["hooks"] = new JsonArray([.. hooks.Select(each => (JsonNode)each)]), ["undo"] = state.From is null ? "switch to any branch; HEAD was detached" : "switch back to " + Short(state.From),
        };
        var act = new ActPlan(SwitchVerbs.Switch, "window", context.Repository, context.Checkout, [ActWorkSteps.Switch(state.From, state.Head!, to)])
        {
            Plan = plan.ToJsonString(),
            Judge = async token =>
            {
                if (await StageActs.GuardAsync(context, "this act", token).ConfigureAwait(false) is { } guard) return guard;
                return (await JudgeAsync(context.Checkout, press, name, token).ConfigureAwait(false)).Refusal;
            },
        };
        var run = await Runs.RunAsync(act, ct).ConfigureAwait(false);
        if (run.Refused || run.Act is null) return run;
        if (run.Outcome == ActOutcomes.Done)
        {
            if (state.From is null) return run;
            var back = $"Switch back to `{Short(state.From)}` to undo it.";
            return run with { Notes = back, Said = $"{run.Said} {back}" };
        }

        return journal.Steps(run.Act).LastOrDefault(each => each.Kind == ActStepKinds.Switch)?.Said is { Length: > 0 } why
            ? run with { Notes = char.ToUpperInvariant(why[0]) + why[1..] + ".", Said = $"{run.Said} {char.ToUpperInvariant(why[0])}{why[1..]}." }
            : run;
    }

    // ——— The reads an act rests on.

    private sealed record State(string? From, string? Head, string? ToHead, bool Dirty, string? Elsewhere, string? Why);

    /// <summary>The checkout as it stands against a press: why the act is refused now, or null; with the state read.</summary>
    private static async Task<(string? Refusal, State? State)> JudgeAsync(string root, SwitchPress press, string name, CancellationToken ct)
    {
        if (await StateAsync(root, name, ct).ConfigureAwait(false) is not { } state) return (Said("git could not read the checkout's HEAD and branches"), null);
        if (state.Head != press.Head || (state.From is null ? null : Short(state.From)) != (press.From is null ? null : Short(press.From)))
        {
            return (Said("HEAD moved since the branches were read"), null);
        }

        return state.Why is { } why ? (Said(why), null) : (null, state);
    }

    private static async Task<State?> StateAsync(string root, string name, CancellationToken ct)
    {
        if (await PlanStatus.ReadAsync(root, ct).ConfigureAwait(false) is not { } status) return null;
        var from = status.Branch is null ? null : "refs/heads/" + status.Branch;
        var (found, tip, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "refs/heads/" + name + "^{commit}"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (found is not (0 or 1)) return null;
        var toHead = found == 0 ? tip.Trim() : null;
        var (listed, trees, _) = await WorkingTree.GitAsync(root, ["worktree", "list", "--porcelain"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (listed != 0) return null;

        var elsewhere = Elsewhere(trees, root, "refs/heads/" + name);
        var dirty = status.Tracked > 0;
        string? why = null;
        if (name.Length == 0 || name.StartsWith('-')) why = "that is no branch name";
        else if (name.StartsWith("daoris/", StringComparison.Ordinal)) why = "`daoris/` branches are Daoris's own and are not switched to here";
        else if (status.Head is null) why = "this checkout has no commit yet";
        else if (toHead is null) why = $"there is no local branch `{name}`";
        else if (from == "refs/heads/" + name) why = $"the checkout is on `{name}` already";
        else if (elsewhere is not null) why = $"`{name}` is checked out in another tree, `{elsewhere}`";
        else if (dirty) why = "tracked files are changed here; commit or stash them first, since a switch never carries them";
        return new(from, status.Head, toHead, dirty, elsewhere, why);
    }

    /// <summary>The folder name of another tree that has <paramref name="reference"/> out, or null.</summary>
    private static string? Elsewhere(string porcelain, string root, string reference)
    {
        string? path = null;
        foreach (var line in porcelain.Split('\n').Select(each => each.TrimEnd('\r')))
        {
            if (line.StartsWith("worktree ", StringComparison.Ordinal)) path = line["worktree ".Length..];
            else if (line == "branch " + reference && path is not null
                && !string.Equals(Path.GetFullPath(path).TrimEnd('\\', '/'), Path.GetFullPath(root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(path.TrimEnd('\\', '/'));
            }
        }

        return null;
    }

    private static string Short(string name) => name.StartsWith("refs/heads/", StringComparison.Ordinal) ? name["refs/heads/".Length..] : name.Trim();

    private static string Said(string why) => $"{why}: nothing was changed.";

    private static ActRun Refused(string said) => new(null, null, said, true);
}
