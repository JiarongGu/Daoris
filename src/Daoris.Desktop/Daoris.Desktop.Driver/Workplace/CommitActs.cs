using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The acts of this class, by the kind their record carries.</summary>
public static class CommitVerbs
{
    public const string Commit = "commit";

    public const string Amend = "amend";
}

/// <summary>
/// A commit or an amend as the page pressed it: the message, and what it read: the commit HEAD named (null on a branch with none) and the tree
/// the index held (<see cref="CommitPlan.Tree"/>).
/// </summary>
public sealed record CommitPress(string Verb, string Message, string? Head, string Tree);

/// <summary>What a commit or an amend rests on, read at the plan: the tip, the branch (null when detached), the index as a tree and the hooks the act runs.</summary>
/// <param name="Tree">Null where the index cannot be made a tree; <paramref name="Why"/> says why.</param>
/// <param name="Empty">Whether the act would change nothing for want of a staged change.</param>
/// <param name="Message">HEAD's message, which an amend starts from; null on an unborn branch.</param>
/// <param name="Hooks">The hooks present that this verb's git runs, never skipped.</param>
/// <param name="Identity">The name and email the commit carries, or null where git has none to give.</param>
public sealed record CommitPlan(string? Head, string? Branch, string? Tree, string? Why, bool Empty, string? Message, IReadOnlyList<string> Hooks, string? Identity);

/// <summary>
/// **Commit and amend**. One <c>commit</c> step of the act journal, in the act's job, so a hook and its
/// children end with it and none is skipped (no <c>--no-verify</c>); git's own auto maintenance runs after it as it does for the person's own
/// commit. An amend first keeps a recovery ref at the old tip under <see cref="ContentHolds.DiscardedPrefix"/>, in a step of its own. The commit
/// carries the author of <see cref="GitAuthor"/>. It is reconciled by HEAD against the old tip and the plan's message
/// (<see cref="ActCommits"/>), never re-run; what a hook changed in it is said.
/// </summary>
public sealed class CommitActs(ActJournal journal)
{
    internal ActRuns Runs { get; } = new(journal);

    /// <summary>The plan: what a press of <paramref name="verb"/> would rest on, read fresh and changing nothing.</summary>
    public async Task<GitAnswer<CommitPlan>> PlanAsync(string root, string verb, CancellationToken ct)
    {
        IReadOnlyList<string> commands = ["git write-tree (on a copy of the index)", "git rev-parse HEAD", "git var GIT_COMMITTER_IDENT"];
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, GitReadRefusal.NotTop, "This folder is not the top of a repository of its own, so git is not asked.") { Commands = commands };
        }

        var layout = await StageActs.LayoutAsync(root).ConfigureAwait(false);
        var state = layout is null ? null : await StateAsync(root, layout.Value.GitDir, ct).ConfigureAwait(false);
        if (state is null) return new(null, GitReadRefusal.NotAnswered, "git could not read this checkout: it may be busy, or git did not start.") { Commands = commands };
        var author = await GitAuthor.OfAsync(journal.Home, root, ct).ConfigureAwait(false);
        var hooks = await HooksAsync(root, verb == CommitVerbs.Amend, ct).ConfigureAwait(false);
        var plan = new CommitPlan(
            state.Head, state.Branch, state.Tree, state.Tree is null ? "a file in conflict stands in the index, so it cannot be committed" : null,
            verb == CommitVerbs.Amend ? state.Head is null : NothingWhy(state, verb, "") is not null, state.Message, hooks, await IdentityAsync(root, author, ct).ConfigureAwait(false));
        return new(plan, GitReadRefusal.None, null) { Commands = commands };
    }

    public async Task<ActRun> RunAsync(StageContext context, CommitPress press, CancellationToken ct)
    {
        var amend = press.Verb == CommitVerbs.Amend;
        if (press.Verb is not (CommitVerbs.Commit or CommitVerbs.Amend)) return Refused($"`{press.Verb}` is no commit act");
        var message = ActCommits.Normalize(press.Message);
        if (message.Length == 0) return Refused("a commit needs a message");
        if (await StageActs.GuardAsync(context, "this act", ct).ConfigureAwait(false) is { } guarded) return Refused(guarded);
        var layout = (await StageActs.LayoutAsync(context.Checkout).ConfigureAwait(false))!.Value;
        var (stale, state) = await JudgeStateAsync(context.Checkout, layout.GitDir, press, message, ct).ConfigureAwait(false);
        if (stale is not null) return Refused(stale);
        state = state!;

        var author = await GitAuthor.OfAsync(journal.Home, context.Checkout, ct).ConfigureAwait(false);
        var identity = await IdentityAsync(context.Checkout, author, ct).ConfigureAwait(false);
        if (identity is null) return Refused(Said("git has no name and email to commit as; set `user.name` and `user.email`, or a git identity that commits as one"));

        var hooks = await HooksAsync(context.Checkout, amend, ct).ConfigureAwait(false);
        var steps = new List<ActStepPlan>();
        string? recovery = null;
        if (amend)
        {
            var named = await RecoveryAsync(context.Checkout, state.Branch, state.Head!, ct).ConfigureAwait(false);
            if (named.Refusal is not null) return Refused(named.Refusal);
            recovery = named.Ref;
            if (named.Made)
            {
                steps.Add(ActSteps.Ref(new RefTransaction().Create(recovery!, state.Head!), [(recovery!, null, state.Head)]));
            }
        }

        var who = GitAuthor.Arguments(author);
        IReadOnlyList<string> parents = amend ? state.Parents : state.Head is null ? [] : [state.Head];
        steps.Add(ActCommits.Step(who, message, state.Head, state.Branch, amend, state.Tree!, parents, hooks));
        var plan = new JsonObject
        {
            ["verb"] = press.Verb, ["head"] = state.Head, ["branch"] = state.Branch, ["tree"] = state.Tree, ["message"] = message,
            ["recovery"] = recovery, ["identity"] = identity, ["switch"] = true, ["hooks"] = new JsonArray([.. hooks.Select(each => (JsonNode)each)]),
            ["maintenance"] = "git's own auto maintenance runs after the commit as it does for the person's own",
        };
        var act = new ActPlan(press.Verb, "window", context.Repository, context.Checkout, steps)
        {
            Plan = plan.ToJsonString(),
            Judge = async token =>
            {
                if (await StageActs.GuardAsync(context, "this act", token).ConfigureAwait(false) is { } guard) return guard;
                return (await JudgeStateAsync(context.Checkout, layout.GitDir, press, message, token).ConfigureAwait(false)).Refusal;
            },
        };
        var run = await Runs.RunAsync(act, ct).ConfigureAwait(false);
        return run.Refused || run.Act is null ? run : await ClosedAsync(context.Checkout, run, state, message, recovery, steps.Count > 1, ct).ConfigureAwait(false);
    }

    /// <summary>The run said with what the commit's hooks changed in it, and, where an amend changed nothing, its recovery ref let go.</summary>
    private async Task<ActRun> ClosedAsync(string root, ActRun run, State state, string message, string? recovery, bool made, CancellationToken ct)
    {
        var none = CancellationToken.None;
        var notes = new List<string>();
        if (run.Outcome == ActOutcomes.Done)
        {
            if (await ActCommits.RewriteWordsAsync(root, state.Tree, message, none).ConfigureAwait(false) is { } rewrite) notes.Add(char.ToUpperInvariant(rewrite[0]) + rewrite[1..] + ".");
        }
        else if (journal.Steps(run.Act!).LastOrDefault(each => each.Kind == ActStepKinds.Commit)?.Said is { Length: > 0 } why)
        {
            notes.Add(char.ToUpperInvariant(why[0]) + why[1..] + ".");
        }

        if (made && run.Outcome == ActOutcomes.Partial && (await ActCommits.HeadAsync(root, none).ConfigureAwait(false)).Id == state.Head)
        {
            var gone = await new RefTransaction().Delete(recovery!, state.Head!).RunAsync(root, none).ConfigureAwait(false);
            notes.Add(gone.Done ? "Nothing was amended, so the recovery ref was let go." : $"Nothing was amended; its recovery ref `{recovery}` stands.");
        }

        return notes.Count == 0 ? run : run with { Said = $"{run.Said} {string.Join(' ', notes)}", Notes = string.Join(' ', notes) };
    }

    // ——— The reads an act rests on.

    private sealed record State(string? Head, string? Branch, string? Tree, string? Message, IReadOnlyList<string> Parents, string? HeadTree);

    private static async Task<State?> StateAsync(string root, string gitDir, CancellationToken ct)
    {
        var head = await ActCommits.HeadAsync(root, ct).ConfigureAwait(false);
        if (head.Unread) return null;
        var (symbolic, branch, _) = await WorkingTree.GitAsync(root, ["symbolic-ref", "-q", "HEAD"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (symbolic is not (0 or 1)) return null;
        var tree = await ActCommits.TreeAsync(root, gitDir, ct).ConfigureAwait(false);
        if (head.Id is null) return new(null, symbolic == 0 ? branch.Trim() : null, tree, null, [], null);
        return await ActCommits.FactsAsync(root, head.Id, ct).ConfigureAwait(false) is not { } facts
            ? null
            : new(head.Id, symbolic == 0 ? branch.Trim() : null, tree, facts.Message, facts.Parents, facts.Tree);
    }

    /// <summary>Why the act would change nothing: no staged change (and, for an amend, no new message); null where it would.</summary>
    private static string? NothingWhy(State state, string verb, string message)
    {
        if (state.Tree is null) return null;
        if (verb == CommitVerbs.Amend)
        {
            if (state.Head is null) return "there is no commit to amend";
            return state.Tree == state.HeadTree && ActCommits.Normalize(message) == ActCommits.Normalize(state.Message ?? "")
                ? "an amend would change neither the files nor the message"
                : null;
        }

        return state.Tree == (state.Head is null ? EmptyTree.Of(state.Tree) : state.HeadTree) ? "nothing is staged" : null;
    }

    /// <summary>The checkout as it stands against the press: why the act is refused now, or null; with the state read.</summary>
    private async Task<(string? Refusal, State? State)> JudgeStateAsync(string root, string gitDir, CommitPress press, string message, CancellationToken ct)
    {
        if (await StateAsync(root, gitDir, ct).ConfigureAwait(false) is not { } state) return (Said("git could not read the checkout's HEAD and index"), null);
        if (state.Head != press.Head) return (Said("HEAD moved since the staged changes were read"), null);
        if (state.Tree is null) return (Said("a file in conflict stands in the index, so it is resolved, not committed"), null);
        if (state.Tree != press.Tree) return (Said("the index changed since it was read"), null);
        return NothingWhy(state, press.Verb, message) is { } nothing ? (Said(nothing), null) : (null, state);
    }

    /// <summary>The hooks of this verb's git that are present, in git's order.</summary>
    private static async Task<IReadOnlyList<string>> HooksAsync(string root, bool amend, CancellationToken ct)
    {
        var (code, path, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--git-path", "hooks/pre-commit"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        if (code != 0 || Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, path.Trim()))) is not { } folder) return [];
        string[] names = amend ? [.. ActCommits.CommitHooks, ActCommits.Rewrite] : ActCommits.CommitHooks;
        return [.. names.Where(name => File.Exists(Path.Combine(folder, name)))];
    }

    /// <summary>The name and email the commit carries, as git would print them, or null where git has none.</summary>
    internal static async Task<string?> IdentityAsync(string root, (string Name, string Email)? author, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, [.. GitAuthor.Arguments(author), "var", "GIT_COMMITTER_IDENT"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        var line = said.Trim();
        var close = line.LastIndexOf('>');
        return code != 0 || close < 0 ? null : line[..(close + 1)];
    }

    /// <summary>The amend's recovery ref, named for the branch and the tip so a second amend of the same tip finds it: to make, or already there.</summary>
    private static async Task<(string? Ref, bool Made, string? Refusal)> RecoveryAsync(string root, string? branch, string head, CancellationToken ct)
    {
        var name = branch is null ? "HEAD" : GitRefActs.ShortName(branch);
        var reference = $"{ContentHolds.DiscardedPrefix}amended/{name}-{head[..12]}";
        var (code, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
        if (code == 0) return at.Trim() == head ? (reference, false, null) : (null, false, Said($"`{reference}` stands at another commit"));
        if (code != 1) return (null, false, Said("git could not read whether the recovery ref stands"));
        var (missing, _) = await ContentHolds.MissingAsync(root, reference, ct).ConfigureAwait(false);
        return missing ? (reference, true, null) : (null, false, Said("git could not read whether the recovery ref stands"));
    }

    private static string Said(string why) => $"{why}: nothing was changed.";

    private static ActRun Refused(string said) => new(null, null, said, true);
}

/// <summary>The empty tree of a repository's object format, told from a tree id's length.</summary>
internal static class EmptyTree
{
    private const string Sha1 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    private const string Sha256 = "6ef19b41225c5369f1c104d45d8d85efa9b057b53b14b4b9b939dd74decc5321";

    public static string Of(string tree) => tree.Length == Sha256.Length ? Sha256 : Sha1;
}
