using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One repository a choice reaches (the integration design §17.7): why its declarations cannot carry the version, and whether its line integrates with no gates.</summary>
public sealed record WorkflowReach(string Repository, IReadOnlyList<string> Cannots, bool NoGates)
{
    /// <summary>The saved workflow whose graph reaches the repository, where a place's reach names it.</summary>
    public string? Workflow { get; init; }

    /// <summary>The sentence the door says of it, or null where it reaches nothing worth saying.</summary>
    public string? Said => Cannots.Count > 0 ? $"`{Repository}`: cannot start — {string.Join(" ", Cannots)}"
        : NoGates ? IntegrateCarry.NoGatesSaid(Repository) : null;
}

/// <summary>What a choice's reach is: the lines the terminal says, each repository's row, and the refusal where one cannot carry the version.</summary>
public sealed record WorkflowReached(IReadOnlyList<string> Lines, IReadOnlyList<WorkflowReach> Rows, string? Refusal);

/// <summary>A choice made (or found already made): the edit, its words, and the repositories it reaches.</summary>
/// <param name="Refusal">Why nothing was written, in the twins' words; null where the edit stands.</param>
/// <param name="Changed">Whether the edit changes what is kept; false is "nothing was written".</param>
public sealed record WorkflowUsed(
    WorkflowChoiceEdit? Edit, bool Changed, IReadOnlyList<string> Lines, IReadOnlyList<WorkflowReach> Reach, string? Refusal);

/// <summary>A kind declared or dropped: the edit and its words, or the refusal.</summary>
public sealed record WorkflowKindSet(WorkflowChoiceEdit? Edit, IReadOnlyList<string> Lines, string? Refusal);

/// <summary>One place a saved workflow is chosen for, which appending a version to it reaches at the next start (<c>WorkflowRunBindings.Plan</c> binds the newest).</summary>
/// <param name="Level"><c>repository</c> or <c>workspace</c>.</param>
/// <param name="Role"><c>default</c>, <c>kind</c> or <c>landed</c>.</param>
public sealed record WorkflowScope(string Level, string Name, string Role, string? Kind)
{
    /// <summary>The scope as the person authorizes it.</summary>
    public string Key => Kind is null ? $"{Level}:{Name}:{Role}" : $"{Level}:{Name}:{Role}:{Kind}";
}

/// <summary>Where a new version comes from, resolved: steps or a graph, its name, and how the terminal says its source.</summary>
public sealed record WorkflowVersionSource(
    string Description, string Name, JsonArray? Steps, JsonObject? Graph, WorkflowGraphPreset? GraphPreset, CurrentWorkflow? Drawn);

/// <summary>A version planned against its base: the file after it, the version read back, and whether it says what the base does.</summary>
public sealed record WorkflowPlanned(WorkflowAddPlan Plan, NamedVersion? Next, IReadOnlyList<NamedStep>? NextSteps, bool Same);

/// <summary>What a save did, or why it did not.</summary>
public sealed record WorkflowSaved(
    string Id, bool Done, string? Refused, string? Message, int? Version, bool Written, bool Appended,
    IReadOnlyList<WorkflowScope> Scopes, IReadOnlyList<WorkflowReach> Reach);

/// <summary>
/// The typed operations behind <c>daoris-driver workflow new|edit|use|kind</c> (D194 point 4; AGENTSPLACE1c): the terminal's verb and the
/// window's <c>WORKFLOW_SAVE</c>, <c>WORKFLOW_USE</c> and <c>WORKFLOW_KIND</c> call these and differ only in how they say the result, so a
/// refusal, a reach or a version is judged once. Nothing here writes <c>driver.json</c>; a caller writes the edit it is handed.
/// </summary>
public static class WorkflowOperations
{
    /// <summary>The choices' registry, read where it is needed.</summary>
    public delegate Task<ChoicesRegistry> RegistryRead(CancellationToken ct);

    /// <summary>
    /// A named workflow is chosen only where it is saved and its newest version reads (design §3.9). Returns the file's mark, which goes with the
    /// edit so a verification pipeline is named by the landed route alone.
    /// </summary>
    /// <exception cref="ChoiceWords">Naming why it cannot be chosen.</exception>
    public static string? CheckChosen(string home, string? chosen)
    {
        if (chosen is null || chosen == WorkflowSelection.Current || !WorkflowNamed.IsId(chosen)) return null;
        var found = WorkflowPorts.Store(home).Load(chosen) ?? throw new ChoiceWords(
            $"no workflow `{chosen}` is saved here — `daoris driver workflow list` names them, and `new` names one.");
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

    /// <summary>
    /// <c>use</c> without <c>--landed</c> (design §4.7): which workflow a repository's or a workspace's work follows, or a kind's. The reach of a
    /// graph that integrates (INTEGRATE1r part r2; §17.7) is judged first, then the edit; the refusal is the words the terminal prints.
    /// </summary>
    public static async Task<WorkflowUsed> UseAsync(
        DriverConfig choices, string home, RegistryRead registry, string? chosen, string? repository, string? workspace, string? kind, CancellationToken ct)
    {
        var pipeline = CheckChosen(home, chosen);
        var given = new WorkflowUse(chosen, repository, workspace, kind) { Pipeline = pipeline };
        var reach = await ReachAsync(choices, home, registry, chosen, repository, workspace, ct).ConfigureAwait(false);
        if (reach.Refusal is not null) return new WorkflowUsed(null, false, [], reach.Rows, reach.Refusal);

        string? unchecked_ = null;
        WorkflowUse edit;
        if (workspace is not null)
        {
            edit = given with { Repository = null };
        }
        else if (kind is null || chosen is null)
        {
            edit = given;
        }
        else
        {
            var read = await registry(ct).ConfigureAwait(false);
            if (read.Checkouts is { } checkouts)
            {
                edit = given with { InKnown = true, In = checkouts.FirstOrDefault(each => InScope(each.Repository, repository!)).Workspace };
            }
            else
            {
                edit = given;
                unchecked_ = read.Unread;
            }
        }

        DriverConfig edited;
        try
        {
            edited = WorkflowSelection.Apply(choices, edit);
        }
        catch (DriverException refused)
        {
            var declare = kind is not null && refused.Message.Contains("declares no kind", StringComparison.Ordinal)
                ? $" `daoris driver workflow kind <workspace> {kind} --label \"…\"` declares one." : "";
            return new WorkflowUsed(null, false, [], reach.Rows, $"{refused.Message}{declare} Nothing was written.");
        }

        var lines = new List<string>();
        var name = (repository ?? workspace)!.Trim();
        var whose = repository is not null ? $"`{repository}`" : $"the workspace `{workspace}`";
        var after = ChoiceAt(repository is not null ? edited.Workflows : edited.WorkspaceWorkflows, name);
        var was = ChoiceAt(repository is not null ? choices.Workflows : choices.WorkspaceWorkflows, name);
        var forWhat = kind is null ? " as its default" : $" for kind `{kind}`";
        if (Shape(after, repository is null) == Shape(was, repository is null))
        {
            lines.Add($"daoris: {whose} {(chosen is null ? "chose no workflow" : $"chooses `{chosen}` already")}{forWhat}: nothing was written.");
            return new WorkflowUsed(edit, false, lines, reach.Rows, null);
        }

        if (chosen is null) lines.Add($"daoris: {whose} no longer chooses a workflow{forWhat}.");
        else if (chosen == WorkflowSelection.Current) lines.Add($"daoris: {whose} now chooses Current{forWhat}: its rules as they stand, read at each gate.");
        else
        {
            var found = WorkflowPorts.Store(home).Load(chosen)!;
            var newest = found.Read.Versions[^1];
            lines.Add($"daoris: {whose} now chooses `{chosen}`{forWhat} — {found.Read.Name}, v{newest.Version} now: {(newest.Graph is { } graph
                ? $"a graph (schema {WorkflowNamed.GraphSchema}) of {graph.Nodes.Count} nodes and {graph.Wires.Count} wires, digest {newest.Digest}."
                : WorkflowPresets.Line(newest.Steps!))}");
        }

        if (repository is not null)
        {
            lines.Add(after is null
                ? $"  `{repository}` names no workflow now, so its workspace's choice reaches it again."
                : !after.NamesDevelopment
                    // A landed route alone replaces nothing (FABRIC-PIPELINE1a).
                    ? $"  `{repository}` names no workflow for its work now, only its landed route, so its workspace's choice reaches it again."
                    : $"  This replaces the workspace's for every kind: `{repository}` follows its own kinds and its own default, and Current "
                        + "where it names none.");
        }

        if (unchecked_ is not null)
        {
            lines.Add($"  The registry was not read ({unchecked_}), so kind `{kind}` was not checked against the kinds `{repository}`'s workspace declares.");
        }

        if (chosen is not null)
        {
            lines.AddRange(reach.Lines);
            lines.Add("  Each new piece of work binds what is chosen at its first start; work already started keeps what it bound.");
        }

        return new WorkflowUsed(edit, true, lines, reach.Rows, null);
    }

    /// <summary>
    /// The repositories a choice of a graph that integrates its landing reaches (INTEGRATE1r part r2; the integration design §17.7, F42): a
    /// repository's own, or a workspace's from the registry, the ones whose own choice does not replace it. It refuses, before anything is
    /// written, where one cannot carry the version, each case its own sentence; and says, for each, <i>integrates with no gates</i> where its
    /// rule's <c>integrate.gates</c> is absent or <c>none</c>. Nothing where the version is not a graph that integrates or pushes.
    /// </summary>
    public static async Task<WorkflowReached> ReachAsync(
        DriverConfig choices, string home, RegistryRead registry, string? chosen, string? repository, string? workspace, CancellationToken ct)
    {
        if (chosen is null || chosen == WorkflowSelection.Current || !WorkflowNamed.IsId(chosen)) return new([], [], null);
        if (WorkflowPorts.Store(home).Load(chosen)?.Read.Versions is not [.., { Graph: { } graph } newest] || !IntegrateChoice.Matters(graph)) return new([], [], null);

        var read = await registry(ct).ConfigureAwait(false);
        var said = new List<string>();
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> reached;
        if (read.Checkouts is not { } checkouts)
        {
            if (repository is null)
            {
                said.Add($"  The registry was not read ({read.Unread}), so the repositories the workspace `{workspace}` reaches were not checked "
                    + "for what the version integrates.");
                return new(said, [], null);
            }

            reached = [(repository, null, null)];
        }
        else if (repository is not null)
        {
            var own = checkouts.FirstOrDefault(each => InScope(each.Repository, repository));
            reached = [own.Repository is null ? (repository, null, null) : own];
        }
        else
        {
            reached = InWorkspace(choices, checkouts, workspace!, except: null);
        }

        return await RowsAsync(choices, chosen, newest.Version, graph, reached, said, ct).ConfigureAwait(false);
    }

    // A repository whose own choice names a workflow replaces its workspace's whole (D157 point 10), so the workspace's does not reach it.
    private static List<(string Repository, string? Workspace, string? Root)> InWorkspace(
        DriverConfig choices, IReadOnlyList<(string Repository, string? Workspace, string? Root)> checkouts, string workspace, string? except) =>
    [
        .. checkouts
            .Where(each => each.Repository is not null && string.Equals(RemoteTarget.Workspace(each.Workspace), workspace.Trim(), StringComparison.OrdinalIgnoreCase)
                && (ChoiceAt(choices.Workflows, each.Repository) is not { NamesDevelopment: true }
                    || (except is not null && ChoiceAt(choices.Workflows, each.Repository)?.Default == except)))
            .DistinctBy(each => each.Repository, StringComparer.OrdinalIgnoreCase)
            .OrderBy(each => each.Repository, StringComparer.OrdinalIgnoreCase),
    ];

    private static async Task<WorkflowReached> RowsAsync(
        DriverConfig choices, string id, int version, NodeGraph graph,
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> reached, List<string> said, CancellationToken ct)
    {
        var answers = await IntegrateChoice.ReachAsync(choices, id, version, graph, reached, ct).ConfigureAwait(false);
        var rows = answers.Select(each => new WorkflowReach(each.Repository, each.Cannots, each.NoGates)).ToList();
        if (answers.Where(each => each.Cannots.Count > 0).ToList() is { Count: > 0 } refused)
        {
            return new(said, rows, IntegrateChoice.Refusal(id, version, refused));
        }

        said.AddRange(answers.Where(each => each.NoGates).Select(each => $"  {IntegrateCarry.NoGatesSaid(each.Repository)}"));
        return new(said, rows, null);
    }

    /// <summary>
    /// <c>kind &lt;workspace&gt; &lt;kind&gt; --label "…" [--paths …]</c> and <c>--drop</c> (design §4.2, §4.7): a kind of task a workspace
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
                : $"  It maps to no workflow there: `daoris driver workflow use <id> --workspace {workspace} --kind {id}` maps one, and "
                    + "`--repository <name>` maps it for one repository.");
        }

        return new WorkflowKindSet(edit, lines, null);
    }

    /// <summary>
    /// What the choices as they stand do to a place (§17.7): for each repository of it, every graph its default or a kind of its workspace chooses
    /// that integrates or pushes, answered as a choice of it is, so a combination that cannot start is named before work stalls on it.
    /// </summary>
    public static async Task<IReadOnlyList<WorkflowReach>> PlaceReachAsync(
        DriverConfig choices, string home, RegistryRead registry, string? repository, string? workspace, CancellationToken ct)
    {
        var read = await registry(ct).ConfigureAwait(false);
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> places;
        if (read.Checkouts is not { } checkouts) places = repository is null ? [] : [(repository, null, null)];
        else if (repository is not null)
        {
            var own = checkouts.FirstOrDefault(each => InScope(each.Repository, repository));
            places = [own.Repository is null ? (repository, null, null) : own];
        }
        else places = InWorkspace(choices, checkouts, workspace!, except: null);

        var rows = new List<WorkflowReach>();
        foreach (var place in places)
        {
            var kinds = ChoiceAt(choices.WorkspaceWorkflows, RemoteTarget.Workspace(place.Workspace))?.Kinds.Select(pair => pair.Key) ?? [];
            var chosen = new[] { (WorkflowTaskChoice?)null }.Concat(kinds.Select(kind => new WorkflowTaskChoice(kind, null)))
                .Select(task => WorkflowSelection.Resolve(choices, place.Repository, place.Workspace, task).Workflow)
                .Where(id => id != WorkflowSelection.Current && WorkflowNamed.IsId(id)).Distinct(StringComparer.Ordinal);
            foreach (var id in chosen)
            {
                if (WorkflowPorts.Store(home).Load(id)?.Read.Versions is not [.., { Graph: { } graph } newest] || !IntegrateChoice.Matters(graph)) continue;
                var answers = await IntegrateChoice.ReachAsync(choices, id, newest.Version, graph, [place], ct).ConfigureAwait(false);
                rows.AddRange(answers.Select(each => new WorkflowReach(each.Repository, each.Cannots, each.NoGates) { Workflow = id }));
            }
        }

        return rows;
    }

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
        return scopes;
    }

    /// <summary>
    /// The terminal's <c>new --from</c> source, resolved: a preset, a graph preset, <c>current:&lt;repository&gt;</c> or a saved workflow's version,
    /// with the second opinion and your look added by the presets' own rule.
    /// </summary>
    /// <exception cref="ChoiceWords">Naming why the source cannot be used.</exception>
    public static async Task<WorkflowVersionSource> SourceAsync(
        DriverConfig choices, string home, RegistryRead registry, string from, bool opinion, bool look, CancellationToken ct)
    {
        if (from.StartsWith("current:", StringComparison.Ordinal))
        {
            var repository = from["current:".Length..];
            if (repository.Length == 0) throw new ChoiceWords($"`current:` names a repository, such as `current:web-app` — {DriverWorkflowsCommand.Usage}");
            var drawn = (await CurrentOfAsync(choices, home, registry, repository, ct).ConfigureAwait(false));
            // LANDNODE1h: Current's landing is written out as a `land` node, so what is saved is a graph.
            return new($"`{repository}`'s Current (version {drawn.Version})", $"{repository}'s Current",
                null, WorkflowWords.GraphOfCurrent(drawn, opinion, look), null, drawn);
        }

        if (!from.Contains('@', StringComparison.Ordinal) && WorkflowPresets.GraphOf(from) is { } graphPreset)
        {
            var name = $"{graphPreset.Name}{(opinion ? " + second opinion" : "")}{(look ? " + your look" : "")}";
            return new($"the preset `{graphPreset.Id}`", name, null, WorkflowPresets.WithGraphGates(graphPreset.Graph(), opinion, look), graphPreset, null);
        }

        if (!from.Contains('@', StringComparison.Ordinal) && WorkflowPresets.Of(from) is { } preset)
        {
            // Short enough that every preset's name with both stays within a name's 60 characters.
            var name = $"{preset.Name}{(opinion ? " + second opinion" : "")}{(look ? " + your look" : "")}";
            return new($"the preset `{preset.Id}`", name, WorkflowPresets.WithGates(preset.Steps(), opinion, look), null, null, null);
        }

        var (sourceId, wanted) = Named(from);
        var found = WorkflowNamed.IsId(sourceId) ? WorkflowPorts.Store(home).Load(sourceId) : throw new ChoiceWords(WorkflowNamed.IdProblem(sourceId)!, 1);
        if (found is null)
        {
            throw new ChoiceWords($"`{from}` is no preset, no saved workflow and no `current:<repository>` — the presets are "
                + $"{string.Join(", ", WorkflowPresets.Table.Select(each => $"`{each.Id}`").Concat(WorkflowPresets.GraphTable.Select(each => $"`{each.Id}`")))}.");
        }

        var (shown, raw) = VersionOf(found, wanted);
        return new($"`{sourceId}` v{shown.Version}", found.Read.Name!, WorkflowPresets.WithGates(raw, opinion, look), null, null, null);
    }

    /// <summary>A repository's Current, drawn as <c>review</c> reads it, for a workflow made from it.</summary>
    public static async Task<CurrentWorkflow> CurrentOfAsync(DriverConfig choices, string home, RegistryRead registry, string name, CancellationToken ct)
    {
        var read = await registry(ct).ConfigureAwait(false);
        string? inWorkspace = null;
        if (read.Checkouts is not { } checkouts)
        {
            if (choices.WorkspaceLandings.Count > 0 || choices.WorkspaceReviews.Count > 0 || choices.WorkspaceOpinions.Count > 0
                || choices.WorkspaceWorkflows.Count > 0)
            {
                throw new ChoiceWords($"cannot say which workspace's rules reach `{name}`: the registry could not be read — "
                    + $"{read.Unread} — and this file sets rules for a workspace. Nothing was drawn. `--workspace <name>` draws a "
                    + "workspace's Current from this file alone.");
            }
        }
        else
        {
            inWorkspace = checkouts.FirstOrDefault(each => InScope(each.Repository, name)).Workspace;
        }

        return WorkflowCurrent.Derive(choices, name, inWorkspace, WorkflowCurrent.PluginsOf(PluginCatalog.Load(home, AdapterSet.Built().Names)));
    }

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

    /// <summary>The base a change is made against: the newest version, which a named <paramref name="wanted"/> must be (design §8.1).</summary>
    /// <exception cref="ChoiceWords">Naming the newest version where the caller read an older one: read it again.</exception>
    public static (NamedVersion Version, JsonArray? Raw) BaseOf(WorkflowFileFound found, string? wanted)
    {
        if (found.Read.Problem is not null) throw new ChoiceWords($"`{found.Id}` cannot be read: {found.Read.Problem}", 1);
        var newest = found.Read.Versions[^1];
        if (wanted is not null && wanted != newest.Version.ToString(CultureInfo.InvariantCulture))
        {
            throw new ChoiceWords($"`{found.Id}` is at v{newest.Version} now, not v{wanted}: read it again (`daoris driver workflow "
                + $"show {found.Id}`) and make the change against v{newest.Version}.", 1);
        }

        return (newest, newest.Steps is null ? null : VersionOf(found, newest.Version).Raw);
    }

    /// <summary>A new version of steps, judged against the base: planned, read back and compared; nothing is written.</summary>
    /// <exception cref="ChoiceWords">Where the plan refuses.</exception>
    public static WorkflowPlanned PlanSteps(string home, WorkflowFileFound? found, string id, NamedVersion? @base, string? name, string door, string at, JsonNode? steps)
    {
        var plan = WorkflowNamed.PlanAdd(found?.File, new WorkflowVersionAdded(id, name, door, at, steps),
            found is null ? [] : WorkflowRunBindings.KeptVersions(home, id).ToList());
        if (plan.Problem is not null) throw new ChoiceWords(plan.Problem, 1);
        // A new workflow's steps are read as the plan saved them; a version added to one, as given, after the versions that read.
        var read = found is null ? plan.Result!["versions"]![0]!["steps"] : steps;
        var next = WorkflowNamed.ReadSteps(read is null ? null : WorkflowNamed.ElementOf(read), plan.Version!.Value,
            found is null ? null : [.. found.Read.Versions.Where(each => each.Steps is not null)]).Steps;
        return new(plan, null, next, @base?.Steps is not null && next is not null && @base.Digest == WorkflowNamed.Digest(next));
    }

    /// <summary>A new graph version, judged whole as the driver judges it, against the base: planned, read back and compared; nothing is written.</summary>
    /// <exception cref="ChoiceWords">Where the plan refuses.</exception>
    public static WorkflowPlanned PlanGraph(string home, WorkflowFileFound? found, string id, NamedVersion? @base, string? name, string door, string at, JsonNode? graph)
    {
        var plan = WorkflowNamed.PlanAdd(found?.File, new WorkflowVersionAdded(id, name, door, at, null, graph?.DeepClone()),
            found is null ? [] : WorkflowRunBindings.KeptVersions(home, id).ToList());
        if (plan.Problem is not null) throw new ChoiceWords(plan.Problem, 1);
        var versions = plan.Result!["versions"]!.AsArray();
        var next = WorkflowNamed.ReadVersion(WorkflowNamed.ElementOf(versions[^1]!), plan.Version!.Value, [],
            found?.Read.Pipeline ?? WorkflowPipelines.Development, onSave: true);
        return new(plan, next, null, @base?.Graph is not null && @base.Digest == next.Digest);
    }

    /// <summary>
    /// The window's save (AGENTSPLACE1c): a new version under a fresh id, or appended to a saved workflow against the version the caller read.
    /// An append first says every place that chooses the workflow, which a new start binds the newest of (<c>WorkflowRunBindings.Plan</c>), and
    /// goes on only when the caller authorized exactly those; its reach of a graph that integrates is judged as a choice's is.
    /// </summary>
    /// <param name="Source">Exactly one of <c>from</c> (a new id), <c>steps</c> or <c>graph</c>.</param>
    public static async Task<WorkflowSaved> SaveAsync(
        DriverConfig choices, string home, RegistryRead registry, string id, string? from, JsonNode? steps, JsonNode? graph,
        bool opinion, bool look, string? name, string? @base, IReadOnlyCollection<string>? authorized, bool plan, string at, CancellationToken ct)
    {
        WorkflowSaved Refuse(string code, string message, IReadOnlyList<WorkflowScope>? scopes = null, IReadOnlyList<WorkflowReach>? reach = null, bool appended = false) =>
            new(id, false, code, message, null, false, appended, scopes ?? [], reach ?? []);

        if (WorkflowNamed.IdProblem(id) is { } idProblem) return Refuse("refused", idProblem);
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
                    var source = await SourceAsync(choices, home, registry, from, opinion, look, ct).ConfigureAwait(false);
                    planned = source.Graph is not null
                        ? PlanGraph(home, null, id, null, name ?? source.Name, "screen", at, source.Graph)
                        : PlanSteps(home, null, id, null, name ?? source.Name, "screen", at, source.Steps);
                    made = source.Description;
                }
                else
                {
                    var nameless = name ?? id;
                    planned = graph is not null
                        ? PlanGraph(home, null, id, null, nameless, "screen", at, graph)
                        : PlanSteps(home, null, id, null, nameless, "screen", at, steps);
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
            var planAgainst = graph is not null
                ? PlanGraph(home, found, id, version, null, "screen", at, graph)
                : PlanSteps(home, found, id, version, null, "screen", at, steps);
            if (planAgainst.Same) return new(id, true, null, $"`{id}` v{version.Version} already says this: nothing was written.", version.Version, false, true, [], []);

            var scopes = ScopesOf(choices, id);
            var reach = await AppendReachAsync(choices, home, registry, id, planAgainst, scopes, ct).ConfigureAwait(false);
            if (reach.Refusal is not null) return Refuse("reach", reach.Refusal, scopes, reach.Rows, appended: true);
            var keys = scopes.Select(each => each.Key).ToHashSet(StringComparer.Ordinal);
            if (keys.Count > 0 && !plan && !(authorized?.ToHashSet(StringComparer.Ordinal).SetEquals(keys) ?? false))
            {
                return Refuse("reach", $"`{id}` is chosen in {scopes.Count} place{(scopes.Count == 1 ? "" : "s")}, and each new start there binds the version saved "
                    + "now: authorize those places, then save. Nothing was written.", scopes, reach.Rows, appended: true);
            }

            if (!plan) WorkflowPorts.Store(home).Write(id, planAgainst.Plan.Result!);
            return new(id, true, null, $"`{id}` v{version.Version} → v{planAgainst.Plan.Version}.", planAgainst.Plan.Version, !plan, true, scopes, reach.Rows);
        }
        catch (ChoiceWords refused)
        {
            return Refuse("refused", refused.Message);
        }
    }

    /// <summary>
    /// What a graph appended to a saved workflow does to the repositories that choose it: each one it reaches, answered as a choice of it is
    /// (§17.7). Nothing where the new version is not a graph that integrates or pushes.
    /// </summary>
    private static async Task<WorkflowReached> AppendReachAsync(
        DriverConfig choices, string home, RegistryRead registry, string id, WorkflowPlanned planned, IReadOnlyList<WorkflowScope> scopes, CancellationToken ct)
    {
        if (planned.Next?.Graph is not { } graph || !IntegrateChoice.Matters(graph) || scopes.Count == 0) return new([], [], null);
        var read = await registry(ct).ConfigureAwait(false);
        var reached = new List<(string Repository, string? Workspace, string? Root)>();
        foreach (var scope in scopes.Where(each => each.Role != "landed"))
        {
            if (scope.Level == "repository")
            {
                var own = read.Checkouts?.FirstOrDefault(each => InScope(each.Repository, scope.Name)) ?? default;
                reached.Add(own.Repository is null ? (scope.Name, null, null) : own);
            }
            else if (read.Checkouts is { } checkouts)
            {
                reached.AddRange(InWorkspace(choices, checkouts, scope.Name, except: null));
            }
        }

        return await RowsAsync(choices, id, planned.Next.Version, graph,
            [.. reached.DistinctBy(each => each.Repository, StringComparer.OrdinalIgnoreCase)], [], ct).ConfigureAwait(false);
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
