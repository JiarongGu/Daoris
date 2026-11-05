using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.DriverWorkflowsCommand;

namespace Daoris.Driver;

/// <summary>The verbs that read: listing the workflows, their defaults, and showing a workflow or what a place follows.</summary>
internal sealed class WorkflowListing(WorkflowTerminal run)
{
    internal int List()
    {
        var ids = WorkflowPorts.Store(run.Home).List();
        if (ids.Count == 0) run.Say($"daoris: no workflow is named in {run.Folder} yet.");
        else run.Say($"daoris: {(ids.Count == 1 ? "1 workflow" : $"{ids.Count} workflows")} named in {run.Folder}:");
        foreach (var found in ids)
        {
            var read = found.Read;
            if (read.Problem is not null)
            {
                run.Say($"  `{found.Id}` — cannot be read: {read.Problem}");
                continue;
            }

            var newest = read.Versions[^1];
            if (newest.Steps is not null)
            {
                run.Say($"  `{found.Id}` — {read.Name}: v{newest.Version}, {newest.Steps.Count} steps. {WorkflowPresets.Line(newest.Steps)}");
                continue;
            }

            // FABRIC-CUTOVER1d: a graph version reads and runs as a version of steps does.
            if (newest.Graph is not null)
            {
                run.Say($"  `{found.Id}` — {read.Name}: v{newest.Version}, {WorkflowWords.GraphSaid(newest)}");
                continue;
            }

            var reads = read.Versions.Reverse().FirstOrDefault(each => each.Steps is not null || each.Graph is not null);
            run.Say($"  `{found.Id}` — {read.Name}: v{newest.Version} cannot be read here ({newest.Unrun})"
                + (reads is null ? "; no version of it reads here." : $"; v{reads.Version} is the newest that reads."));
        }

        run.Say("Defaults built in, for `daoris workflow new <id> --from <default>` (`--with-opinion`, `--with-look`):");
        foreach (var preset in WorkflowPresets.GraphTable)
        {
            run.Say($"  `{preset.Id}` — {preset.Name}, a graph. {WorkflowPresets.GraphLine(preset.Graph())}");
        }

        run.Say(WorkflowWords.WhatChooses);
        return 0;
    }

    /// <summary>
    /// <c>defaults [&lt;default&gt; [--to &lt;file&gt;]]</c>: the defaults Daoris ships, each with what it is for; one said
    /// whole, its names, line, delivery and nodes; or its graph written to a new file, which <c>apply</c> saves as a version. Reads only.
    /// </summary>
    internal int Defaults()
    {
        var named = run.Argv.ElementAtOrDefault(2) is { } token && !token.StartsWith("--", StringComparison.Ordinal) ? token : null;
        run.Flags("defaults", named is null ? 2 : 3, ["--to"], []);
        if (named is null && run.Argv.Count > 2) throw new ChoiceWords(Usage);
        if (named is null)
        {
            run.Say("daoris: the defaults Daoris ships, each a graph `daoris workflow new <id> --from <default>` copies (`--with-opinion`, `--with-look`):");
            foreach (var each in WorkflowDefaults.All) run.Say($"  `{each.Id}` — {each.Name}. {each.For}");
            run.Say("  `daoris workflow defaults <default>` says one whole, and `--to <file>` writes its graph to a file.");
            return 0;
        }

        var shipped = WorkflowDefaults.Of(named) ?? throw new ChoiceWords(
            $"`{named}` is no default Daoris ships — `daoris workflow defaults` names them, and `show {named}` shows a workflow saved here.");
        if (run.FlagValue("--to") is { } to)
        {
            var path = run.FileOperand(to);
            if (File.Exists(path) || Directory.Exists(path)) throw new ChoiceWords($"`{to}` exists already: a default is written to a new file, and never over one.");
            AtomicFile.WriteText(path, JsonFile.Serialize(shipped.Graph()));
            run.Say($"daoris: the default `{shipped.Id}`'s graph written to {path}: `daoris workflow apply <id> {to}` saves it as a version.");
            return 0;
        }

        var graph = NodeGraphs.Read(shipped.GraphJson).Graph!;
        var delivery = shipped.Delivery;
        run.Say($"daoris: `{shipped.Id}` — {shipped.Name}, a default Daoris ships: {WorkflowWords.GraphSaid(new NamedVersion(1, null, null, null, null, shipped.Digest, graph))}");
        if (shipped.Aliases.Count > 0) run.Say($"  Also named {string.Join(", ", shipped.Aliases.Select(alias => $"`{alias}`"))}.");
        run.Say($"  {shipped.For}");
        run.Say($"  {WorkflowPresets.GraphLine(shipped.Graph())}");
        run.Say($"  Its delivery: method `{delivery.Method}`, accept `{delivery.Accept}`{(delivery.Plugin is { } plugin ? $", plugin `{plugin}`" : "")}"
            + $"{(delivery.Request is { } request ? $", request `{request}`" : "")}{(shipped.SetUp ? "; a place's quick set-up offers it" : "")}.");
        run.Say("  Nodes, by id:");
        foreach (var node in graph.Nodes)
        {
            var options = string.Join("; ", node.Options.Select(option => $"{option.Key} `{NodeOptions.Value(option.Value)}`"));
            run.Say($"    {node.Id} · {node.Kind}{(options.Length > 0 ? $": {options}" : "")}");
        }

        run.Say($"  Wires: {string.Join(", ", graph.Wires.Select(wire => $"{wire.From?.Node} → {wire.To.Node}"))}.");
        run.Say($"  `daoris workflow new <id> --from {shipped.Id}` copies it, and the copy keeps where it came from.");
        return 0;
    }

    internal int Show()
    {
        run.Flags("show", 3, [], []);
        var (id, version) = WorkflowOperations.Named(run.Argv[2]);
        var read = run.Required(id).Read;
        if (read.Problem is not null) throw new ChoiceWords($"`{id}` cannot be read: {read.Problem}", 1);
        var shown = version is null ? read.Versions[^1] : read.Versions.FirstOrDefault(each => each.Version == version);
        if (shown is null) throw new ChoiceWords($"`{id}` keeps no v{version} — it keeps {Kept(read)}.");
        if (shown.Graph is not null) throw new ChoiceWords(WorkflowWords.NoSteps(id, shown), 1);
        if (shown.Steps is null) throw new ChoiceWords($"`{id}` v{shown.Version} cannot be read here: {shown.Unrun} The file keeps it as written.", 1);

        var newest = ReferenceEquals(shown, read.Versions[^1]);
        run.Say($"daoris: `{id}` — {read.Name}: v{shown.Version}{(newest ? ", its newest" : "")}, saved {shown.At} {WorkflowWords.DoorSaid(shown.Door!)} "
            + $"(digest {shown.Digest}).");
        run.Say($"  {WorkflowPresets.Line(shown.Steps)}");
        foreach (var step in shown.Steps)
        {
            foreach (var line in WorkflowWords.NamedStepSaid(step)) run.Say(line);
        }

        run.Say($"  Versions kept: {string.Join(", ", read.Versions.Select(each => $"v{each.Version}{(each.Steps is not null ? "" : each.Graph is not null ? " (a graph)" : " (cannot be read here)")}"))}.");
        run.Say(WorkflowWords.WhatChooses);
        return 0;
    }

    /// <summary>
    /// The workflow a repository's or a workspace's work follows (WORKFLOW1a, WORKFLOW1e; design §4.1, §4.7): resolved for the kind
    /// named, as a task of that kind would be. Current is drawn from the rules as they stand; a named workflow, its newest version,
    /// which new work binds. A workspace's is read from the file alone; a repository's workspace from the registry.
    /// </summary>
    internal async Task<int> ShowChosenAsync()
    {
        const string usage = "`driver workflow show` takes --repository <name>|--workspace <name>, and --kind <kind> for a kind of task: "
            + "the workflow its work follows, Current or the one chosen — e.g. `daoris workflow show --repository web-app`; or a "
            + "named workflow's <id>[@<version>].";
        string[] valued = ["--repository", "--workspace", "--kind"];
        for (var at = 2; at < run.Argv.Count; at++)
        {
            var token = run.Argv[at];
            if (valued.Contains(token)) at++;
            else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `workflowshow` takes — {usage}");
            else throw new ChoiceWords(usage);
        }

        var repository = run.FlagValue("--repository");
        var workspace = run.FlagValue("--workspace");
        var kind = run.FlagValue("--kind");
        if ((repository is null) == (workspace is null)) throw new ChoiceWords(usage);
        var task = kind is null ? null : new WorkflowTaskChoice(kind, null);
        if (workspace is not null)
        {
            var selected = WorkflowSelection.Resolve(run.Choices, null, workspace, task);
            var whose = $"the workspace `{workspace}`{KindSaid(selected)}";
            if (selected.Workflow != WorkflowSelection.Current) return ShowNamed(selected, whose, workspace, null, [], workspace);
            foreach (var line in WithChoice([NoneSaid(whose)], selected, workspace, null, [], workspace, kind)) run.Say(line);
            return 0;
        }

        var (notes, inWorkspace) = await new WorkflowChoosing(run).WorkspaceOfAsync(repository!).ConfigureAwait(false);
        var chosen = WorkflowSelection.Resolve(run.Choices, repository, inWorkspace, task);
        var named = $"`{repository}`{KindSaid(chosen)}";
        var circle = RemoteTarget.Workspace(inWorkspace);
        if (chosen.Workflow != WorkflowSelection.Current) return ShowNamed(chosen, named, circle, repository, notes, inWorkspace);
        foreach (var line in WithChoice([NoneSaid(named)], chosen, circle, repository, notes, inWorkspace, kind)) run.Say(line);
        return 0;
    }

    internal static string NoneSaid(string whose) =>
        $"{whose} follows no workflow: nothing runs or lands its work. `daoris workflow use <id> …` chooses one.";

    /// <summary>The lines of a place that follows no workflow, with the registry's notes and, where a choice or a kind is read, what chose it under the header; then the routes.</summary>
    internal List<string> WithChoice(
        IReadOnlyList<string> said, WorkflowSelected selected, string circle, string? owner, IReadOnlyList<string> notes, string? inWorkspace, string? kind)
    {
        var lines = new List<string> { said[0] };
        lines.AddRange(notes);
        if (!(selected.Level == WorkflowLevels.Current && kind is null)) lines.Add($"  Chosen: {WorkflowWords.WhyChosen(selected, owner, circle)}.");
        lines.AddRange(UndeclaredSaid(selected, circle));
        lines.AddRange(RoutesSaid(owner, circle, inWorkspace));
        lines.AddRange(said.Skip(1));
        return lines;
    }

    /// <summary>
    /// The start node's routes (FABRIC-PIPELINE1a, the pipeline design §1.3), said before the steps: the default, each kind the
    /// workspace declares by its label and paths, and the landed route, each with the workflow it names and what chose it.
    /// </summary>
    internal List<string> RoutesSaid(string? owner, string circle, string? inWorkspace)
    {
        string Target(WorkflowSelected selected) =>
            $"{(selected.Workflow == WorkflowSelection.Current ? "no workflow" : $"`{selected.Workflow}`")}, {WorkflowWords.WhyChosen(selected, owner, circle)}";
        var lines = new List<string>
        {
            "  Its start node routes the work that comes here:",
            $"    the default → {Target(WorkflowSelection.Resolve(run.Choices, owner, inWorkspace, null))}",
        };
        if (run.Choices.WorkspaceWorkflows.TryGetValue(circle, out var shared))
        {
            foreach (var (id, declared) in shared.Kinds)
            {
                var paths = string.Concat(declared.Paths.Select(path => $", `{path}`"));
                lines.Add($"    kind `{id}` ({declared.Label}{paths}) → {Target(WorkflowSelection.Resolve(run.Choices, owner, inWorkspace, new WorkflowTaskChoice(id, null)))}");
            }
        }

        lines.Add($"    landed work → {WorkflowWords.LandedSaid(WorkflowSelection.ResolveLanded(run.Choices, owner, inWorkspace), owner, circle)}");
        return lines;
    }

    /// <summary>A named workflow chosen: its newest version, which new work binds, or why it cannot be read here.</summary>
    internal int ShowNamed(WorkflowSelected selected, string whose, string circle, string? owner, IReadOnlyList<string> notes, string? inWorkspace)
    {
        var why = WorkflowWords.WhyChosen(selected, owner, circle);
        var found = run.Load(selected.Workflow);
        var newest = found?.Read.Problem is null && found is not null ? found.Read.Versions[^1] : null;
        if (newest is { Graph: not null })
        {
            // FABRIC-CUTOVER1d: a graph newest is followed as a version of steps is, its graph said whole.
            run.Say($"daoris: {whose} follows `{selected.Workflow}` — {found!.Read.Name}, v{newest.Version} (digest {newest.Digest}), {why}.");
            foreach (var line in notes.Concat(UndeclaredSaid(selected, circle)).Concat(RoutesSaid(owner, circle, inWorkspace))) run.Say(line);
            run.Say($"  {char.ToUpperInvariant(WorkflowWords.GraphSaid(newest)[0])}{WorkflowWords.GraphSaid(newest)[1..]}");
            run.Say(WorkflowWords.WhatChooses);
            return 0;
        }

        if (newest?.Steps is null)
        {
            var problem = found is null ? "no workflow by that id is saved here"
                : found.Read.Problem ?? $"v{newest!.Version} cannot be read here: {newest.Unrun}";
            run.Say($"daoris: {whose} chooses `{selected.Workflow}`, {why}, which cannot be read here: {problem.TrimEnd('.')}.");
            foreach (var line in notes.Concat(UndeclaredSaid(selected, circle))) run.Say(line);
            run.Say(WorkflowWords.WhatChooses);
            return 1;
        }

        run.Say($"daoris: {whose} follows `{selected.Workflow}` — {found!.Read.Name}, v{newest.Version} (digest {newest.Digest}), {why}.");
        foreach (var line in notes.Concat(UndeclaredSaid(selected, circle)).Concat(RoutesSaid(owner, circle, inWorkspace))) run.Say(line);
        run.Say($"  {WorkflowPresets.Line(newest.Steps)}");
        foreach (var step in newest.Steps)
        {
            foreach (var line in WorkflowWords.NamedStepSaid(step)) run.Say(line);
        }

        run.Say(WorkflowWords.WhatChooses);
        return 0;
    }

    /// <summary><c> for kind `docs` (Documentation),</c> where a kind is read; nothing where none is.</summary>
    internal static string KindSaid(WorkflowSelected selected) =>
        selected.Kind is null ? "" : $", for kind `{selected.Kind}`{(selected.Label is null ? "" : $" ({selected.Label})")},";

    /// <summary>A kind named that the workspace does not declare, read as none.</summary>
    internal static IEnumerable<string> UndeclaredSaid(WorkflowSelected selected, string circle) =>
        selected.Undeclared is null ? [] : [$"  `{selected.Undeclared}` is not a kind the workspace `{circle}` declares, so it is read as none."];
}
