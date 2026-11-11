namespace Daoris.Driver;

/// <summary>What <c>repository create</c>'s words ask, and whether they only plan it.</summary>
public sealed record CreateWords(CreateAsk Ask, bool Plan);

/// <summary>
/// <c>daoris repository create &lt;name&gt; [--kind git|folder] [--workspace &lt;name&gt;] [--under &lt;folder&gt;] [--line &lt;branch&gt;] --purpose "…" [--work "…"]
/// [--plan]</c>: <c>REGISTRY_CREATE</c>'s terminal twin, the same plan and the same apply from the same code in English. <c>--plan</c> reads and
/// writes nothing. Without it the verb is the person's press: through a running Daoris it waits for their Confirm on the window's card, once,
/// and runs only then; alone it is the person's own terminal acting as the window's press would.
/// </summary>
/// <remarks>Exit codes keep the family contract: 0 a plan nothing refuses, or an apply that is done · 1 refused, or an apply not done · 2 the usage.</remarks>
public static class RepositoryCreateCommand
{
    public const string Usage =
        "usage: daoris repository create <name> [--kind git|folder] [--workspace <name>] [--under <folder>] [--line <branch>] --purpose \"…\" [--work \"…\"] [--plan]";

    /// <summary>The words that ask a creation, or what is wrong with them; a relative <c>--under</c> is read against <paramref name="folder"/>.</summary>
    public static (CreateWords? Words, string? Problem) Parse(IReadOnlyList<string> args, string folder)
    {
        string? name = null, kind = null, workspace = null, under = null, line = null, purpose = null, work = null;
        var plan = false;
        for (var at = 0; at < args.Count; at++)
        {
            var arg = args[at];
            string? Value() => at + 1 < args.Count ? args[++at] : null;
            switch (arg)
            {
                case "--plan":
                    plan = true;
                    break;
                case "--kind" or "--workspace" or "--under" or "--line" or "--purpose" or "--work":
                    if (Value() is not { } value) return (null, $"`{arg}` takes a value.");
                    switch (arg)
                    {
                        case "--kind": kind = value; break;
                        case "--workspace": workspace = value; break;
                        case "--under": under = Path.GetFullPath(value, folder); break;
                        case "--line": line = value; break;
                        case "--purpose": purpose = value; break;
                        default: work = value; break;
                    }

                    break;
                default:
                    if (arg.StartsWith('-')) return (null, $"`{arg}` is not a word repository create takes.");
                    if (name is not null) return (null, $"it makes one department at a time: `{name}` and `{arg}` are two names.");
                    name = arg;
                    break;
            }
        }

        if (name is null) return (null, "name the department to make.");
        if (purpose is null) return (null, "say why it is opened with `--purpose \"…\"`.");
        return (new CreateWords(new CreateAsk(name, purpose) { Kind = kind ?? PlaceKind.Git, Workspace = workspace, Under = under, Line = line, Work = work }, plan), null);
    }

    /// <summary>The terminal's plan: the lines said, and its exit code (0 where nothing refuses).</summary>
    public static async Task<int> PlanAsync(CreateAsk ask, CreateDoors doors, TextWriter output, CancellationToken ct = default)
    {
        var plan = await RepositoryCreatePlans.PlanAsync(doors, ask, ct).ConfigureAwait(false);
        foreach (var line in Lines(plan)) output.WriteLine(line);
        return plan.Applies ? 0 : 1;
    }

    /// <summary>
    /// The apply: the plan printed as <c>--plan</c> prints it, then the apply of that plan by its digest (<paramref name="digest"/> where the
    /// person's card pinned one), then the outcome in a sentence and what is made. Exit 0 where every operation is done.
    /// </summary>
    public static async Task<int> ApplyAsync(CreateAsk ask, string? digest, CreateDoors doors, TextWriter output, CancellationToken ct = default)
    {
        var plan = await RepositoryCreatePlans.PlanAsync(doors, ask, ct).ConfigureAwait(false);
        foreach (var line in Lines(plan, applying: true)) output.WriteLine(line);
        var result = await RepositoryCreates.ApplyAsync(doors, ask, digest ?? plan.Digest, ct).ConfigureAwait(false);
        output.WriteLine($"repository create: {result.Message}");
        if (result.Commit is { } commit) output.WriteLine($"  commit {TextBounds.ShortCommit(commit, 12)} in {result.Folder}");
        foreach (var operation in result.Operations.Where(each => each.Quest is { Length: > 0 })) output.WriteLine($"  quest  #{operation.Quest} for `{operation.Repository}`");
        return result.Done ? 0 : 1;
    }

    /// <summary>
    /// The press, as the running Daoris's client: its words handed to the route, which asks the person to confirm on the window's card and
    /// runs only then; where none answers, the verb alone, which a session's terminal never is.
    /// </summary>
    public static Task<int> PressAsync(CreateAsk ask, CreateDoors? doors, InstanceWorld? instance, TextWriter output, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(
            instance, PersonPresses.RegistryCreate,
            InstanceDoor.Said(
                ("name", ask.Name), ("purpose", ask.Purpose), ("kind", ask.Kind), ("workspace", ask.Workspace), ("under", ask.Under), ("line", ask.Line), ("work", ask.Work)),
            () => ApplyAloneAsync(ask, doors, instance, output, ct), output, ct);

    private static async Task<int> ApplyAloneAsync(CreateAsk ask, CreateDoors? doors, InstanceWorld? instance, TextWriter output, CancellationToken ct)
    {
        // A session's terminal never applies alone; the running Daoris's window confirms the same press.
        if (instance?.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            output.WriteLine($"repository create: {DriverChoicesCommand.SessionsTerminal}");
            return 1;
        }

        if (doors is null)
        {
            output.WriteLine("repository create: applying needs the driver's home and its service, which this run was not given.");
            return 2;
        }

        return await ApplyAsync(ask, null, doors, output, ct).ConfigureAwait(false);
    }

    /// <summary>A plan in the terminal's words: what is asked, where it would be made, each step in its order, then every refusal.</summary>
    public static IReadOnlyList<string> Lines(CreatePlanned plan, bool applying = false)
    {
        var ask = plan.Asked;
        var lines = new List<string>
        {
            $"repository create: `{ask.Name}` in `{plan.Workspace}` — {PlaceKind.Said(ask.Kind)}, for: {ask.Purpose}",
        };
        if (plan.Folder.Length > 0) lines.Add($"  folder  {plan.Folder}");
        if (ask.Kind == PlaceKind.Git)
        {
            lines.Add($"  line    {plan.Line ?? RepositoryCreatePlans.DefaultLine}");
            lines.Add($"  author  {(plan.Author is { } author ? $"{author.Name} <{author.Email}>" : "(none chosen)")}");
            lines.Add("  no remote is made and nothing is pushed");
        }

        if (ask.Work is { } work) lines.Add($"  work    kept on the receipt: {work}");
        if (plan.Resumes is { } resumes) lines.Add($"  resumes the receipt `{resumes[..12]}`: its folder holds what it made");
        lines.Add("steps, in order:");
        lines.AddRange(plan.Steps.Select((step, at) => $"  {at + 1}. {step.Said}"));
        if (plan.Refusals.Count > 0)
        {
            lines.Add("refused:");
            lines.AddRange(plan.Refusals.Select(refusal => $"  - {refusal.Sentence}"));
        }

        lines.Add($"plan {plan.Digest[..12]}");
        if (!applying) lines.Add("--plan: nothing was written, and nothing was asked.");
        return lines;
    }
}
