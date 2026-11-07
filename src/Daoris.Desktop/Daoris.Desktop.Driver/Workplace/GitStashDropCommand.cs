namespace Daoris.Driver;

/// <summary>What a terminal asked of a stash Drop: the stash's commit id in a repository's checkout, <see cref="Yes"/> to drop it.</summary>
public sealed record GitStashDropVerb(string Repository, string Id, bool Yes);

/// <summary>What <see cref="GitStashDropCommand"/> is handed: where a repository's checkout is, the act journal, and the context of a working-tree act there (the switch and the sessions beside).</summary>
public sealed record GitStashWorld(Func<string, CancellationToken, Task<string?>> CheckoutOf, Func<ActJournal> Journal, Func<string, string, StageContext> ContextOf);

/// <summary>
/// <c>daoris git stash drop &lt;repository&gt; &lt;commit&gt; [--plan|--yes]</c> (GITAPP2, D204; the git-writes design §9): the terminal's twin of the
/// Changes tab's stash <i>Drop</i>. The stash is named by its commit id, as the page names it, never by its place on the shared list. Without
/// <c>--yes</c> (or with <c>--plan</c>) it says what it would drop and changes nothing. With <c>--yes</c> it is the running Daoris's client
/// (<see cref="InstanceDoor.PressAsync"/>): the person's press, confirmed in the window; with no Daoris running it drops through the same act
/// (<see cref="StashDrops"/>), refused from a session's terminal (<c>DAORIS_SESSION_ID</c>).
/// </summary>
/// <remarks>Exit codes: 0 done or planned · 1 refused, nothing changed · 2 could not, or the outcome is not known.</remarks>
public static class GitStashDropCommand
{
    public const string Usage = "usage: daoris git stash drop <repository> <commit> [--plan | --yes]";

    private const string Prefix = "daoris git stash drop";

    /// <summary>Whether the words after <c>git</c> are this verb's.</summary>
    public static bool Answers(IReadOnlyList<string> args) => args.Count > 0 && args[0] == "stash";

    /// <summary>Whether the words drop (<c>--yes</c>), so the verb holds the home's lease (<see cref="VerbLeases"/>); a plan does not.</summary>
    public static bool Changes(IReadOnlyList<string> args) => Answers(args) && args.Contains("--yes");

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static GitStashDropVerb? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count < 2 || args[1] != "drop")
        {
            problem = "`git stash` takes `drop`.";
            return null;
        }

        var words = new List<string>();
        bool? yes = null;
        foreach (var word in args.Skip(2))
        {
            if (word is "--yes" or "--plan")
            {
                if (yes is not null && yes != (word == "--yes")) { problem = "`--plan` and `--yes` are two answers; give one."; return null; }
                yes = word == "--yes";
            }
            else if (word.StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"`{word}` is not a word `git stash drop` takes.";
                return null;
            }
            else
            {
                words.Add(word);
            }
        }

        if (words.Count != 2)
        {
            problem = "`git stash drop` takes a repository and the stash's commit id.";
            return null;
        }

        return new GitStashDropVerb(words[0], words[1], yes == true);
    }

    /// <summary>The verb's payload: the route's own fields beside <c>said: true</c> (RPC1e).</summary>
    public static Dictionary<string, object?> Payload(GitStashDropVerb verb) => InstanceDoor.Said(("repository", verb.Repository), ("id", verb.Id));

    public static async Task<int> RunAsync(IReadOnlyList<string> args, GitStashWorld world, InstanceWorld instance, CancellationToken ct)
    {
        if (Read(args, out var problem) is not { } verb)
        {
            await instance.Error.WriteLineAsync($"{Prefix}: {problem}\n{Usage}").ConfigureAwait(false);
            return 2;
        }

        if (!verb.Yes) return await PlanAsync(verb, world, instance.Out, instance.Error, ct).ConfigureAwait(false);
        return await InstanceDoor.PressAsync(PersonPresses.GitStashDrop, Payload(verb), async () =>
        {
            if (instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await instance.Error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            return await ExecuteAsync(verb, world, instance.Out, instance.Error, ct).ConfigureAwait(false);
        }, TreesSaid.Words(instance.Out, instance.Error, instance.Verb), instance, ct).ConfigureAwait(false);
    }

    /// <summary>What the act would do, said, and nothing written.</summary>
    public static async Task<int> PlanAsync(GitStashDropVerb verb, GitStashWorld world, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        var (message, why) = await new StashDrops(world.Journal()).PlanAsync(world.ContextOf(verb.Repository, checkout), verb.Id, ct).ConfigureAwait(false);
        if (why is not null)
        {
            await error.WriteLineAsync($"{Prefix}: {why}").ConfigureAwait(false);
            return 1;
        }

        await output.WriteLineAsync($"would drop the stash `{verb.Id[..Math.Min(12, verb.Id.Length)]}` ({message}) of {verb.Repository}, a Save of Daoris's own; nothing was changed.").ConfigureAwait(false);
        await output.WriteLineAsync($"to put it back: {StashDrops.StoreCommand(verb.Id, message!)}, until git clears unused objects.").ConfigureAwait(false);
        await output.WriteLineAsync("--yes drops it; Daoris running asks for your yes in its window first.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>The act run and its outcome said: this process's own run, and the words the instance answers for the terminal.</summary>
    public static async Task<int> ExecuteAsync(GitStashDropVerb verb, GitStashWorld world, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        var run = await new StashDrops(world.Journal()).RunAsync(world.ContextOf(verb.Repository, checkout), new StashDropPress(verb.Id), ct).ConfigureAwait(false);
        if (run.Refused)
        {
            await error.WriteLineAsync($"{Prefix}: {run.Said}").ConfigureAwait(false);
            return 1;
        }

        switch (run.Outcome)
        {
            case ActOutcomes.Done:
                await output.WriteLineAsync($"dropped the stash of {verb.Repository} (act {run.Act}). {run.Notes}").ConfigureAwait(false);
                return 0;
            case ActOutcomes.NotDone:
                await error.WriteLineAsync($"{Prefix}: the stash was not dropped (act {run.Act}). {run.Notes}").ConfigureAwait(false);
                return 1;
            default:
                await error.WriteLineAsync($"{Prefix}: Daoris could not tell whether this finished; nothing was retried (act {run.Act}). {run.Said}").ConfigureAwait(false);
                return 2;
        }
    }

    private static async Task<string?> CheckoutAsync(GitStashDropVerb verb, GitStashWorld world, TextWriter error, CancellationToken ct)
    {
        var root = await world.CheckoutOf(verb.Repository, ct).ConfigureAwait(false);
        if (root is not null && Directory.Exists(root)) return root;
        await error.WriteLineAsync($"{Prefix}: {verb.Repository} has no checkout on this machine; nothing was changed.").ConfigureAwait(false);
        return null;
    }
}
