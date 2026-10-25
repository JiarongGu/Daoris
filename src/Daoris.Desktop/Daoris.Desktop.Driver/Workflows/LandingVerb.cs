using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver landing</c> (WSR1, D87, D100, LAND2a, PROCESS1b, FABRIC-GRAPH1c; FOUNDATION1 mod-iii): the settings' port
/// (<see cref="ChoiceVerb"/>) implemented where the rule's plugin and the cycle after it are both known, the workflows': it sets the
/// workplace's landing rule (<see cref="LandingRules"/>) with the plugin that lands by it and the graph that follows, and says what the
/// file keeps that no unit reads.
/// </summary>
public sealed class LandingVerb : ChoiceVerb
{
    public override string Word => "landing";

    /// <summary>
    /// <c>landing</c> (WSR1, D87, D100, LAND2a, PROCESS1b): a rule replaced whole, or cleared; its plugin asked by the operation
    /// (<see cref="LandingRules.Put"/>). A cycle the rule holds is kept and said, judged under the rule as it now stands;
    /// <c>--cycle &lt;file&gt;</c> sets one, judged after this rule's landing as <see cref="DriverConfig.WithLandingCycle"/> judges it, and
    /// <c>--no-cycle</c> takes it (FABRIC-GRAPH1c): no route takes either, so the rule and its cycle are written alone (PUSH1f2b).
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var workspace = FlagValue(args, "--workspace");
        var clear = args.Contains("--clear");
        var words = Operands(args, ["--workspace", "--plugin", "--key", "--cycle"]).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var form = words.ElementAtOrDefault(0);
        var pattern = words.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(name) || (!clear && string.IsNullOrEmpty(form)))
        {
            throw new ChoiceWords("`driver landing` needs <repository>|--workspace <name>, then merge|branch <pattern>|--clear — "
                + "e.g. `daoris driver landing --workspace aurora branch \"feature/{quest}-{slug}\"`, and on a branch "
                + "`--plugin <id>` for an installed plugin that pushes it and opens the pull request, `--auto-accept` "
                + "for a quest's done to land it with no press, `--key <shape>` for the shape `{ticket}` is found by, and "
                + "`--squash` for the pull request's branch as one commit.");
        }

        var keyAt = IndexOf(args, "--key");
        var key = keyAt == -1 ? null : args.ElementAtOrDefault(keyAt + 1);
        if (keyAt != -1 && (key is null || key.StartsWith("--", StringComparison.Ordinal)))
        {
            throw new ChoiceWords($"`--key` needs the shape of a ticket's key — `--key <shape>`, e.g. `--key \"{LandingRules.DefaultKey}\"`.");
        }

        if (key is not null && args.Contains("--no-key"))
        {
            throw new ChoiceWords("a rule names its ticket's key or reads the default — say `--key <shape>` or `--no-key`, not both.");
        }

        var pluginAt = IndexOf(args, "--plugin");
        var plugin = pluginAt == -1 ? null : args.ElementAtOrDefault(pluginAt + 1);
        if (pluginAt != -1 && (string.IsNullOrEmpty(plugin) || plugin.StartsWith("--", StringComparison.Ordinal)))
        {
            throw new ChoiceWords("`--plugin` needs the id of an installed plugin — `--plugin <id>`; `daoris plugin list` shows what there is.");
        }

        var autoAccept = args.Contains("--auto-accept");
        if (autoAccept && args.Contains("--no-auto-accept"))
        {
            throw new ChoiceWords("a rule accepts automatically or waits for your Accept — say `--auto-accept` or `--no-auto-accept`, not both.");
        }

        var squash = args.Contains("--squash");
        if (squash && args.Contains("--no-squash"))
        {
            throw new ChoiceWords("a rule squashes the work to one commit or keeps the session's commits — say `--squash` or `--no-squash`, not both.");
        }

        var rule = clear ? null : new LandingRule(form!, pattern, args.Contains("--tidy"), plugin, autoAccept, key, squash);
        if (rule is not null && LandingRules.Problem(rule) is { } problem) throw new ChoiceWords(problem);

        // The work's cycle after the landing (FABRIC-GRAPH1c): set, taken, or with neither flag kept as the rule holds it.
        var cycleAt = IndexOf(args, "--cycle");
        var cycleFile = cycleAt == -1 ? null : args.ElementAtOrDefault(cycleAt + 1);
        if (cycleAt != -1 && (cycleFile is null || cycleFile.StartsWith("--", StringComparison.Ordinal)))
        {
            throw new ChoiceWords("`--cycle` needs the file holding the cycle's JSON — `--cycle <file>`, a graph fragment after the landing.");
        }

        var noCycle = args.Contains("--no-cycle");
        if (cycleFile is not null && noCycle) throw new ChoiceWords("a rule sets its cycle or takes it away — say `--cycle <file>` or `--no-cycle`, not both.");
        if (rule is null && (cycleFile is not null || noCycle))
        {
            throw new ChoiceWords("`--clear` takes the rule and its cycle with it — a cycle rides its rule (D164). Nothing was written.");
        }

        string? cycle = null;
        if (cycleFile is not null)
        {
            cycle = ChoiceCycles.File(world, cycleFile);
            var source = workspace is not null ? LandingSource.Workspace : LandingSource.Repository;
            if (NodeGraphHomes.OfLanding(rule! with { Cycle = cycle }, source).Problems.FirstOrDefault() is { } first)
            {
                throw new ChoiceWords($"the cycle in `{cycleFile}` does not hold: {first.Words} Nothing was written.");
            }
        }

        var held = workspace is not null ? AtName(before.WorkspaceLandings, workspace) : AtName(before.Landings, name);
        var payload = Scoped(workspace, name);
        if (rule is not null)
        {
            // The rule is sent whole, its key and its squash named, as this verb replaces it whole: a route that kept the standing
            // rule's key or squash would leave what the terminal did not say.
            payload["form"] = rule.Form;
            if (rule.Pattern is not null) payload["pattern"] = rule.Pattern;
            payload["tidy"] = rule.Tidy;
            if (rule.Plugin is not null) payload["plugin"] = rule.Plugin;
            payload["autoAccept"] = rule.AutoAccept;
            payload["key"] = rule.Key;
            payload["squash"] = rule.Squash;
        }

        var home = DriverConfig.HomeOf(path);
        // SET_LANDING has no field for the cycle and keeps the one the rule holds, as this verb does with neither flag; a cycle set or taken
        // is this verb's alone (D171's PUSH1f note).
        var cycled = cycleFile is not null || noCycle;
        return new("landing", cycled ? null : ChoiceRoutes.Landing, cycled ? null : payload, _ =>
        {
            StandingChoices.Write(path, config =>
            {
                var landed = LandingRules.Put(config, workspace is null ? name : null, workspace, rule, PluginCatalog.Load(home, AdapterSet.Built().Names));
                if (!cycled) return landed;
                return workspace is not null ? landed.WithWorkspaceLandingCycle(workspace, cycle) : landed.WithLandingCycle(name, cycle);
            });
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var whose = workspace is not null ? $"repositories in the workspace `{workspace}`" : $"`{name}`";
            if (rule is null)
            {
                Say($"daoris: work in {whose} lands as {(workspace is not null ? "each one's own rule, else " : "its workspace's rule, else ")}the merge into its line.");
            }
            else if (rule.Form == LandingForm.Merge)
            {
                Say($"daoris: work in {whose} is merged into the line when you accept it, in the repository's own checkout.");
            }
            else if (rule.Plugin is not null)
            {
                Say($"daoris: work in {whose} is put on a branch named `{rule.Pattern}` when you accept it, and then");
                Say($"  plugin `{rule.Plugin}` pushes it and opens the pull request, over its wire. Daoris itself never pushes");
                Say("  (D87, D100): the plugin's own process does, signed in as its platform's tools are. A plugin that fails");
                Say("  leaves the branch standing, and the review says how to push it yourself.");
            }
            else
            {
                Say($"daoris: work in {whose} is put on a branch named `{rule.Pattern}` when you accept it —");
                Say("  from the session's branch, with nothing merged and no checkout touched. You push it and open");
                Say("  the pull request: Daoris never pushes (D87).");
            }

            // What names the branch's ticket, said where the pattern asks for one (PROCESS1b-i).
            if (rule is { Form: LandingForm.Branch } && (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal)) Say($"  {LandingRules.TicketSays(rule.Key)}");

            if (rule is { Tidy: true })
            {
                Say("  Once a press lands the work, its tree and its branch go — only where git proves the work is on a");
                Say("  branch of yours. Without --tidy the tree stays until you discard it.");
            }

            // The person's standing say-so for a push with no press, said as it is given (D145 point 5, design §6).
            if (rule is { AutoAccept: true }) Say($"  Accept automatically: {LandingRules.AutoAcceptSays(rule.Plugin)}");
            else if (rule is { Form: LandingForm.Branch } && args.Contains("--no-auto-accept"))
            {
                Say("  Done work there waits for your Accept — a repository's own rule replaces its workspace's whole, the switch with it.");
            }

            // The first commit Daoris itself makes (PROCESS1b-ii), said with the identity it is made under.
            if (rule is { Squash: true })
            {
                foreach (var line in LandingRules.SquashSays) Say($"  {line}");
            }
            else if (rule is { Form: LandingForm.Branch } && args.Contains("--no-squash"))
            {
                Say("  The branch holds the session's own commits — a repository's own rule replaces its workspace's whole, the squash with it.");
            }

            // The cycle: set, taken, or kept and judged under the rule as it now stands, since a form that changed under a cycle keeps it
            // and the driver says one that does not hold (FABRIC-GRAPH1's choice 3).
            if (cycle is not null)
            {
                Say($"  After the landing, its cycle: {ChoiceCycles.Said(cycle)}.");
            }
            else if (noCycle)
            {
                Say("  Its cycle is cleared: the work ends when the line holds it.");
            }
            else if (rule is not null && held?.Cycle is { } kept)
            {
                var first = NodeGraphHomes.OfLanding(rule with { Cycle = kept }, LandingSource.Repository).Problems.FirstOrDefault();
                Say(first is null ? $"  Its cycle is kept: {ChoiceCycles.Said(kept)}." : $"  Its cycle is kept as written, and does not hold under this rule: {first.Words}");
            }

            if (rule is not null && workspace is not null)
            {
                Say("  A repository with a rule of its own keeps it — `daoris driver landing <repository> --clear` hands it back.");
            }

            Say($"  Written to {path} — the review reads it at each press, so nothing restarts.");
            return 0;
        });
    }

    /// <summary>The lines this verb's keys add to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        static string Spelled(LandingRule rule) =>
            (rule.Form == LandingForm.Branch ? $"branch {rule.Pattern}" : rule.Form)
            + (rule.Key is { Length: > 0 } key ? $", key {key}" : "")
            + (rule.Plugin is { Length: > 0 } plugin ? $", plugin {plugin}" : "") + (rule.Tidy ? ", tidy" : "")
            + (rule.AutoAccept ? ", accept automatically" : "") + (rule.Squash ? ", squash" : "");
        foreach (var (repository, rule) in choices.Landings) Say($"  landing    {repository}  {Spelled(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceLandings) Say($"  landing    workspace {workspace}  {Spelled(rule)}  (for each repository there that sets none)");

        // FABRIC-GRAPH1d: what the file keeps that no unit reads is said, never skipped without a word.
        foreach (var stray in NodeGraphHomes.Strays(choices)) Say($"  unread     {stray.Words}");
    }
}

/// <summary>
/// A cycle as a door says it and reads it (FABRIC-GRAPH1c): what the landing's and the work cycles' verbs share.
/// </summary>
internal static class ChoiceCycles
{
    /// <summary>
    /// A cycle's size and end, as a door says it: `1 node and 1 wire, ending at `qa``; a fragment's end is the line where it names none,
    /// and a work cycle's (<paramref name="end"/> empty) none.
    /// </summary>
    public static string Said(string cycle, string end = NodeKinds.Line)
    {
        var parts = NodeGraphs.Parts(cycle, out _);
        var nodes = parts?.Nodes.Count ?? 0;
        var wires = parts?.Wires.Count ?? 0;
        return $"{nodes} node{(nodes == 1 ? "" : "s")} and {wires} wire{(wires == 1 ? "" : "s")}, ending at `{parts?.End ?? end}`";
    }

    /// <summary>
    /// A cycle's file, read for a door that sets it (FABRIC-GRAPH1c): its JSON as <c>driver.json</c> keeps it, compact, every value as
    /// written, or the refusal naming the file. A path is the terminal's, from the folder it was run in.
    /// </summary>
    public static string File(DriverChoicesWorld world, string file)
    {
        var path = Path.IsPathFullyQualified(file) ? file : Path.GetFullPath(Path.Combine(world.Folder, file));
        if (!System.IO.File.Exists(path)) throw new ChoiceWords($"there is no file `{file}`. Nothing was written.");
        try
        {
            return NodeGraphHomes.Compact(System.IO.File.ReadAllText(path));
        }
        catch (JsonException)
        {
            throw new ChoiceWords($"`{file}` is not readable JSON. Nothing was written.");
        }
    }
}
