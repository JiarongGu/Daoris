namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver setup &lt;repository&gt; [--plan]</c> (LAYOUT7, D117 §6.1): the terminal's door to the set-up press,
/// on the headless binary because it already owns git, which the facts are read with, and already publishes asks
/// (<c>ask --to</c>). The CLI has no quest command (D32). In the library rather than the host, so its words are held
/// by a test, as the plugin kit's are.
/// </summary>
/// <remarks>
/// <para><c>--plan</c> prints what was read, every refusal, the rule the press would add, how the work would land, the
/// agent that would carry it and the quest's whole text, and publishes nothing and writes nothing.</para>
/// <para>Exit codes keep the family contract: 0 published, or a plan that would publish · 1 refused, by the press or by
/// the service · 2 a tool error, the usage among them.</para>
/// </remarks>
public static class SetupCommand
{
    public const string Usage = "usage: daoris-driver setup <repository> [--plan]";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, TextWriter output, ISetupWorld world, DriverConfig config, SessionWire door, string home,
        DateOnly day, CancellationToken ct = default)
    {
        if (Problem(args) is { } problem)
        {
            output.WriteLine($"setup: {problem}");
            output.WriteLine(Usage);
            return 2;
        }

        var plan = args.Contains("--plan");
        var setup = await SetupPress.PlanAsync(world, config, door, args.Single(arg => arg != "--plan"), day, ct).ConfigureAwait(false);
        Describe(setup, output);

        if (!setup.Pressable)
        {
            output.WriteLine(setup.Refusals.Count == 0 ? "setup: there is nothing to ask." : "refused:");
            foreach (var refusal in setup.Refusals) output.WriteLine($"  - {refusal.Sentence}");
            if (plan) output.WriteLine("--plan: nothing was published, and no rule was added.");
            return 1;
        }

        if (plan)
        {
            output.WriteLine($"a press publishes this to `{setup.Repository}`, as your ask, in `{setup.Workspace}`:");
            output.WriteLine();
            output.WriteLine(setup.Sentence);
            output.WriteLine($"and adds to `{setup.Repository}`'s rules, so its session may run the doctrine tool:");
            foreach (var rule in SetupPress.Rules) output.WriteLine($"  {rule}");
            output.WriteLine("--plan: nothing was published, and no rule was added.");
            return 0;
        }

        var outcome = await SetupPress.ApplyAsync(setup, world, home, ct).ConfigureAwait(false);
        output.WriteLine(outcome.Message);
        if (outcome.AskId is not null) output.WriteLine($"  ask    #{outcome.AskId}");
        if (outcome.QuestId is not null) output.WriteLine($"  quest  #{outcome.QuestId}");
        if (!outcome.Published)
        {
            output.WriteLine("setup: nothing was published, and the rules this press added were taken back.");
            return 1;
        }

        if (outcome.Added.Count > 0)
        {
            output.WriteLine($"added to `{setup.Repository}`'s rules, so its session may run the doctrine tool:");
            foreach (var rule in outcome.Added) output.WriteLine($"  {rule}");
            // HELPSETUP1: a repository's rules are on its Setup → Reach since UX6f (D150 §3.1).
            output.WriteLine("  taken back in Repositories → the repository's page → Setup → Reach, or "
                + $"`daoris agent rules remove <rule> --repository {setup.Repository}`.");
        }

        return 0;
    }

    /// <summary>What is wrong with the words, or null for one repository and, perhaps, <c>--plan</c>.</summary>
    public static string? Problem(IReadOnlyList<string> args)
    {
        var named = args.Where(arg => arg != "--plan").ToList();
        if (named.Count > 1) return "one repository at a time: each one's session, branch and review is its own.";
        if (named.Count == 0) return "name the repository to set up.";
        return named[0].StartsWith('-') ? $"`{named[0]}` is not a word setup takes." : null;
    }

    /// <summary>What the press found, before what it would do: the line, the layout, the agent, the landing, the tools.</summary>
    private static void Describe(SetupPlan setup, TextWriter output)
    {
        output.WriteLine($"setup: `{setup.Repository}`" + (setup.Workspace is { } workspace ? $", in workspace `{workspace}`" : ""));
        if (setup.Facts is { } facts)
        {
            output.WriteLine($"  line     `{facts.Line}` at `{(facts.Commit.Length > 12 ? facts.Commit[..12] : facts.Commit)}`");
            output.WriteLine($"  layout   {facts.Layout} · adopted {(facts.Adopted ? "yes" : "no")} · declares {(facts.Declares ? "yes" : "no")}"
                + (setup.Case is { } kind and not SetupCase.Done ? $" · {Asks(kind)}" : ""));
        }

        if (setup.Adapter is { } adapter) output.WriteLine($"  agent    {adapter}" + (adapter.StartsWith("codex", StringComparison.Ordinal)
            ? " (whether its sandbox runs a program outside the workspace is not measured)"
            : ""));
        if (setup.Landing is { } landing) output.WriteLine($"  lands    {SetupPress.LandsAs(landing)}");
        if (setup.Tools is { } tools)
        {
            output.WriteLine($"  tools    node {tools.NodeVersion?.Trim() ?? "not found"} · daoris {(SetupPress.IsVersion(tools.DaorisVersion) ? tools.DaorisVersion!.Trim() : "not found")}");
        }
    }

    private static string Asks(SetupCase kind) => kind switch
    {
        SetupCase.Whole => "the whole set-up",
        SetupCase.Move => "the move to the agents layout",
        _ => "the knowledge alone",
    };
}
