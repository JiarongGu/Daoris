using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.WorkflowUsing;

namespace Daoris.Driver;

/// <summary>
/// The typed operations behind <c>daoris workflow new|edit|use|kind</c>: the terminal's verb and the
/// window's <c>WORKFLOW_SAVE</c>, <c>WORKFLOW_USE</c> and <c>WORKFLOW_KIND</c> call these and differ only in how they say the result, so a
/// refusal, a reach or a version is judged once. Nothing here writes <c>driver.json</c>; a caller writes the edit it is handed.
/// </summary>
public static class WorkflowOperations
{
    /// <summary>The choices' registry, read where it is needed.</summary>
    public delegate Task<ChoicesRegistry> RegistryRead(CancellationToken ct);

    /// <summary>
    /// A named workflow is chosen only where it is saved and its newest version reads. Returns the file's mark, which goes with the
    /// edit so a verification pipeline is named by the landed route alone.
    /// </summary>
    /// <exception cref="ChoiceWords">Naming why it cannot be chosen.</exception>
    public static string? CheckChosen(string home, string? chosen)
    {
        if (chosen is null || chosen == WorkflowSelection.Current || !WorkflowNamed.IsId(chosen)) return null;
        var found = WorkflowPorts.Store(home).Load(chosen) ?? throw new ChoiceWords(
            $"no workflow `{chosen}` is saved here — `daoris workflow list` names them, and `new` names one.");
        if (found.Read.Problem is not null) throw new ChoiceWords($"`{chosen}` cannot be read: {found.Read.Problem}", 1);
        var newest = found.Read.Versions[^1];
        if (newest.Steps is null && newest.Graph is null)
        {
            throw new ChoiceWords($"`{chosen}` v{newest.Version} cannot be read here: {newest.Problem} It can be chosen once a "
                + "version that reads is saved after it.", 1);
        }

        return found.Read.Pipeline;
    }

    /// <summary>The newest version of a saved workflow, or null where there is none that reads.</summary>
    public static int? NewestOf(string home, string id) =>
        WorkflowNamed.IsId(id) && WorkflowPorts.Store(home).Load(id) is { Read: { Problem: null, Versions: [.., var last] } } ? last.Version : null;

    public static async Task<WorkflowUsed> UseAsync(
        DriverConfig choices, string home, RegistryRead registry, string? chosen, string? repository, string? workspace, string? kind, CancellationToken ct) =>
        await WorkflowUsing.UseAsync(choices, home, registry, chosen, repository, workspace, kind, ct).ConfigureAwait(false);

    public static async Task<WorkflowUsePlan> PlanUseAsync(
        DriverConfig choices, string home, RegistryRead registry, string? chosen, string? repository, string? workspace, string? kind, CancellationToken ct) =>
        await WorkflowUsing.PlanUseAsync(choices, home, registry, chosen, repository, workspace, kind, ct).ConfigureAwait(false);

    public static async Task<WorkflowReached> ReachAsync(
        DriverConfig choices, string home, RegistryRead registry, string? chosen, string? repository, string? workspace, CancellationToken ct) =>
        await WorkflowUsing.ReachAsync(choices, home, registry, chosen, repository, workspace, ct).ConfigureAwait(false);


    /// <summary>
    /// <c>kind &lt;workspace&gt; &lt;kind&gt; --label "…" [--paths …]</c> and <c>--drop</c>: a kind of task a workspace
    /// declares, with its label and the paths work of that kind keeps to; declared again, both are replaced.
    /// </summary>
    public static WorkflowKindSet KindSet(DriverConfig choices, string workspace, string id, string? label, IReadOnlyList<string> paths, bool drop)
    {
        WorkflowChoiceEdit edit = drop ? new WorkflowDrop(id, workspace) : new WorkflowDeclare(id, workspace, label!, paths);
        DriverConfig edited;
        try
        {
            edited = WorkflowSelection.Apply(choices, edit);
        }
        catch (DriverException refused)
        {
            return new WorkflowKindSet(null, [], $"{refused.Message} Nothing was written.");
        }

        var lines = new List<string>();
        if (drop)
        {
            lines.Add($"daoris: the workspace `{workspace}` no longer declares kind `{id}`.");
            var mapping = choices.Workflows.Where(pair => pair.Value.Kinds.Any(each => each.Key == id)).Select(pair => pair.Key)
                .Order(StringComparer.Ordinal).Select(name => $"`{name}`").ToList();
            if (mapping.Count > 0)
            {
                lines.Add($"  {string.Join(", ", mapping)} still map{(mapping.Count == 1 ? "s" : "")} it; no task of that kind reaches "
                    + $"{(mapping.Count == 1 ? "that mapping" : "those mappings")} until `{id}` is declared again.");
            }
        }
        else
        {
            var declared = ChoiceAt(edited.WorkspaceWorkflows, workspace.Trim())?.KindOf(id);
            lines.Add($"daoris: the workspace `{workspace}` declares kind `{id}` — {label}, "
                + $"{(paths.Count == 0 ? "with no paths" : $"its paths {string.Join(", ", paths.Select(path => $"`{path}`"))}")}.");
            lines.Add(declared?.Workflow is { } workflow
                ? $"  It maps to `{workflow}` there."
                : $"  It maps to no workflow there: `daoris workflow use <id> --workspace {workspace} --kind {id}` maps one, and "
                    + "`--repository <name>` maps it for one repository.");
            // The documents default is for such a kind: it is offered, never chosen for it.
            if (declared?.Workflow is null && WorkflowDefaults.AreDocuments(paths) && WorkflowDefaults.Of("documents") is { } documents)
            {
                lines.Add($"  Its paths are documents: the default `{documents.Id}` merges such work into the line with no review, and "
                    + $"`daoris workflow new <id> --from {documents.Id}` copies it.");
            }
        }

        return new WorkflowKindSet(edit, lines, null);
    }

    public static async Task<IReadOnlyList<WorkflowReach>> PlaceReachAsync(
        DriverConfig choices, string home, RegistryRead registry, string? repository, string? workspace, CancellationToken ct) =>
        await WorkflowUsing.PlaceReachAsync(choices, home, registry, repository, workspace, ct).ConfigureAwait(false);


    /// <summary>The scope of the home's default: every repository that follows it with nothing written.</summary>
    public const string HomeLevel = "home";

    public const string HomeName = "this machine";

    /// <summary>The places a saved workflow is chosen for, in the order the file keeps them: what appending a version to it reaches.</summary>
    public static IReadOnlyList<WorkflowScope> ScopesOf(DriverConfig choices, string id)
    {
        var scopes = new List<WorkflowScope>();
        void Level(string level, IReadOnlyDictionary<string, WorkflowChoice> map)
        {
            foreach (var (name, choice) in map)
            {
                if (choice.Default == id) scopes.Add(new WorkflowScope(level, name, "default", null));
                scopes.AddRange(choice.Kinds.Where(pair => pair.Value.Workflow == id).Select(pair => new WorkflowScope(level, name, "kind", pair.Key)));
                if (choice.Landed?.Workflow == id) scopes.Add(new WorkflowScope(level, name, "landed", null));
            }
        }

        Level("repository", choices.Workflows);
        Level("workspace", choices.WorkspaceWorkflows);
        // The home's default reaches every place that names nothing of its own, so editing its copy asks consent for them all.
        if (choices.DefaultWorkflow == id) scopes.Add(new WorkflowScope(HomeLevel, HomeName, "default", null));
        return scopes;
    }

    /// <summary>
    /// The terminal's <c>new --from</c> source, resolved: a preset, a graph preset, <c>current:&lt;repository&gt;</c> or a saved workflow's version,
    /// with the second opinion and your look added by the presets' own rule.
    /// </summary>
    /// <exception cref="ChoiceWords">Naming why the source cannot be used.</exception>
    public static async Task<WorkflowVersionSource> SourceAsync(
        DriverConfig choices, string home, RegistryRead registry, string from, bool opinion, bool look, CancellationToken ct, string? asId = null)
    {
        // Converting Current has one door, and it is not a copy made by hand.
        if (from.StartsWith("current:", StringComparison.Ordinal))
        {
            throw new ChoiceWords(CurrentRetired);
        }

        if (!from.Contains('@', StringComparison.Ordinal) && WorkflowDefaults.Of(from) is { } shipped && WorkflowPresets.GraphOf(shipped.Id) is { } graphPreset)
        {
            var name = $"{graphPreset.Name}{(opinion ? " + second opinion" : "")}{(look ? " + your look" : "")}";
            return new($"the default `{shipped.Id}`", name, null, WorkflowPresets.WithGraphGates(graphPreset.Graph(), opinion, look), graphPreset)
            {
                From = shipped.From,
            };
        }

        var (sourceId, wanted) = Named(from);
        var found = WorkflowNamed.IsId(sourceId) ? WorkflowPorts.Store(home).Load(sourceId) : throw new ChoiceWords(WorkflowNamed.IdProblem(sourceId)!, 1);
        if (found is null)
        {
            throw new ChoiceWords($"`{from}` is no preset, no saved workflow and no `current:<repository>` — the presets are "
                + $"{string.Join(", ", WorkflowPresets.GraphTable.Select(each => $"`{each.Id}`"))}.");
        }

        // A copy of a list of steps would save one, and a list of steps is not saved.
        _ = VersionOf(found, wanted);
        throw new ChoiceWords(WorkflowNamed.StepsRefused(asId), 1);
    }

    /// <summary>What <c>new --from current:&lt;repository&gt;</c> is told: the conversion is the one door for what a place follows under Current.</summary>
    public const string CurrentRetired =
        "retired: a workflow is no longer copied from a repository's Current by hand. `daoris convert` converts what a place follows "
        + "under Current into a saved workflow, as you press it. Nothing was written.";

    /// <summary><c>&lt;id&gt;</c> or <c>&lt;id&gt;@&lt;version&gt;</c>.</summary>
    public static (string Id, int? Version) Named(string? token)
    {
        if (token is null || token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords(DriverWorkflowsCommand.Usage);
        var at = token.LastIndexOf('@');
        if (at == -1) return (token, null);
        var version = token[(at + 1)..];
        if (!(version.Length is >= 1 and <= 9 && version[0] is >= '1' and <= '9' && version.All(char.IsAsciiDigit)))
        {
            throw new ChoiceWords($"`{token}` names no version — `<id>@<version>`, such as `docs-to-pr@2`.");
        }

        return (token[..at], int.Parse(version, CultureInfo.InvariantCulture));
    }

    /// <summary>A saved workflow's version that reads, and its steps as the file holds them; the newest where none is named.</summary>
    public static (NamedVersion Version, JsonArray Raw) VersionOf(WorkflowFileFound found, int? wanted)
    {
        if (found.Read.Problem is not null) throw new ChoiceWords($"`{found.Id}` cannot be read: {found.Read.Problem}", 1);
        var versions = found.Read.Versions;
        var version = wanted is null ? versions[^1] : versions.FirstOrDefault(each => each.Version == wanted);
        if (version is null) throw new ChoiceWords($"`{found.Id}` keeps no v{wanted} — it keeps {string.Join(", ", versions.Select(each => $"v{each.Version}"))}.");
        if (version.Graph is not null) throw new ChoiceWords(WorkflowWords.NoSteps(found.Id, version), 1);
        if (version.Steps is null) throw new ChoiceWords($"`{found.Id}` v{version.Version} cannot be read here: {version.Unrun}", 1);
        var index = versions.ToList().IndexOf(version);
        return (version, found.File!["versions"]![index]!["steps"]!.DeepClone().AsArray());
    }

    /// <summary>The base a change is made against: the newest version, which a named <paramref name="wanted"/> must be.</summary>
    /// <exception cref="ChoiceWords">Naming the newest version where the caller read an older one: read it again.</exception>
    public static (NamedVersion Version, JsonArray? Raw) BaseOf(WorkflowFileFound found, string? wanted)
    {
        if (found.Read.Problem is not null) throw new ChoiceWords($"`{found.Id}` cannot be read: {found.Read.Problem}", 1);
        var newest = found.Read.Versions[^1];
        if (wanted is not null && wanted != newest.Version.ToString(CultureInfo.InvariantCulture))
        {
            throw new ChoiceWords($"`{found.Id}` is at v{newest.Version} now, not v{wanted}: read it again (`daoris workflow "
                + $"show {found.Id}`) and make the change against v{newest.Version}.", 1);
        }

        return (newest, newest.Steps is null ? null : VersionOf(found, newest.Version).Raw);
    }

    /// <summary>A new graph version, judged whole as the driver judges it, against the base: planned, read back and compared; nothing is written.</summary>
    /// <param name="from">The default it copies, kept in the version; null for a graph from anywhere else.</param>
    /// <exception cref="ChoiceWords">Where the plan refuses.</exception>
    public static WorkflowPlanned PlanGraph(
        string home, WorkflowFileFound? found, string id, NamedVersion? @base, string? name, string door, string at, JsonNode? graph, WorkflowFrom? from = null)
    {
        var plan = WorkflowNamed.PlanAdd(found?.File, new WorkflowVersionAdded(id, name, door, at, null, graph?.DeepClone()),
            found is null ? [] : WorkflowRunBindings.KeptVersions(home, id).ToList(), from);
        if (plan.Problem is not null) throw new ChoiceWords(plan.Problem, 1);
        var versions = plan.Result!["versions"]!.AsArray();
        var next = WorkflowNamed.ReadVersion(WorkflowNamed.ElementOf(versions[^1]!), plan.Version!.Value, [],
            found?.Read.Pipeline ?? WorkflowPipelines.Development, onSave: true);
        if (next.Graph is { } read && RoleProblems(home, read) is [var role, ..]) throw new ChoiceWords($"v{plan.Version}: {role.Words}", 1);
        return new(plan, next, null, @base?.Graph is not null && @base.Digest == next.Digest);
    }

    /// <summary>The code a step's role refused at save is said by, beside the graph's and the wires' own.</summary>
    public const string RoleRefused = "role";

    /// <summary>
    /// Everything a save refuses in <paramref name="graph"/>, each at the step or wire it names: the graph rules' refusals as a save judges
    /// them, and where those hold, each step whose role cannot take it (<see cref="RoleProblems"/>). Empty where the graph would be saved.
    /// </summary>
    public static IReadOnlyList<WireProblem> GraphProblems(string home, JsonNode graph)
    {
        var (read, problems) = NodeGraphs.Read(graph.ToJsonString(), onSave: true);
        return read is null ? problems : RoleProblems(home, read);
    }

    /// <summary>
    /// Each agent step whose role cannot take it: the role named, by the home's
    /// catalogue, for the step's work at the role's own scope. Where it works is the bind's to judge, since a version is chosen for its places
    /// after it is saved. The catalogue is read only where a step names a role.
    /// </summary>
    public static IReadOnlyList<WireProblem> RoleProblems(string home, NodeGraph graph)
    {
        var problems = new List<WireProblem>();
        RoleCatalog? catalogue = null;
        foreach (var node in graph.Nodes)
        {
            if (NodeKit.Of(node.Kind)?.RoleTakes is not [var work, ..]) continue;
            if (!node.Options.TryGetValue(NodeOptionFields.Role, out var written) || NodeOptions.Value(written) is null or "") continue;
            if (NodeOptions.Value(written) is not string role)
            {
                problems.Add(new(RoleRefused, null, node.Id, $"the step `{node.Id}` names its role by something other than a role's id."));
                continue;
            }

            try
            {
                catalogue ??= RoleCatalog.ReadHome(home);
            }
            catch (DriverException unread)
            {
                problems.Add(new(RoleRefused, null, node.Id, $"the step `{node.Id}` names the role `{role}`, and {unread.Message}"));
                continue;
            }

            var scope = catalogue.Slots.FirstOrDefault(slot => slot.Id == role)?.Role?.Scope;
            var place = scope?.Kind switch
            {
                RoleScope.Repository => new RolePlace(Repository: scope.Id),
                RoleScope.Workspace => new RolePlace(Workspace: scope.Id),
                _ => new RolePlace(),
            };
            if (catalogue.Named(role, work, place).Held is { } held)
            {
                problems.Add(new(RoleRefused, null, node.Id, $"the step `{node.Id}` names the role `{role}`, which cannot take it: {held}"));
            }
        }

        return problems;
    }

    /// <summary>
    /// The window's save: a new version under a fresh id, or appended to a saved workflow against the version the caller read.
    /// An append first says every place that chooses the workflow, which a new start binds the newest of (<c>WorkflowRunBindings.Plan</c>), and
    /// goes on only when the caller authorized exactly those; its reach of a graph that integrates is judged as a choice's is.
    /// </summary>
    /// <param name="Source">Exactly one of <c>from</c> (a new id), <c>steps</c> or <c>graph</c>.</param>
    /// <param name="updates">The shipped default an appended version updates its copy to: the version keeps that default's <c>from</c> stamp.</param>
    public static async Task<WorkflowSaved> SaveAsync(
        DriverConfig choices, string home, RegistryRead registry, string id, string? from, JsonNode? steps, JsonNode? graph,
        bool opinion, bool look, string? name, string? @base, IReadOnlyCollection<string>? authorized, bool plan, string at, CancellationToken ct,
        string? updates = null)
    {
        WorkflowSaved Refuse(string code, string message, IReadOnlyList<WorkflowScope>? scopes = null, IReadOnlyList<WorkflowReach>? reach = null, bool appended = false) =>
            new(id, false, code, message, null, false, appended, scopes ?? [], reach ?? []);

        if (WorkflowNamed.IdProblem(id) is { } idProblem) return Refuse("refused", idProblem);
        if (steps is not null) return Refuse(WorkflowNamed.StepsRefusedCode, WorkflowNamed.StepsRefused(id));
        var given = (from is null ? 0 : 1) + (steps is null ? 0 : 1) + (graph is null ? 0 : 1);
        if (given != 1) return Refuse("refused", "a save names where the version comes from — one of `from`, `steps` or `graph`.");
        try
        {
            var found = WorkflowPorts.Store(home).Load(id);
            if (found is { File: null }) return Refuse("unread", $"`{id}` cannot be read: {found.Read.Problem} Nothing was written.");
            if (found is null)
            {
                if (@base is not null) return Refuse("stale", $"there is no workflow `{id}` to add a version to, so `base` names none. Nothing was written.");
                WorkflowPlanned planned;
                string made;
                if (from is not null)
                {
                    var source = await SourceAsync(choices, home, registry, from, opinion, look, ct, id).ConfigureAwait(false);
                    planned = PlanGraph(home, null, id, null, name ?? source.Name, "screen", at, source.Graph, source.From);
                    made = source.Description;
                }
                else
                {
                    planned = PlanGraph(home, null, id, null, name ?? id, "screen", at, graph);
                    made = "the page";
                }

                if (!plan) WorkflowPorts.Store(home).Write(id, planned.Plan.Result!);
                return new(id, true, null, $"`{id}` v1, a new workflow, from {made}.", planned.Plan.Version, !plan, false, [], []);
            }

            if (from is not null) return Refuse("refused", $"`{id}` is a workflow here already: a save with `from` makes a new one. Add a version with `steps` or `graph`.");
            if (found.Read.Problem is not null) return Refuse("refused", $"`{id}` cannot be read: {found.Read.Problem}");
            var newest = found.Read.Versions[^1];
            if (@base != newest.Version.ToString(CultureInfo.InvariantCulture))
            {
                return Refuse("stale", $"`{id}` is at v{newest.Version} now, {(@base is null ? "and a save onto it names the version it was made against" : $"not v{@base}")}: "
                    + $"read it again and make the change against v{newest.Version}. Nothing was written.");
            }

            var version = newest;
            // An update to a copy's shipped default keeps where the version came from, so the copy follows the shipped digest it is updated to.
            if (updates is not null && WorkflowDefaults.Of(updates) is null) return Refuse("refused", $"this build ships no default `{updates}` to update `{id}` from. Nothing was written.");
            var planAgainst = PlanGraph(home, found, id, version, null, "screen", at, graph, updates is null ? null : WorkflowDefaults.Of(updates)!.From);
            if (planAgainst.Same) return new(id, true, null, $"`{id}` v{version.Version} already says this: nothing was written.", version.Version, false, true, [], []);

            var scopes = ScopesOf(choices, id);
            var reach = await AppendReachAsync(choices, home, registry, id, planAgainst, scopes, ct).ConfigureAwait(false);
            const string Changed = "changed while this save was judged: read it again and make the change against its newest version. Nothing was written.";
            if (!WorkflowStore.NewestIs(WorkflowPorts.Store(home), id, newest.Version)) return Refuse("stale", $"`{id}` {Changed}");

            if (reach.Refusal is not null) return Refuse("reach", reach.Refusal, scopes, reach.Rows, appended: true);
            var keys = scopes.Select(each => each.Key).ToHashSet(StringComparer.Ordinal);
            if (keys.Count > 0 && !plan && !(authorized?.ToHashSet(StringComparer.Ordinal).SetEquals(keys) ?? false))
            {
                return Refuse("reach", $"`{id}` is chosen in {scopes.Count} place{(scopes.Count == 1 ? "" : "s")}, and each new start there binds the version saved "
                    + "now: authorize those places, then save. Nothing was written.", scopes, reach.Rows, appended: true);
            }

            if (!plan && !WorkflowStore.WriteIfNewest(WorkflowPorts.Store(home), id, newest.Version, planAgainst.Plan.Result!)) return Refuse("stale", $"`{id}` {Changed}");
            return new(id, true, null, $"`{id}` v{version.Version} →v{planAgainst.Plan.Version}.", planAgainst.Plan.Version, !plan, true, scopes, reach.Rows);
        }
        catch (ChoiceWords refused)
        {
            return Refuse("refused", refused.Message) with { Problems = graph is null ? [] : GraphProblems(home, graph) };
        }
    }

    /// <summary>
    /// Every workflow saved under the home, as <c>driver workflow list</c> reads them, and the presets built in. Reads only.
    /// </summary>
    public static (IReadOnlyList<WorkflowListed> Saved, IReadOnlyList<WorkflowPresetListed> Presets) Listing(string home, DriverConfig choices)
    {
        var saved = new List<WorkflowListed>();
        foreach (var found in WorkflowStore.List(home))
        {
            var read = found.Read;
            if (read.Problem is not null)
            {
                saved.Add(new(found.Id, null, read.Problem, [], null, null, null, null, ScopesOf(choices, found.Id)));
                continue;
            }

            var newest = read.Versions[^1];
            var line = newest.Steps is not null ? WorkflowPresets.Line(newest.Steps)
                : newest.Graph is not null ? WorkflowPresets.GraphLine(RawOf(found, read.Versions.Count - 1).AsObject()) : null;
            saved.Add(new(found.Id, read.Name, newest.Unrun, [.. read.Versions.Select(each => each.Version)],
                newest.Steps is not null ? 1 : newest.Graph is not null ? WorkflowNamed.GraphSchema : null, newest.Digest, line, read.Pipeline,
                ScopesOf(choices, found.Id)));
        }

        var presets = WorkflowPresets.GraphTable.Select(preset => new WorkflowPresetListed(preset.Id, preset.Name, true, WorkflowPresets.GraphLine(preset.Graph()))
            {
                Body = preset.Graph(), Default = WorkflowDefaults.Of(preset.Id),
            })
            .ToArray();
        return (saved, presets);
    }

    private static JsonNode RawOf(WorkflowFileFound found, int index)
    {
        var version = found.File!["versions"]![index]!;
        return (version["graph"] ?? version["steps"])!;
    }

    /// <summary>One version of a saved workflow as the file keeps it (the newest where none is named), with its line and, for a graph, its problems.</summary>
    /// <exception cref="ChoiceWords">Naming why it cannot be shown.</exception>
    public static WorkflowShown Show(string home, string id, int? wanted)
    {
        if (!WorkflowNamed.IsId(id)) throw new ChoiceWords(WorkflowNamed.IdProblem(id)!, 1);
        var found = WorkflowStore.Load(home, id) ?? throw new ChoiceWords($"no workflow `{id}` here.");
        if (found.Read.Problem is not null) throw new ChoiceWords($"`{id}` cannot be read: {found.Read.Problem}", 1);
        var versions = found.Read.Versions;
        var index = wanted is null ? versions.Count - 1 : versions.ToList().FindIndex(each => each.Version == wanted);
        if (index < 0) throw new ChoiceWords($"`{id}` keeps no v{wanted} — it keeps {string.Join(", ", versions.Select(each => $"v{each.Version}"))}.");
        var shown = versions[index];
        if (shown.Unrun is { } unrun) throw new ChoiceWords($"`{id}` v{shown.Version} cannot be read here: {unrun}", 1);
        var body = RawOf(found, index).DeepClone();
        var line = shown.Steps is not null ? WorkflowPresets.Line(shown.Steps) : WorkflowPresets.GraphLine(body.AsObject());
        return new(id, found.Read.Name!, shown.Version, index == versions.Count - 1, [.. versions.Select(each => each.Version)], shown.At, shown.Door,
            shown.Digest, shown.Steps is not null ? 1 : WorkflowNamed.GraphSchema, line, body,
            shown.Graph is null ? [] : NodeGraphs.Problems(shown.Graph, onSave: false));
    }

    /// <summary>Whether a checkout is the repository named, in any case.</summary>
    public static bool InScope(string? repository, string name) => string.Equals(repository, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>A level's choice by its name in any case, or null.</summary>
    public static WorkflowChoice? ChoiceAt(IReadOnlyDictionary<string, WorkflowChoice> map, string name) =>
        map.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>A choice as the file writes it, to tell an edit that changes nothing.</summary>
    public static string Shape(WorkflowChoice? choice, bool workspace)
    {
        if (choice is null) return "";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WorkflowSelection.Write(writer, choice, workspace);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
