using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.DriverWorkflowsCommand;

namespace Daoris.Driver;

/// <summary>The verbs that write a workflow: new, edit, apply, export and import.</summary>
internal sealed class WorkflowWriting(WorkflowTerminal run)
{
    internal async Task<int> CreateAsync()
    {
        run.Flags("new", 3, ["--from", "--name"], ["--with-opinion", "--with-look", "--plan"]);
        var id = run.Argv[2];
        if (WorkflowNamed.IdProblem(id) is { } idProblem) throw new ChoiceWords(idProblem, 1);
        var from = run.FlagValue("--from")
            ?? throw new ChoiceWords($"`driver workflow new` needs --from: a default or a saved workflow — {Usage}");
        if (run.Load(id) is { } existing)
        {
            var at = existing.Read.Versions.Count > 0 ? $", at v{existing.Read.Versions[^1].Version}" : "";
            throw new ChoiceWords($"`{id}` is a workflow here already{at}: `daoris workflow apply {id} <file>` adds a version to it.", 1);
        }

        var opinion = run.Argv.Contains("--with-opinion");
        var look = run.Argv.Contains("--with-look");
        var asked = new JsonObject { ["id"] = id, ["from"] = from };
        if (opinion) asked["withOpinion"] = true;
        if (look) asked["withLook"] = true;
        if (run.FlagValue("--name") is { } given) asked["name"] = given;
        var gated = string.Join(" and ", new[] { opinion ? "with a second opinion" : null, look ? "with your look first" : null }.OfType<string>());

        var source = await WorkflowOperations.SourceAsync(run.Choices, run.Home, run.Registry, from, opinion, look, run.Ct, id).ConfigureAwait(false);
        var name = run.FlagValue("--name") ?? source.Name;
        var also = gated.Length > 0 ? $", {gated}" : "";
        var graph = source.Graph!;
        var planned = WorkflowOperations.PlanGraph(run.Home, null, id, null, name, "terminal", run.Moment(), graph, source.From);
        run.Say($"daoris: `{id}` v1, a new workflow — {(string?)planned.Plan.Result!["name"]}, from {source.Description}{also}, a graph:");
        run.Say($"  {WorkflowPresets.GraphLine(graph)}");
        if (source.GraphPreset?.NoPress == true) run.Say("  With no press: choosing it is refused for a repository whose line does not declare its gates and have them accepted.");
        if (source.From is { } copied) run.Say($"  A copy of the default `{copied.Default}` (digest {copied.Digest}): the version keeps where it came from.");
        return run.Saved(id, planned.Plan.Result, [asked]);
    }

    /// <summary>The base a change is made against: the newest version, which <c>--base</c> must name where it is given (design §8.1).</summary>
    internal (NamedVersion Version, JsonArray? Raw) BaseOf(WorkflowFound found) => WorkflowOperations.BaseOf(found.Of, run.FlagValue("--base"));

    internal int Edit()
    {
        // WORKFLOWCLEAN1 1b: `edit` composed a list of steps, which is no longer saved; the editor and `apply <id> <graph-file>` save a graph.
        var id = run.Argv.ElementAtOrDefault(2) is { } token && !token.StartsWith("--", StringComparison.Ordinal) ? WorkflowOperations.Named(token).Id : null;
        throw new ChoiceWords(WorkflowNamed.StepsRefused(id), 1);
    }

    internal int Apply()
    {
        var asGraph = run.Argv.Contains("--as-graph");
        run.Flags("apply", asGraph ? 3 : 4, ["--base"], ["--plan", "--as-graph"]);
        var id = WorkflowOperations.Named(run.Argv[2]).Id;
        if (asGraph)
        {
            throw new ChoiceWords($"`apply {id} --as-graph` is folded into `daoris convert`, which converts what was saved as steps as you press it: "
                + "`daoris convert --plan`. Nothing was written.", 1);
        }

        var found = run.Required(id);
        var (version, _) = BaseOf(found);
        var held = WorkflowTerminal.Parsed(run.FileOperand(run.Argv[3]));
        // A graph (FABRIC-GRAPH1f): the file's own, a version's of schema 2, or a workflow's newest version's.
        var versions = held is JsonObject record && record["versions"] is JsonArray listed ? listed : null;
        if (GraphIn(versions is null ? held : versions.Count > 0 ? versions[^1] : null, out var graph)) return ChangeGraph(found, version, graph);
        // A list of steps, however the file holds it, is no longer saved (WORKFLOWCLEAN1 1b).
        if (held is JsonArray || (held is JsonObject holder && holder["steps"] is JsonArray)
            || (versions is { Count: > 0 } && versions[^1] is JsonObject last && last.ContainsKey("steps")))
        {
            throw new ChoiceWords(WorkflowNamed.StepsRefused(id), 1);
        }

        throw new ChoiceWords($"`{run.Argv[3]}` holds no graph: a version's `graph` (schema 2), a graph itself, or a workflow's `versions`, its newest taken.", 1);
    }

    /// <summary>
    /// A new version that is a graph, against the base: judged whole as the driver judges it, said with its digest,
    /// and saved unless <c>--plan</c>. The same graph as the base's is nothing to do.
    /// </summary>
    /// <param name="what">What the graph is, said after the workflow's name, where it is not a file's.</param>
    /// <param name="lines">Lines said after the header, before what is written.</param>
    internal int ChangeGraph(WorkflowFound found, NamedVersion @base, JsonNode? graph, string? what = null, IReadOnlyList<string>? lines = null)
    {
        var planned = WorkflowOperations.PlanGraph(run.Home, found.Of, found.Id, @base, null, "terminal", run.Moment(), graph);
        var (plan, next) = (planned.Plan, planned.Next!);
        if (planned.Same)
        {
            run.Say($"daoris: `{found.Id}` v{@base.Version} already says this: nothing was written.");
            return 0;
        }

        run.Say($"daoris: `{found.Id}` v{@base.Version} → v{plan.Version} — {found.Read.Name}{(what is null ? "" : $", {what}")}: {WorkflowWords.GraphSaid(next)}");
        foreach (var line in lines ?? []) run.Say(line);
        return run.Saved(found.Id, plan.Result!, [new JsonObject
        {
            ["id"] = found.Id, ["graph"] = graph?.DeepClone(), ["base"] = @base.Version, ["authorize"] = run.Authorize(found.Id),
        }]);
    }

    internal int Export()
    {
        run.Flags("export", 3, ["--to"], []);
        var id = WorkflowOperations.Named(run.Argv[2]).Id;
        var found = run.Required(id);
        if (found.File is null) throw new ChoiceWords($"`{id}` cannot be exported: {found.Read.Problem}", 1);
        var text = WorkflowNamed.Text(found.File);
        if (run.FlagValue("--to") is not { } to)
        {
            run.Say(text[..^1]);
            return 0;
        }

        var path = run.FileOperand(to);
        if (File.Exists(path) || Directory.Exists(path)) throw new ChoiceWords($"`{to}` exists already: export writes a new file, and never over one.");
        AtomicFile.WriteText(path, text);
        var count = found.Read.Versions.Count;
        run.Say($"daoris: `{id}` written to {path}, with {(count == 1 ? "its version" : $"its {count} versions")}: "
            + "`daoris workflow import <file>` reads it on another machine.");
        return 0;
    }

    internal int Import()
    {
        run.Flags("import", 3, [], ["--plan"]);
        var from = run.Argv[2];
        var file = WorkflowTerminal.Parsed(run.FileOperand(from));
        var read = file is null ? WorkflowNamed.Read(default) : WorkflowNamed.Read(WorkflowNamed.ElementOf(file));
        if (read.Problem is not null) throw new ChoiceWords($"`{from}` cannot be imported: {read.Problem}", 1);
        // A graph version that holds is a version like another here (FABRIC-GRAPH1f); one that does not read is refused.
        if (read.Versions.FirstOrDefault(each => each.Steps is null && each.Graph is null) is { } unread)
        {
            throw new ChoiceWords($"`{from}` cannot be imported: {unread.Unrun}", 1);
        }

        var id = read.Id!;
        if (read.Versions.Any(each => each.Steps is not null)) throw new ChoiceWords(WorkflowNamed.StepsRefused(id), 1);
        var newest = read.Versions[^1];
        if (run.Load(id) is { } here)
        {
            var same = here.Read.Problem is null && here.Read.Versions.Count == read.Versions.Count
                && here.Read.Versions.Select((each, index) => each.Version == read.Versions[index].Version && each.Digest == read.Versions[index].Digest).All(each => each);
            if (same)
            {
                run.Say($"daoris: `{id}` is here already, with the same versions: nothing was written.");
                return 0;
            }

            var at = here.Read.Versions.Count > 0 ? $", at v{here.Read.Versions[^1].Version}" : "";
            throw new ChoiceWords($"`{id}` is a workflow here already{at}, and the file's versions differ: `daoris workflow apply {id} {from}` "
                + "adds the file's newest version to it.", 1);
        }

        // A verification pipeline is made so here, its mark in its file (FABRIC-PIPELINE1a), and said so.
        var marked = read.Pipeline == WorkflowPipelines.Verification ? ", a verification pipeline" : "";
        var header = $"daoris: `{id}` — {read.Name}{marked}, from {from}: {(read.Versions.Count == 1 ? "one version" : $"{read.Versions.Count} versions")}, "
            + $"its newest v{newest.Version}:";
        run.Say($"{header} {WorkflowWords.GraphSaid(newest)}");

        // Through the window's save a file goes a version at a time, each confirmed there.
        var versions = file!["versions"]?.AsArray();
        var payloads = new List<JsonObject>();
        for (var at = 0; at < read.Versions.Count && versions is not null; at++)
        {
            var asked = new JsonObject { ["id"] = id };
            if (at == 0) asked["name"] = read.Name;
            else asked["base"] = at;
            var version = versions[at]!.AsObject();
            if (version["graph"] is { } graph)
            {
                // The one judge at the save point: a file written straight to the home is judged as the window's save is.
                if (WorkflowOperations.GraphProblems(run.Home, graph) is [var refused, ..]) throw new ChoiceWords($"`{from}` cannot be imported: v{at + 1}: {refused.Words}", 1);
                asked["graph"] = graph.DeepClone();
            }
            payloads.Add(asked);
        }

        if (payloads.Count == 0) payloads.Add(new JsonObject { ["id"] = id });
        var cannot = read.Pipeline == WorkflowPipelines.Verification
            ? "The window's save cannot carry a verification pipeline's mark, so this file is not imported while Daoris runs: stop it first, or save its graph from the window."
            : null;
        return run.Saved(id, file!, payloads, cannot);
    }
}
