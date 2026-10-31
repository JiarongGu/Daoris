namespace Daoris.Driver;

/// <summary>
/// What a terminal asked of a ref: <c>branch</c> or <c>tag</c> to create, in a repository, at a commit, <see cref="Yes"/> to make it; with
/// <see cref="Delete"/> to delete it (the commit then the one the ref is expected at).
/// </summary>
public sealed record GitRefVerb(string Kind, string Repository, string Name, string? At, bool Yes, bool Delete = false);

/// <summary>What <see cref="GitRefCommand"/> is handed: where a repository's checkout is, the runs of acts this process holds, and the repository's line.</summary>
/// <param name="CheckoutOf">The repository's checkout on this machine, or null where it has none.</param>
/// <param name="LineOf">The repository's line branch, given its name and checkout; null where unset, or the world has none to tell (a delete then refuses nothing for it).</param>
public sealed record GitRefWorld(Func<string, CancellationToken, Task<string?>> CheckoutOf, Func<ActRuns> Runs, Func<string, string, CancellationToken, Task<string?>>? LineOf = null);

/// <summary>
/// <c>daoris-driver git branch|tag &lt;repository&gt; &lt;name&gt; [&lt;commit&gt;] [--delete] [--plan|--yes]</c> (GITAPP2c and d, D204; the
/// git-writes design §9): the terminal's twin of the Branches tab's <i>New branch</i> and <i>New tag</i>, and of its <i>Delete</i>. Without
/// <c>--yes</c> (or with <c>--plan</c>) it says what it would make or delete and changes nothing. With <c>--yes</c> it is the running Daoris's
/// client (<see cref="InstanceDoor.PressAsync"/>): the person's press, confirmed in the window; with no Daoris running it makes the change
/// itself, through the same act (<see cref="GitRefActs"/>), refused from a session's terminal (<c>DAORIS_SESSION_ID</c>).
/// </summary>
/// <remarks>Exit codes: 0 done or planned · 1 refused, nothing changed · 2 could not, or the outcome is not known.</remarks>
public static class GitRefCommand
{
    public const string Usage = "usage: daoris-driver git branch|tag <repository> <name> [<commit>] [--delete] [--plan | --yes]";

    private const string Prefix = "daoris-driver git";

    /// <summary>Whether the words after <c>git</c> are this verb's.</summary>
    public static bool Answers(IReadOnlyList<string> args) => args.Count > 0 && args[0] is "branch" or "tag";

    /// <summary>Whether the words make a ref (<c>--yes</c>), so the verb holds the home's lease (<see cref="VerbLeases"/>); a plan does not.</summary>
    public static bool Changes(IReadOnlyList<string> args) => Answers(args) && args.Contains("--yes");

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static GitRefVerb? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        var words = new List<string>();
        bool? yes = null;
        var delete = false;
        foreach (var word in args.Skip(1))
        {
            if (word == "--delete")
            {
                delete = true;
            }
            else if (word is "--yes" or "--plan")
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

        if (words.Count is < 2 or > 3)
        {
            problem = delete
                ? $"`git {args[0]} --delete` takes a repository, a name and, if it must stand there, a commit."
                : $"`git {args[0]}` takes a repository, a name and, if not HEAD, a commit.";
            return null;
        }

        return new GitRefVerb(args[0], words[0], words[1], words.Count == 3 ? words[2] : null, yes == true, delete);
    }

    /// <summary>The verb's payload: the route's own fields beside <c>said: true</c> (RPC1e).</summary>
    public static Dictionary<string, object?> Payload(GitRefVerb verb) =>
        InstanceDoor.Said(("repository", verb.Repository), ("kind", verb.Kind), ("name", verb.Name), ("at", verb.At));

    /// <summary>The press the verb is: a ref made, or taken away.</summary>
    public static InstanceRoute RouteOf(GitRefVerb verb) => verb.Delete ? PersonPresses.GitRefDelete : PersonPresses.GitRefCreate;

    public static async Task<int> RunAsync(IReadOnlyList<string> args, GitRefWorld world, InstanceWorld instance, CancellationToken ct)
    {
        if (Read(args, out var problem) is not { } verb)
        {
            await instance.Error.WriteLineAsync($"{Prefix}: {problem}\n{Usage}").ConfigureAwait(false);
            return 2;
        }

        if (!verb.Yes) return await PlanAsync(verb, world, instance.Out, instance.Error, ct).ConfigureAwait(false);
        return await InstanceDoor.PressAsync(RouteOf(verb), Payload(verb), async () =>
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
    public static async Task<int> PlanAsync(GitRefVerb verb, GitRefWorld world, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        if (verb.Delete)
        {
            var drop = await DeleteAskAsync(verb, world, checkout, ct).ConfigureAwait(false);
            var (found, why) = await GitRefActs.PlanDeleteAsync(world.Runs().Journal.Home, drop, ct).ConfigureAwait(false);
            if (why is not null)
            {
                await error.WriteLineAsync($"{Prefix} {verb.Kind}: {why}.").ConfigureAwait(false);
                return 1;
            }

            await output.WriteLineAsync($"would delete the {verb.Kind} `{verb.Name}` in {verb.Repository} at {found![..12]}, keeping its commits on a recovery ref; nothing was changed.").ConfigureAwait(false);
            await output.WriteLineAsync($"the same by hand: {GitRefActs.Commands(drop)[0]}").ConfigureAwait(false);
            await output.WriteLineAsync("--yes deletes it; Daoris running asks for your yes in its window first.").ConfigureAwait(false);
            return 0;
        }

        var ask = new RefAsk(verb.Repository, checkout, verb.Kind, verb.Name, verb.At);
        var (id, refusal) = await GitRefActs.PlanAsync(ask, ct).ConfigureAwait(false);
        if (refusal is not null)
        {
            await error.WriteLineAsync($"{Prefix} {verb.Kind}: {refusal}.").ConfigureAwait(false);
            return 1;
        }

        await output.WriteLineAsync($"would create the {verb.Kind} `{verb.Name}` in {verb.Repository} at {id![..12]}; nothing was changed.").ConfigureAwait(false);
        await output.WriteLineAsync($"the same by hand: {GitRefActs.Commands(ask)[0]}").ConfigureAwait(false);
        await output.WriteLineAsync("--yes makes it; Daoris running asks for your yes in its window first.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>The act run and its outcome said: this process's own run, and the words the instance answers for the terminal.</summary>
    public static async Task<int> ExecuteAsync(
        GitRefVerb verb, GitRefWorld world, string press, string? confirmed, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (await CheckoutAsync(verb, world, error, ct).ConfigureAwait(false) is not { } checkout) return 2;
        var run = verb.Delete
            ? await GitRefActs.DeleteAsync(world.Runs(), await DeleteAskAsync(verb, world, checkout, ct).ConfigureAwait(false), press, confirmed, ct).ConfigureAwait(false)
            : await GitRefActs.CreateAsync(world.Runs(), new RefAsk(verb.Repository, checkout, verb.Kind, verb.Name, verb.At), press, confirmed, ct).ConfigureAwait(false);
        return await SayAsync(run, verb, output, error).ConfigureAwait(false);
    }

    private static async Task<RefDeleteAsk> DeleteAskAsync(GitRefVerb verb, GitRefWorld world, string checkout, CancellationToken ct) =>
        new(verb.Repository, checkout, verb.Kind, verb.Name, verb.At, world.LineOf is { } line ? await line(verb.Repository, checkout, ct).ConfigureAwait(false) : null);

    private static async Task<int> SayAsync(ActRun run, GitRefVerb verb, TextWriter output, TextWriter error)
    {
        if (run.Refused)
        {
            await error.WriteLineAsync($"{Prefix} {verb.Kind}: {run.Said}").ConfigureAwait(false);
            return 1;
        }

        switch (run.Outcome)
        {
            case ActOutcomes.Done:
                await output.WriteLineAsync($"{(verb.Delete ? "deleted" : "created")} the {verb.Kind} `{verb.Name}` in {verb.Repository} (act {run.Act}).").ConfigureAwait(false);
                return 0;
            case ActOutcomes.NotDone:
                await error.WriteLineAsync($"{Prefix} {verb.Kind}: git did not {(verb.Delete ? "delete it" : "make it")}; nothing was changed (act {run.Act}). {run.Said}").ConfigureAwait(false);
                return 1;
            default:
                await error.WriteLineAsync(
                    $"{Prefix} {verb.Kind}: Daoris could not tell whether this finished; nothing was retried (act {run.Act}). {run.Said}").ConfigureAwait(false);
                return 2;
        }
    }

    private static async Task<string?> CheckoutAsync(GitRefVerb verb, GitRefWorld world, TextWriter error, CancellationToken ct)
    {
        var root = await world.CheckoutOf(verb.Repository, ct).ConfigureAwait(false);
        if (root is not null && Directory.Exists(root)) return root;
        await error.WriteLineAsync($"{Prefix} {verb.Kind}: {verb.Repository} has no checkout on this machine; nothing was changed.").ConfigureAwait(false);
        return null;
    }
}
