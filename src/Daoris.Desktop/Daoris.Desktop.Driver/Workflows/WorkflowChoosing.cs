using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.DriverWorkflowsCommand;

namespace Daoris.Driver;

/// <summary>The verbs that choose: use, use for the landed gates, and a kind of a workspace.</summary>
internal sealed class WorkflowChoosing(WorkflowTerminal run)
{
    /// <summary>
    /// <c>use &lt;id&gt;|current|--clear --repository &lt;name&gt;|--workspace &lt;name&gt; [--kind &lt;kind&gt;]</c> (design §4.7): which
    /// workflow a repository's or a workspace's work follows, or a kind's. A named workflow is one saved here whose newest version
    /// reads (§3.9); a repository's kind is one its workspace declares, where the registry says which that is.
    /// </summary>
    internal async Task<int> UseAsync()
    {
        const string usage = "`driver workflow use` takes <id>|--clear (`current` only with --landed), then --repository <name>|--workspace <name>, and --kind <kind> "
            + "for a kind of task or --landed for the work that lands — e.g. `daoris workflow use docs-to-pr --workspace work --kind docs`.";
        string[] valued = ["--repository", "--workspace", "--kind"];
        var operands = new List<string>();
        for (var at = 2; at < run.Argv.Count; at++)
        {
            var token = run.Argv[at];
            if (valued.Contains(token)) at++;
            else if (token is "--clear" or "--plan" or "--landed") continue;
            else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `workflowuse` takes — {usage}");
            else operands.Add(token);
        }

        var repository = run.FlagValue("--repository");
        var workspace = run.FlagValue("--workspace");
        var kind = run.FlagValue("--kind");
        var clear = run.Argv.Contains("--clear");
        if ((repository is null) == (workspace is null) || operands.Count != (clear ? 0 : 1)) throw new ChoiceWords(usage);
        var chosen = clear ? null : operands[0];

        // A named workflow is chosen only where it reads here (design §3.9): refused where it would be chosen, naming why. Its file's
        // mark (FABRIC-PIPELINE1a) goes with the edit, so a verification pipeline is named by the landed route alone.
        var pipeline = WorkflowOperations.CheckChosen(run.Home, chosen);
        var given = new WorkflowUse(chosen, repository, workspace, kind) { Pipeline = pipeline };
        if (run.Argv.Contains("--landed")) return await UseLandedAsync(given, repository, workspace, chosen).ConfigureAwait(false);
        var used = await WorkflowOperations.UseAsync(run.Choices, run.Home, run.Registry, chosen, repository, workspace, kind, run.Ct).ConfigureAwait(false);
        if (used.Refusal is not null) throw new ChoiceWords(used.Refusal, 1);
        foreach (var line in used.Lines) run.Say(line);
        if (!used.Changed) return 0;
        var asked = new JsonObject { [repository is not null ? "repository" : "workspace"] = repository ?? workspace };
        if (chosen is not null) asked["workflow"] = chosen;
        if (kind is not null) asked["kind"] = kind;
        return SavedChoice(used.Edit!, ChoiceRoutes.WorkflowUse, asked);
    }


    /// <summary>
    /// <c>use &lt;id&gt;|current|--clear … --landed</c> (FABRIC-PIPELINE1a, the pipeline design §1.3, §4.2): the landed route, a
    /// verification pipeline for the work that reaches the line. For a repository it shows the <c>daoris.gates.json</c> at its line's
    /// tip and accepts it by its blob, as the tab's <i>Accept the gates</i> does; run again, it accepts the tip's new blob. A
    /// workspace's route accepts its gates repository by repository. Judged before any git is read.
    /// </summary>
    internal async Task<int> UseLandedAsync(WorkflowUse given, string? repository, string? workspace, string? chosen)
    {
        WorkflowUse Edit(string? gates) => given with { Repository = repository, Workspace = repository is null ? workspace : null, Landed = true, Gates = gates };
        Judge(Edit(null));
        if (repository is null)
        {
            IReadOnlyList<string> shared = chosen is not null && chosen != WorkflowSelection.Current
                ? [$"  Its gates are accepted repository by repository, at each line's tip: `daoris workflow use {chosen} --repository <name> "
                    + "--landed` accepts one's."]
                : [];
            return LandedDone(Edit(null), workspace, shared, repository, workspace, chosen);
        }

        var own = run.Choices.Workflows.TryGetValue(repository.Trim(), out var held) ? held.Landed : null;
        // A tip that cannot be read keeps what was accepted for the same pipeline: an acceptance names a blob, never a moment.
        var kept = own is not null && own.Workflow == chosen ? own.Gates : null;
        var read = await run.World.Registry(run.Ct).ConfigureAwait(false);
        var inWorkspace = read.Checkouts?.FirstOrDefault(each => InScope(each.Repository, repository)).Workspace;
        if (chosen is null || chosen == WorkflowSelection.Current) return LandedDone(Edit(null), inWorkspace, [], repository, workspace, chosen);
        if (read.Checkouts is not { } checkouts)
        {
            return LandedDone(Edit(kept), inWorkspace, [$"  No gates were accepted: the registry was not read ({read.Unread}). Run this again where it can be."],
                repository, workspace, chosen);
        }

        var root = checkouts.FirstOrDefault(each => InScope(each.Repository, repository) && each.Root is { } at && Directory.Exists(at)).Root;
        if (root is null)
        {
            return LandedDone(Edit(kept), inWorkspace,
                [$"  No gates were accepted: `{repository}` has no checkout here whose line could be read. Run this again where it has one."],
                repository, workspace, chosen);
        }

        var line = (await CanonicalLine.ResolveAsync(root, repository, inWorkspace, run.Choices, run.Ct).ConfigureAwait(false)).Branch;
        var (tip, problem) = line is null ? (null, "its line is not known here") : await LandedGates.ReadAsync(root, line, run.Ct).ConfigureAwait(false);
        if (tip is null)
        {
            return LandedDone(Edit(kept), inWorkspace,
                [$"  No gates were accepted: for `{repository}`, {problem}. Run this again where its line can be read."], repository, workspace, chosen);
        }

        var at = $"`{line}`'s tip `{tip.Commit[..7]}`";
        if (tip.Gates == WorkflowSelection.NoGates)
        {
            return LandedDone(Edit(WorkflowSelection.NoGates), inWorkspace,
                [$"  {at} holds no `daoris.gates.json`: accepted as none, so its checks find no declaration until one lands and you accept it."],
                repository, workspace, chosen);
        }

        var text = ((await LandedGates.TextAsync(root, tip.Gates, run.Ct).ConfigureAwait(false)) ?? "").TrimEnd();
        var lines = new List<string> { $"  The gates it runs unasked, as `daoris.gates.json` declares them at {at} (blob `{tip.Gates[..7]}`):" };
        lines.AddRange(text.Split('\n').Select(each => $"    {each.TrimEnd('\r')}"));
        lines.Add("  Accepted: these gates run unasked on the work that lands; an edit of `daoris.gates.json` asks you again, and nothing runs "
            + "until you accept it.");
        if (own?.Gates is { } before && before != WorkflowSelection.NoGates && before != tip.Gates)
        {
            lines.Add($"  It replaces the gates accepted before, at blob `{before[..7]}`: what changed is `git diff {before} {tip.Gates}`.");
        }

        return LandedDone(Edit(tip.Gates), inWorkspace, lines, repository, workspace, chosen);
    }

    /// <summary>The landed route edited, said and written, or not with <c>--plan</c>: its pipeline, what was accepted, and what it lowers.</summary>
    internal int LandedDone(WorkflowUse edit, string? inWorkspace, IReadOnlyList<string> accepted, string? repository, string? workspace, string? chosen)
    {
        var edited = Judge(edit);
        var whose = repository is not null ? $"`{repository}`" : $"the workspace `{workspace}`";
        var name = (repository ?? workspace)!.Trim();
        WorkflowChoice? Pick(DriverConfig config) => ChoiceAt(repository is not null ? config.Workflows : config.WorkspaceWorkflows, name);
        if (Shape(Pick(edited), repository is null) == Shape(Pick(run.Choices), repository is null))
        {
            run.Say($"daoris: {whose} {(chosen is null ? "names no landed route" : $"names `{chosen}` for the work that lands already, its gates as accepted")}: "
                + "nothing was written.");
            return 0;
        }

        if (chosen is null)
        {
            run.Say($"daoris: {whose} no longer names a landed route: {(repository is not null
                ? "its workspace's reaches it, where it names one." : "each repository there verifies only the work it names a route for.")}");
        }
        else if (chosen == WorkflowSelection.Current)
        {
            run.Say($"daoris: {whose} now names no verification for the work that lands: `current`, which keeps its workspace's away.");
        }
        else
        {
            var found = run.Load(chosen)!;
            var newest = found.Read.Versions[^1];
            var graph = newest.Graph!;
            run.Say($"daoris: {whose} now verifies the work that lands with `{chosen}` — {found.Read.Name}, v{newest.Version} now: a graph "
                + $"(schema {WorkflowNamed.GraphSchema}) of {graph.Nodes.Count} node{(graph.Nodes.Count == 1 ? "" : "s")} and {graph.Wires.Count} "
                + $"wire{(graph.Wires.Count == 1 ? "" : "s")}, digest {newest.Digest}.");
        }

        foreach (var line in accepted) run.Say(line);
        // D157 point 10 (the pipeline design §1.3): what the change asks less of the person, said where they make it.
        var circle = repository is not null ? inWorkspace : workspace;
        if (WorkflowSelection.ResolveLanded(run.Choices, repository, circle).Workflow != WorkflowSelection.Current
            && WorkflowSelection.ResolveLanded(edited, repository, circle).Workflow == WorkflowSelection.Current)
        {
            run.Say("  This asks less of you: it leaves the work that lands unverified.");
        }

        if (chosen is not null)
        {
            run.Say(repository is not null
                ? $"  The landed route resolves on its own: `{repository}`'s development routes stay as they are."
                : "  The landed route resolves on its own: each repository there that names none of its own follows it, and every development "
                    + "route stays as it is.");
        }

        if (run.Plan)
        {
            run.Say("  --plan: nothing was written.");
            run.Say(WorkflowWords.LandedReads);
            return 0;
        }

        var payload = new JsonObject { [repository is not null ? "repository" : "workspace"] = repository ?? workspace };
        if (chosen is not null) payload["workflow"] = chosen;
        if (edit.Gates is { } blob && IntegrationGatesKey.IsAcceptance(blob)) payload["gates"] = blob;
        run.Write = new WorkflowDeferred(ChoiceRoutes.WorkflowLanded, [payload], () => StandingChoices.Write(run.World.ConfigPath, config => WorkflowSelection.Apply(config, edit)),
            [$"  Written to {run.World.ConfigPath}.", WorkflowWords.LandedReads]);
        return 0;
    }

    /// <summary>The run.Choices with <paramref name="edit"/> made, or its refusal in the twins' words: nothing was written.</summary>
    internal DriverConfig Judge(WorkflowChoiceEdit edit)
    {
        try
        {
            return WorkflowSelection.Apply(run.Choices, edit);
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords($"{refused.Message} Nothing was written.", 1);
        }
    }

    /// <summary>
    /// <c>kind &lt;workspace&gt; &lt;kind&gt; --label "…" [--paths &lt;path,…&gt;]</c> and <c>kind &lt;workspace&gt; &lt;kind&gt; --drop</c>
    /// (design §4.2, §4.7): a kind of task a workspace declares, with its label and the paths work of that kind keeps to; declared
    /// again, both are replaced.
    /// </summary>
    internal int Kind()
    {
        const string usage = "`driver workflow kind` takes <workspace> <kind> --label \"…\" [--paths <path,…>], or <workspace> <kind> --drop — "
            + "e.g. `daoris workflow kind work docs --label \"Documentation\" --paths \"docs/**,**/*.md\"`.";
        string[] valued = ["--label", "--paths"];
        var operands = new List<string>();
        for (var at = 2; at < run.Argv.Count; at++)
        {
            var token = run.Argv[at];
            if (valued.Contains(token)) at++;
            else if (token is "--drop" or "--plan") continue;
            else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `workflowkind` takes — {usage}");
            else operands.Add(token);
        }

        var label = run.FlagValue("--label");
        var drop = run.Argv.Contains("--drop");
        if (operands.Count != 2 || drop == (label is not null) || (drop && run.Argv.Contains("--paths"))) throw new ChoiceWords(usage);
        var (workspace, id) = (operands[0], operands[1]);
        var paths = (run.FlagValue("--paths") ?? "").Split(',').Select(each => each.Trim()).Where(each => each.Length > 0).ToList();

        var set = WorkflowOperations.KindSet(run.Choices, workspace, id, label, paths, drop);
        if (set.Refusal is not null) throw new ChoiceWords(set.Refusal, 1);
        foreach (var line in set.Lines) run.Say(line);
        var declared = new JsonObject { ["workspace"] = workspace, ["kind"] = id };
        if (drop) declared["drop"] = true;
        else declared["label"] = label;
        declared["paths"] = new JsonArray(paths.Select(each => (JsonNode)JsonValue.Create(each)!).ToArray());
        return SavedChoice(set.Edit!, ChoiceRoutes.WorkflowKind, declared);
    }

    /// <summary>The choice written, or not with <c>--plan</c>, and said either way, with what reads it: every gate, from a run's first start.</summary>
    internal int SavedChoice(WorkflowChoiceEdit edit, InstanceRoute route, JsonObject payload)
    {
        if (run.Plan)
        {
            run.Say("  --plan: nothing was written.");
            run.Say(WorkflowWords.GateReads);
            return 0;
        }

        run.Write = new WorkflowDeferred(route, [payload], () => StandingChoices.Write(run.World.ConfigPath, config => WorkflowSelection.Apply(config, edit)),
            [$"  Written to {run.World.ConfigPath}.", WorkflowWords.GateReads]);
        return 0;
    }

    /// <summary>
    /// A repository's workspace, the registry's, read as <c>review</c> reads it; unread, it is read only where no choice in the file is set
    /// for a workspace, since none could then reach it.
    /// </summary>
    internal async Task<(List<string> Notes, string? InWorkspace)> WorkspaceOfAsync(string name)
    {
        var read = await run.World.Registry(run.Ct).ConfigureAwait(false);
        var notes = new List<string>();
        string? inWorkspace = null;
        if (read.Checkouts is not { } checkouts)
        {
            if (run.Choices.WorkspaceWorkflows.Count > 0)
            {
                throw new ChoiceWords($"cannot say which workspace's run.Choices reach `{name}`: the registry could not be read — "
                    + $"{read.Unread} — and this file sets run.Choices for a workspace. `--workspace <name>` shows a workspace's from this file alone.");
            }

            notes.Add($"  The registry was not read ({read.Unread}); no choice here is set for a workspace, so none reaches it.");
        }
        else
        {
            var checkout = checkouts.FirstOrDefault(each => InScope(each.Repository, name));
            inWorkspace = checkout.Workspace;
            if (checkout.Repository is null)
            {
                notes.Add($"  `{name}` is not in the registry, so it is read as in no workspace: the `{RemoteTarget.Workspace(null)}` "
                    + "workspace's run.Choices reach it.");
            }
        }

        return (notes, inWorkspace);
    }
}
