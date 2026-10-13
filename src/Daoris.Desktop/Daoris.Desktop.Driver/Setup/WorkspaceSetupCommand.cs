using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver setup --workspace &lt;name&gt; …</c> (WSSETUP6, D124 §4.5): the terminal's door to a workspace plan, beside
/// LAYOUT7's <c>setup &lt;repository&gt;</c> on the same binary, since the plan's press reads each line with git as that press
/// does. In the library rather than the host, so its words are held by a test, as the single press's are.
/// </summary>
/// <remarks>
/// <para><c>--plan</c> prints the list: each repository with a checkout here, in the order the plan would work them, with what
/// touched each (§4.2), where it stands, and every refusal the press would meet now; then the pacing, the agent and the rule a
/// press adds. It writes, publishes and adds nothing. With a plan already working, it prints where that plan stands.</para>
/// <para>A press writes the plan and adds the doctrine tool's verbs to the workspace's rules, once (§4.3). It publishes
/// nothing: the driver's loop does, one at a time. <c>--pause</c>, <c>--resume</c> and <c>--stop</c> steer a plan already
/// made, and print where it stands.</para>
/// <para>Exit codes keep the family contract: 0 written, steered, or a plan that would write · 1 refused · 2 a tool error,
/// the usage among them.</para>
/// </remarks>
public static class WorkspaceSetupCommand
{
    public const string Usage =
        "usage: daoris-driver setup --workspace <name> [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]\n"
        + "       daoris-driver setup --workspace <name> --pause | --resume | --stop";

    /// <summary>The words read: the workspace, the ask, and the person's choices.</summary>
    private sealed record Words(string Workspace, string? Mode, WorkspaceSetupOptions Options);

    /// <summary>Whether the words ask for a workspace plan rather than one repository's set-up.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args.Contains("--workspace");

    /// <summary>What is wrong with the words, or null.</summary>
    public static string? Problem(IReadOnlyList<string> args) => Read(args, out _);

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, TextWriter output, IWorkspaceSetupWorld world, DriverConfig config, SessionWire door, string home,
        DateOnly day, DateTimeOffset now, CancellationToken ct = default)
    {
        if (Read(args, out var words) is { } problem)
        {
            output.WriteLine($"setup: {problem}");
            output.WriteLine(Usage);
            return 2;
        }

        var workspace = words!.Workspace;
        switch (words.Mode)
        {
            case "--pause":
                return await SteeredAsync(WorkspaceSetup.Pause(world, home, workspace, now)).ConfigureAwait(false);
            case "--resume":
                return await SteeredAsync(WorkspaceSetup.Resume(world, home, workspace)).ConfigureAwait(false);
            case "--stop":
                return await SteeredAsync(WorkspaceSetup.Stop(world, home, workspace, now), stopped: true).ConfigureAwait(false);
        }

        var plan = words.Mode == "--plan";
        if (plan && WorkspaceSetupFile.Load(home, workspace).Plan is { Live: true } working)
        {
            await StandingAsync(working).ConfigureAwait(false);
            output.WriteLine("--plan: a plan is working; `--pause`, `--resume` or `--stop` steers it, and nothing was changed.");
            return 0;
        }

        var preview = await WorkspaceSetup.PreviewAsync(world, config, door, home, workspace, words.Options, day, ct).ConfigureAwait(false);
        Describe(preview, output);
        if (!preview.Pressable)
        {
            output.WriteLine(preview.Refusals.Count == 0 ? "setup: there is no repository to set up." : "refused:");
            foreach (var refusal in preview.Refusals) output.WriteLine($"  - {refusal.Sentence}");
            output.WriteLine(plan ? "--plan: nothing was written, published or added." : "setup: nothing was written, and no rule was added.");
            return 1;
        }

        if (plan)
        {
            output.WriteLine($"a press adds to workspace `{preview.Workspace}`'s rules, once, so each set-up's session may run the doctrine tool:");
            foreach (var rule in SetupPress.Rules) output.WriteLine($"  {rule}");
            output.WriteLine("--plan: nothing was written, published or added.");
            return 0;
        }

        var outcome = await WorkspaceSetup.PressAsync(preview, world, home, now, ct).ConfigureAwait(false);
        output.WriteLine($"setup: {outcome.Message}");
        if (!outcome.Written) return 2;

        if (outcome.Added.Count == 0)
        {
            output.WriteLine($"workspace `{preview.Workspace}`'s rules already let each set-up's session run the doctrine tool.");
        }
        else
        {
            output.WriteLine($"added to workspace `{preview.Workspace}`'s rules, so each set-up's session may run the doctrine tool:");
            foreach (var rule in outcome.Added) output.WriteLine($"  {rule}");
            output.WriteLine($"  taken back in Repositories → the workspace's page → Setup → Remote and reach, or `daoris agent rules remove <rule> --workspace {WorkspaceSetup.Spelled(preview.Workspace)}`.");
        }

        return 0;

        async Task<int> SteeredAsync(WorkspaceSetupSteer steer, bool stopped = false)
        {
            output.WriteLine($"setup: {steer.Message}");
            if (steer.Plan is not { } steered) return steer.Ok ? 0 : 1;

            if (stopped)
            {
                var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
                foreach (var repository in steered.Order.Where(steered.Published.ContainsKey))
                {
                    var id = steered.Published[repository];
                    if (quests.FirstOrDefault(quest => string.Equals(quest.Id, id, StringComparison.OrdinalIgnoreCase)) is { Status: "Open", Deletable: true })
                    {
                        output.WriteLine($"  #{id} to `{repository}` is open and nobody has started it: `daoris-driver quest delete {id}` deletes it.");
                    }
                }
            }

            await StandingAsync(steered).ConfigureAwait(false);
            return steer.Ok ? 0 : 1;
        }

        async Task StandingAsync(WorkspaceSetupPlan standing)
        {
            var standings = await WorkspaceSetup.StandAsync(world, config, standing, ct).ConfigureAwait(false);
            output.WriteLine($"setup: workspace `{standing.Workspace}`, a plan made {Day(standing.Created)}: {AtOnceWords(standing.AtOnce)}, "
                + (standing.Pilot > 0 ? $"a pilot of {standing.Pilot}" : "with no pilot"));
            var width = standings.Select(each => each.Repository.Length).DefaultIfEmpty(0).Max();
            var number = 0;
            foreach (var each in standings)
            {
                output.WriteLine($"{++number,3}  {each.Repository.PadRight(width)}  {WorkspaceSetup.Word(each.State)}"
                    + (each.State == SetupState.ToGo ? "" : $" — {each.Said}"));
            }

            output.WriteLine(WorkspaceSetup.Summary(standing, standings));
        }
    }

    /// <summary>The list a press works from: each repository in order, what touched it, where it stands, and its refusals now.</summary>
    private static void Describe(WorkspaceSetupPreview preview, TextWriter output)
    {
        if (preview.Rows.Count == 0) return;

        output.WriteLine($"setup: workspace `{preview.Workspace}`, {preview.Rows.Count} repositories with a checkout here, the ones other work touches first");
        var width = Math.Max("repository".Length, preview.Rows.Max(row => row.Repository.Length));
        output.WriteLine($"{"",3}  {"repository".PadRight(width)} {"asked",5}  {"read",4}  {"sessions",8}  now");
        var number = 0;
        foreach (var row in preview.Rows)
        {
            output.WriteLine($"{++number,3}  {row.Repository.PadRight(width)} {row.Touches.Asked,5}  {row.Touches.Read,4}  {row.Touches.Sessions,8}  "
                + WorkspaceSetup.Word(row.Standing.State));
            foreach (var refusal in row.Refusals) output.WriteLine($"       refused: {refusal.Sentence}");
        }

        if (preview.Unticked.Count > 0)
        {
            output.WriteLine($"  left out  {string.Join(", ", preview.Unticked)} (you unticked {(preview.Unticked.Count == 1 ? "it" : "them")})");
        }

        if (preview.NoCheckout.Count > 0)
        {
            output.WriteLine($"  no checkout here: {string.Join(", ", preview.NoCheckout)}, which each one's own machine's driver sets up");
        }

        output.WriteLine($"  at once   {preview.AtOnce} (at most {WorkspaceSetup.MostAtOnce(preview.Cap)} while the cap is {preview.Cap}, so other work keeps a slot)");
        output.WriteLine(preview.Pilot > 0
            ? $"  pilot     {preview.Pilot}: once the first {preview.Pilot} have closed, the plan pauses until you resume it"
            : "  pilot     none: the plan never pauses on its own");
        if (preview.Adapter is { } adapter) output.WriteLine($"  agent     {adapter}");
    }

    /// <summary>The words, read; null when they are well formed, else what is wrong with them.</summary>
    private static string? Read(IReadOnlyList<string> args, out Words? words)
    {
        words = null;
        string? workspace = null, mode = null;
        int? atOnce = null, pilot = null;
        var first = new List<string>();
        var skip = new List<string>();
        List<string>? naming = null;

        for (var at = 0; at < args.Count; at++)
        {
            var word = args[at];
            switch (word)
            {
                case "--workspace":
                    if (workspace is not null) return "`--workspace` names one workspace.";
                    if (Next(at) is not { } named) return "`--workspace` takes a workspace's name.";
                    workspace = named.Trim();
                    at++;
                    naming = null;
                    break;

                case "--plan" or "--pause" or "--resume" or "--stop":
                    if (mode is not null) return $"`{mode}` and `{word}` are two asks: name one.";
                    mode = word;
                    naming = null;
                    break;

                case "--at-once" or "--pilot":
                    if (Next(at) is not { } given || !int.TryParse(given, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                        || (word == "--at-once" && count < 1))
                    {
                        return word == "--at-once" ? "`--at-once` takes a number, one or more." : "`--pilot` takes a number, zero for none.";
                    }

                    if (word == "--at-once") atOnce = count;
                    else pilot = count;
                    at++;
                    naming = null;
                    break;

                case "--first" or "--skip":
                    if (Next(at) is null) return $"`{word}` takes one repository or more.";
                    naming = word == "--first" ? first : skip;
                    break;

                default:
                    if (word.StartsWith('-')) return $"`{word}` is not a word `setup --workspace` takes.";
                    if (naming is null)
                    {
                        return $"`{word}`: a workspace plan names repositories only after `--first` or `--skip`; "
                            + "`daoris-driver setup <repository>` sets one up alone.";
                    }

                    naming.Add(word.Trim());
                    break;
            }
        }

        if (workspace is null) return "name the workspace: `--workspace <name>`.";
        if (mode is "--pause" or "--resume" or "--stop" && (atOnce is not null || pilot is not null || first.Count > 0 || skip.Count > 0))
        {
            return $"`{mode}` steers a plan already made, and takes no other choice.";
        }

        words = new Words(workspace, mode, new WorkspaceSetupOptions(atOnce, pilot, first, skip));
        return null;

        string? Next(int at) => at + 1 < args.Count && !args[at + 1].StartsWith('-') && args[at + 1].Trim().Length > 0 ? args[at + 1] : null;
    }

    private static string AtOnceWords(int atOnce) => atOnce == 1 ? "one at a time" : $"{atOnce} at a time";

    private static string Day(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
