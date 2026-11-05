namespace Daoris.Driver;

/// <summary>What a terminal asked of a push: one branch or lightweight tag of a repository's checkout to origin, at the commit shown where named, <see cref="Yes"/> to make it.</summary>
public sealed record GitPushVerb(string Repository, string Kind, string Name, string? At, bool Yes);

/// <summary>
/// <c>daoris git push &lt;repository&gt; branch|tag &lt;name&gt; [&lt;commit&gt;] [--plan|--yes]</c> (GITAPP2g, n, D204, D241 point 2): the terminal's twin
/// of the Branches tab's <i>Push</i> and <i>Push tag</i>. Without <c>--yes</c> (or with <c>--plan</c>) it says what it would push and changes
/// nothing. With <c>--yes</c> it is the running Daoris's client (<see cref="InstanceDoor.PressAsync"/>): the person's press, confirmed in the
/// window; with no Daoris running it pushes itself, through the same act (<see cref="GitPushActs"/>), refused from a session's terminal
/// (<c>DAORIS_SESSION_ID</c>).
/// </summary>
/// <remarks>Exit codes: 0 done or planned · 1 refused, nothing changed · 2 could not, or the outcome is not known.</remarks>
public static class GitPushCommand
{
    public const string Usage = "usage: daoris git push <repository> branch|tag <name> [<commit>] [--plan | --yes]";

    private const string Prefix = "daoris git push";

    /// <summary>Whether the words after <c>git</c> are this verb's.</summary>
    public static bool Answers(IReadOnlyList<string> args) => args.Count > 0 && args[0] == "push";

    /// <summary>Whether the words push (<c>--yes</c>), so the verb holds the home's lease (<see cref="VerbLeases"/>); a plan does not.</summary>
    public static bool Changes(IReadOnlyList<string> args) => Answers(args) && args.Contains("--yes");

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static GitPushVerb? Read(IReadOnlyList<string> args, out string? problem)
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
                problem = $"`{word}` is not a word `git push` takes: it pushes one ref, never forced, never with `-u`.";
                return null;
            }
            else
            {
                words.Add(word);
            }
        }

        if (words.Count is < 3 or > 4 || words[1] is not ("branch" or "tag"))
        {
            problem = "`git push` takes a repository, `branch` or `tag`, its name, and the commit shown where you want it checked.";
            return null;
        }

        return new GitPushVerb(words[0], words[1], words[2], words.Count == 4 ? words[3] : null, yes == true);
    }

    /// <summary>The verb's payload: the route's own fields beside <c>said: true</c> (RPC1e).</summary>
    public static Dictionary<string, object?> Payload(GitPushVerb verb) =>
        InstanceDoor.Said(("repository", verb.Repository), ("kind", verb.Kind), ("name", verb.Name), ("at", verb.At));

    public static async Task<int> RunAsync(IReadOnlyList<string> args, GitRefWorld world, InstanceWorld instance, CancellationToken ct)
    {
        if (Read(args, out var problem) is not { } verb)
        {
            await instance.Error.WriteLineAsync($"{Prefix}: {problem}\n{Usage}").ConfigureAwait(false);
            return 2;
        }

        if (!verb.Yes) return await PlanAsync(verb, world, instance.Out, instance.Error, ct).ConfigureAwait(false);
        return await InstanceDoor.PressAsync(PersonPresses.GitPush, Payload(verb), async () =>
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
    public static async Task<int> PlanAsync(GitPushVerb verb, GitRefWorld world, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        var (update, address, why) = await GitPushActs.PlanAsync(world.Runs().Journal.Home, Ask(verb, checkout), ct).ConfigureAwait(false);
        if (why is not null)
        {
            await error.WriteLineAsync($"{Prefix}: {why}").ConfigureAwait(false);
            return 1;
        }

        var from = update!.Old == GitPushActs.Absent ? "as new" : $"from {update.Old![..12]}, where origin had it at the last fetch";
        await output.WriteLineAsync($"would push {verb.Kind} `{verb.Name}` at {update.New[..12]} to origin ({address}) {from}, under a grant for that one update; nothing was changed.").ConfigureAwait(false);
        if (verb.Kind == "tag") await output.WriteLineAsync("a tag is a Dangerous push: the place's Dangerous value decides it at the door.").ConfigureAwait(false);
        await output.WriteLineAsync($"the same by hand: {GitPushActs.Command(update.New, update.Ref)}").ConfigureAwait(false);
        await output.WriteLineAsync("--yes pushes; Daoris running asks for your yes in its window first.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>The act run and its outcome said: this process's own run, and the words the instance answers for the terminal.</summary>
    public static async Task<int> ExecuteAsync(
        GitPushVerb verb, GitRefWorld world, string press, string? confirmed, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        var run = await GitPushActs.PushAsync(world.Runs(), Ask(verb, checkout), press, confirmed, ct).ConfigureAwait(false);
        if (run.Refused)
        {
            await error.WriteLineAsync($"{Prefix}: {run.Said}").ConfigureAwait(false);
            return 1;
        }

        switch (run.Outcome)
        {
            case ActOutcomes.Done:
                await output.WriteLineAsync($"pushed {verb.Kind} `{verb.Name}` to origin (act {run.Act}).").ConfigureAwait(false);
                return 0;
            case ActOutcomes.NotDone:
                await error.WriteLineAsync($"{Prefix}: nothing was pushed (act {run.Act}). {run.Notes}").ConfigureAwait(false);
                return 1;
            default:
                await error.WriteLineAsync($"{Prefix}: Daoris could not tell whether this finished; nothing was retried (act {run.Act}). {run.Said}").ConfigureAwait(false);
                return 2;
        }
    }

    private static PushAsk Ask(GitPushVerb verb, string checkout) => new(verb.Repository, checkout, verb.Kind, verb.Name, verb.At);

    private static async Task<string?> CheckoutAsync(GitPushVerb verb, GitRefWorld world, TextWriter error, CancellationToken ct)
    {
        var root = await world.CheckoutOf(verb.Repository, ct).ConfigureAwait(false);
        if (root is not null && Directory.Exists(root)) return root;
        await error.WriteLineAsync($"{Prefix}: {verb.Repository} has no checkout on this machine; nothing was changed.").ConfigureAwait(false);
        return null;
    }
}
