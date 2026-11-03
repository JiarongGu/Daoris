namespace Daoris.Driver;

/// <summary>What a terminal asked of a branch's history: <c>pick</c>, <c>revert</c> or <c>rebase</c>, in a repository, <see cref="Yes"/> to make it.</summary>
/// <param name="Target">The commit or <c>A..B</c> range to pick or revert, or the commit to rebase onto.</param>
public sealed record GitHistoryVerb(string Edit, string Repository, string Branch, string Target, bool Yes);

/// <summary>What <see cref="GitHistoryCommand"/> is handed: where a repository's checkout is, its line, the runs of acts this process holds, and the home.</summary>
/// <param name="CheckoutOf">The repository's checkout on this machine, or null where it has none.</param>
/// <param name="LineOf">The repository's line, by its name and its checkout; null where none is known.</param>
public sealed record GitHistoryWorld(
    Func<string, CancellationToken, Task<string?>> CheckoutOf, Func<string, string, CancellationToken, Task<string?>> LineOf, Func<ActRuns> Runs, string Home);

/// <summary>
/// <c>daoris git pick|revert &lt;repository&gt; &lt;branch&gt; &lt;commit|A..B&gt;</c> and <c>git rebase &lt;repository&gt; &lt;branch&gt; &lt;onto&gt;</c>,
/// each <c>[--plan|--yes]</c> (GITAPP2m, D204; the git-writes design §7, §9): the terminal's twin of a branch page's <i>Edit history</i>. Without
/// <c>--yes</c> it says the plan and changes nothing. With <c>--yes</c> it says the plan, then is the running Daoris's client
/// (<see cref="InstanceDoor.PressAsync"/>), the press carrying the ids the plan pinned and confirmed in the window; with no Daoris running
/// it makes the edit itself through the same act (<see cref="GitHistoryEdits"/>), saying each commit made, refused from a session's terminal.
/// </summary>
/// <remarks>Exit codes: 0 done or planned · 1 refused, nothing changed · 2 could not, or the outcome is not known.</remarks>
public static class GitHistoryCommand
{
    public const string Usage =
        "usage: daoris git pick|revert <repository> <branch> <commit|A..B> [--plan | --yes]\n"
        + "       daoris git rebase <repository> <branch> <onto> [--plan | --yes]";

    private const string Prefix = "daoris git";

    /// <summary>Whether the words after <c>git</c> are these verbs'.</summary>
    public static bool Answers(IReadOnlyList<string> args) => args.Count > 0 && args[0] is GitHistoryEdits.Pick or GitHistoryEdits.Revert or GitHistoryEdits.Rebase;

    /// <summary>Whether the words make an edit (<c>--yes</c>), so the verb holds the home's lease (<see cref="VerbLeases"/>); a plan does not.</summary>
    public static bool Changes(IReadOnlyList<string> args) => Answers(args) && args.Contains("--yes");

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static GitHistoryVerb? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        var words = new List<string>();
        bool? yes = null;
        foreach (var word in args.Skip(1))
        {
            if (word is "--yes" or "--plan")
            {
                if (yes is not null && yes != (word == "--yes")) { problem = "`--plan` and `--yes` are two answers; give one."; return null; }
                yes = word == "--yes";
            }
            else if (word.StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"`{word}` is not a word `git {args[0]}` takes.";
                return null;
            }
            else
            {
                words.Add(word);
            }
        }

        if (words.Count != 3)
        {
            problem = args[0] == GitHistoryEdits.Rebase
                ? "`git rebase` takes a repository, a branch and the commit to rebase it onto."
                : $"`git {args[0]}` takes a repository, a branch and a commit or an `A..B` range.";
            return null;
        }

        return new GitHistoryVerb(args[0], words[0], words[1], words[2], yes == true);
    }

    /// <summary>The verb's payload: the route's own fields and the plan's pinned ids beside <c>said: true</c> (RPC1e).</summary>
    public static Dictionary<string, object?> Payload(GitHistoryVerb verb, HistoryEditPins pins) =>
        InstanceDoor.Said(
            ("repository", verb.Repository), ("edit", verb.Edit), ("branch", verb.Branch), ("target", verb.Target),
            ("pins", new Dictionary<string, object?> { ["tip"] = pins.Tip, ["onto"] = pins.Onto, ["commits"] = pins.Commits.ToArray() }));

    public static async Task<int> RunAsync(IReadOnlyList<string> args, GitHistoryWorld world, InstanceWorld instance, CancellationToken ct)
    {
        if (Read(args, out var problem) is not { } verb)
        {
            await instance.Error.WriteLineAsync($"{Prefix}: {problem}\n{Usage}").ConfigureAwait(false);
            return 2;
        }

        if (await AskAsync(verb, world, instance.Error, ct).ConfigureAwait(false) is not { } ask) return 2;
        var (plan, refusal) = await GitHistoryEdits.PlanAsync(ask, ct).ConfigureAwait(false);
        if (plan is null)
        {
            await instance.Error.WriteLineAsync($"{Prefix} {verb.Edit}: {Ended(refusal!)}").ConfigureAwait(false);
            return 1;
        }

        await SayPlanAsync(verb, plan, instance.Out, nothing: !verb.Yes).ConfigureAwait(false);
        if (!verb.Yes) return 0;
        return await InstanceDoor.PressAsync(PersonPresses.GitHistoryEdit, Payload(verb, plan.Pins), async () =>
        {
            if (instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await instance.Error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            return await ExecuteAsync(verb, plan.Pins, world, "terminal", "yes", instance.Out, instance.Error, ct).ConfigureAwait(false);
        }, TreesSaid.Words(instance.Out, instance.Error, instance.Verb), instance, ct).ConfigureAwait(false);
    }

    /// <summary>The edit run with the ids a plan pinned, and its outcome said: this process's own run, and the words the instance answers for the terminal.</summary>
    public static async Task<int> ExecuteAsync(
        GitHistoryVerb verb, HistoryEditPins? pins, GitHistoryWorld world, string press, string? confirmed, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await AskAsync(verb, world, error, ct).ConfigureAwait(false) is not { } ask) return 2;
        var run = await GitHistoryEdits.EditAsync(
            world.Runs(), ask with { Pins = pins, Progress = (made, of) => output.WriteLine($"made {made} of {of}") }, press, confirmed, ct).ConfigureAwait(false);
        return await SayAsync(run, verb, output, error).ConfigureAwait(false);
    }

    private static async Task SayPlanAsync(GitHistoryVerb verb, HistoryEditPlan plan, TextWriter output, bool nothing)
    {
        var count = plan.Commits.Count == 1 ? "1 commit" : $"{plan.Commits.Count} commits";
        await output.WriteLineAsync(verb.Edit switch
        {
            GitHistoryEdits.Rebase => $"would rebase `{verb.Branch}` in {verb.Repository} onto {verb.Target} ({plan.Onto[..12]}): {count}{(nothing ? "; nothing was changed." : ".")}",
            GitHistoryEdits.Revert => $"would revert {count} on `{verb.Branch}` in {verb.Repository} at {plan.Tip[..12]}{(nothing ? "; nothing was changed." : ".")}",
            _ => $"would pick {count} onto `{verb.Branch}` in {verb.Repository} at {plan.Tip[..12]}{(nothing ? "; nothing was changed." : ".")}",
        }).ConfigureAwait(false);
        foreach (var commit in plan.Commits) await output.WriteLineAsync($"  {commit.Id[..12]} {commit.Subject}").ConfigureAwait(false);
        await output.WriteLineAsync($"the same by hand: {string.Join(" then ", plan.Commands)}").ConfigureAwait(false);
        if (nothing) await output.WriteLineAsync("--yes makes it; Daoris running asks for your yes in its window first.").ConfigureAwait(false);
    }

    private static async Task<int> SayAsync(HistoryEditRun edited, GitHistoryVerb verb, TextWriter output, TextWriter error)
    {
        var run = edited.Run;
        if (run.Refused)
        {
            await error.WriteLineAsync($"{Prefix} {verb.Edit}: {Ended(run.Said)}").ConfigureAwait(false);
            return 1;
        }

        switch (run.Outcome)
        {
            case ActOutcomes.Done:
                var did = verb.Edit switch { GitHistoryEdits.Rebase => "rebased", GitHistoryEdits.Revert => "reverted onto", _ => "picked onto" };
                await output.WriteLineAsync(
                    $"{did} `{verb.Branch}` in {verb.Repository} (act {run.Act}): it is at {edited.Tip?[..12]}; its old tip is kept at `{edited.Kept}`.").ConfigureAwait(false);
                return 0;
            case ActOutcomes.NotDone:
                await error.WriteLineAsync($"{Prefix} {verb.Edit}: nothing was changed (act {run.Act}). {run.Said}").ConfigureAwait(false);
                return 1;
            default:
                await error.WriteLineAsync(
                    $"{Prefix} {verb.Edit}: Daoris could not tell whether this finished; nothing was retried (act {run.Act}). {run.Said}").ConfigureAwait(false);
                return 2;
        }
    }

    private static string Ended(string sentence) => sentence.EndsWith('.') ? sentence : sentence + ".";

    private static async Task<HistoryEditAsk?> AskAsync(GitHistoryVerb verb, GitHistoryWorld world, TextWriter error, CancellationToken ct)
    {
        var root = await world.CheckoutOf(verb.Repository, ct).ConfigureAwait(false);
        if (root is null || !Directory.Exists(root))
        {
            await error.WriteLineAsync($"{Prefix} {verb.Edit}: {verb.Repository} has no checkout on this machine; nothing was changed.").ConfigureAwait(false);
            return null;
        }

        return new HistoryEditAsk(world.Home, verb.Repository, root, verb.Edit, verb.Branch, verb.Target)
        {
            Line = await world.LineOf(verb.Repository, root, ct).ConfigureAwait(false),
        };
    }
}
