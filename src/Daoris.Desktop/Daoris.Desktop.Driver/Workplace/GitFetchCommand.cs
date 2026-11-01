namespace Daoris.Driver;

/// <summary>What a terminal asked of a fetch: origin's branches into a repository's checkout, <see cref="Yes"/> to make it.</summary>
public sealed record GitFetchVerb(string Repository, bool Yes);

/// <summary>
/// <c>daoris-driver git fetch &lt;repository&gt; [--plan|--yes]</c> (GITAPP2f, D204; the git-writes design §9): the terminal's twin of the Branches
/// tab's <i>Fetch</i>. Without <c>--yes</c> (or with <c>--plan</c>) it says what it would fetch and changes nothing. With <c>--yes</c> it is the
/// running Daoris's client (<see cref="InstanceDoor.PressAsync"/>): the person's press, confirmed in the window; with no Daoris running it fetches
/// itself, through the same act (<see cref="GitFetchActs"/>), refused from a session's terminal (<c>DAORIS_SESSION_ID</c>).
/// </summary>
/// <remarks>Exit codes: 0 done or planned · 1 refused, nothing changed · 2 could not, or the outcome is not known.</remarks>
public static class GitFetchCommand
{
    public const string Usage = "usage: daoris-driver git fetch <repository> [--plan | --yes]";

    private const string Prefix = "daoris-driver git fetch";

    /// <summary>Whether the words after <c>git</c> are this verb's.</summary>
    public static bool Answers(IReadOnlyList<string> args) => args.Count > 0 && args[0] == "fetch";

    /// <summary>Whether the words fetch (<c>--yes</c>), so the verb holds the home's lease (<see cref="VerbLeases"/>); a plan does not.</summary>
    public static bool Changes(IReadOnlyList<string> args) => Answers(args) && args.Contains("--yes");

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static GitFetchVerb? Read(IReadOnlyList<string> args, out string? problem)
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
                problem = $"`{word}` is not a word `git fetch` takes.";
                return null;
            }
            else
            {
                words.Add(word);
            }
        }

        if (words.Count != 1)
        {
            problem = "`git fetch` takes a repository.";
            return null;
        }

        return new GitFetchVerb(words[0], yes == true);
    }

    /// <summary>The verb's payload: the route's own fields beside <c>said: true</c> (RPC1e).</summary>
    public static Dictionary<string, object?> Payload(GitFetchVerb verb) => InstanceDoor.Said(("repository", verb.Repository));

    public static async Task<int> RunAsync(IReadOnlyList<string> args, GitRefWorld world, InstanceWorld instance, CancellationToken ct)
    {
        if (Read(args, out var problem) is not { } verb)
        {
            await instance.Error.WriteLineAsync($"{Prefix}: {problem}\n{Usage}").ConfigureAwait(false);
            return 2;
        }

        if (!verb.Yes) return await PlanAsync(verb, world, instance.Out, instance.Error, ct).ConfigureAwait(false);
        return await InstanceDoor.PressAsync(PersonPresses.GitFetch, Payload(verb), async () =>
        {
            if (instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await instance.Error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            return await ExecuteAsync(verb, world, "terminal", "yes", instance.Out, instance.Error, ct).ConfigureAwait(false);
        }, TreesSaid.Words(instance.Out, instance.Error, instance.Verb), instance, ct).ConfigureAwait(false);
    }

    /// <summary>What the act would do, said, and nothing written.</summary>
    public static async Task<int> PlanAsync(GitFetchVerb verb, GitRefWorld world, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        var (address, why) = await GitFetchActs.PlanAsync(world.Runs().Journal.Home, new FetchAsk(verb.Repository, checkout), ct).ConfigureAwait(false);
        if (why is not null)
        {
            await error.WriteLineAsync($"{Prefix}: {why}").ConfigureAwait(false);
            return 1;
        }

        await output.WriteLineAsync($"would fetch origin's branches into {verb.Repository} from {address}, moving only refs/remotes/origin/* and removing the ones origin no longer has; nothing was changed.").ConfigureAwait(false);
        await output.WriteLineAsync($"the same by hand: {GitFetchActs.Command}").ConfigureAwait(false);
        await output.WriteLineAsync("--yes fetches; Daoris running asks for your yes in its window first.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>The act run and its outcome said: this process's own run, and the words the instance answers for the terminal.</summary>
    public static async Task<int> ExecuteAsync(
        GitFetchVerb verb, GitRefWorld world, string press, string? confirmed, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        var run = await GitFetchActs.FetchAsync(world.Runs(), new FetchAsk(verb.Repository, checkout), press, confirmed, ct).ConfigureAwait(false);
        if (run.Refused)
        {
            await error.WriteLineAsync($"{Prefix}: {run.Said}").ConfigureAwait(false);
            return 1;
        }

        switch (run.Outcome)
        {
            case ActOutcomes.Done:
                await output.WriteLineAsync($"fetched origin's branches into {verb.Repository} (act {run.Act}). {run.Notes}").ConfigureAwait(false);
                return 0;
            case ActOutcomes.NotDone:
                await error.WriteLineAsync($"{Prefix}: git did not fetch; nothing of origin's changed (act {run.Act}). {run.Notes}").ConfigureAwait(false);
                return 1;
            default:
                await error.WriteLineAsync($"{Prefix}: Daoris could not tell whether this finished; nothing was retried (act {run.Act}). {run.Said}").ConfigureAwait(false);
                return 2;
        }
    }

    private static async Task<string?> CheckoutAsync(GitFetchVerb verb, GitRefWorld world, TextWriter error, CancellationToken ct)
    {
        var root = await world.CheckoutOf(verb.Repository, ct).ConfigureAwait(false);
        if (root is not null && Directory.Exists(root)) return root;
        await error.WriteLineAsync($"{Prefix}: {verb.Repository} has no checkout on this machine; nothing was changed.").ConfigureAwait(false);
        return null;
    }
}
