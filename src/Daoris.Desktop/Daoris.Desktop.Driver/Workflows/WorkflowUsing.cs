using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using static Daoris.Driver.WorkflowOperations;

namespace Daoris.Driver;

/// <summary>Choosing a saved workflow for a place: what a use would change, which repositories it reaches, and the reach of a version appended.</summary>
internal static class WorkflowUsing
{
    /// <summary>
    /// <c>use</c> without <c>--landed</c>: which workflow a repository's or a workspace's work follows, or a kind's. The reach of a
    /// graph that integrates is judged first, then the edit; the refusal is the words the terminal prints.
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
                ? $" `daoris workflow kind <workspace> {kind} --label \"…\"` declares one." : "";
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
                    // A landed route alone replaces nothing.
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
    /// A choice read as a plan of one, nothing written: <see cref="UseAsync"/>'s judgement, each place whose
    /// workflow it changes with what lowers the person's part there, the kinds a cleared default leaves mapped, and a token over all of it that a
    /// press carries to be refused where anything moved since.
    /// </summary>
    public static async Task<WorkflowUsePlan> PlanUseAsync(
        DriverConfig choices, string home, RegistryRead registry, string? chosen, string? repository, string? workspace, string? kind, CancellationToken ct)
    {
        var used = await UseAsync(choices, home, registry, chosen, repository, workspace, kind, ct).ConfigureAwait(false);
        var places = new List<WorkflowUsePlace>();
        var seen = new List<string>();
        WorkflowUseKeeps? keeps = null;
        if (used is { Changed: true, Edit: { } edit })
        {
            var edited = WorkflowSelection.Apply(choices, edit);
            var store = WorkflowPorts.Store(home);
            var task = kind is null ? null : new WorkflowTaskChoice(kind, null);
            foreach (var place in await ReachedByAsync(choices, registry, repository, workspace, ct).ConfigureAwait(false))
            {
                if (WorkflowSelection.Resolve(choices, place.Repository, place.Workspace, task).Workflow
                    == WorkflowSelection.Resolve(edited, place.Repository, place.Workspace, task).Workflow)
                {
                    continue;
                }

                var (was, wasSteps) = WorkflowProcesses.Otherwise(store, choices, place.Repository, place.Workspace, kind);
                var (now, nowSteps) = WorkflowProcesses.Otherwise(store, edited, place.Repository, place.Workspace, kind);
                IReadOnlyList<string> lowers = wasSteps is null || nowSteps is null
                    ? [$"what {(wasSteps is null ? was : now)} asks of you could not be read here, so it is taken as more"]
                    : Involvement.Lowers(nowSteps, wasSteps);
                places.Add(new WorkflowUsePlace(place.Repository, was, now, lowers));
            }

            if (chosen is null && kind is null)
            {
                var after = ChoiceAt(repository is not null ? edited.Workflows : edited.WorkspaceWorkflows, (repository ?? workspace)!.Trim());
                var mapped = after?.Kinds.Where(pair => pair.Value.Workflow is not null)
                    .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Workflow!)).ToList();
                if (mapped is { Count: > 0 })
                {
                    var said = string.Join(", ", mapped.Select(pair => $"kind `{pair.Key}` to `{pair.Value}`"));
                    keeps = new WorkflowUseKeeps(repository is not null
                        ? $"`{repository}` still maps {said}: while a repository maps a kind of its own, its default work follows this machine's "
                            + "default, not the workspace's. Clearing a kind's mapping is that kind's own press."
                        : $"The workspace `{workspace}` still maps {said}: those kinds keep their own workflows, and only its default work changes.",
                        mapped);
                }
            }
        }

        if (chosen is not null && chosen != WorkflowSelection.Current && WorkflowNamed.IsId(chosen)
            && WorkflowPorts.Store(home).Load(chosen)?.Read.Versions is [.., var newest])
        {
            seen.Add($"{chosen}@{newest.Version}:{newest.Digest}");
        }

        var lowered = places.SelectMany(place => place.Lowers).Distinct(StringComparer.Ordinal).ToList();
        var text = string.Join('\n', new[] { used.Refusal ?? "", used.Changed ? "changed" : "same" }
            .Concat(used.Lines)
            .Concat(used.Reach.Select(row => $"{row.Repository}|{row.Workflow}|{string.Join(';', row.Cannots)}|{row.NoGates}"))
            .Concat(places.Select(place => $"{place.Repository}|{place.Was}|{place.Now}|{string.Join(';', place.Lowers)}"))
            .Append(keeps?.Said ?? "")
            .Concat(seen));
        var expected = "use-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
        return new WorkflowUsePlan(used, places, lowered, keeps, expected);
    }

    // The repositories a use reaches: the one named, or a workspace's whose own choice does not replace it; the registry unread leaves the one named.
    private static async Task<IReadOnlyList<(string Repository, string? Workspace, string? Root)>> ReachedByAsync(
        DriverConfig choices, RegistryRead registry, string? repository, string? workspace, CancellationToken ct)
    {
        var read = await registry(ct).ConfigureAwait(false);
        if (read.Checkouts is not { } checkouts) return repository is null ? [] : [(repository, null, null)];
        if (repository is null) return InWorkspace(choices, checkouts, workspace!, except: null);
        var own = checkouts.FirstOrDefault(each => InScope(each.Repository, repository));
        return [own.Repository is null ? (repository, null, null) : own];
    }

    /// <summary>
    /// The repositories a choice of a graph that integrates its landing reaches: a
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

    // A repository whose own choice names a workflow replaces its workspace's whole, so the workspace's does not reach it.
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
    /// What the choices as they stand do to a place: for each repository of it, every graph its default or a kind of its workspace chooses
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

    /// <summary>
    /// What a graph appended to a saved workflow does to the repositories that choose it: each one it reaches, answered as a choice of it is
    /// Nothing where the new version is not a graph that integrates or pushes.
    /// </summary>
    internal static async Task<WorkflowReached> AppendReachAsync(
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
            else if (scope.Level == HomeLevel)
            {
                // Every place that follows the home's default with nothing written.
                reached.AddRange((read.Checkouts ?? []).Where(each =>
                    WorkflowSelection.Resolve(choices, each.Repository, each.Workspace, null).Level == WorkflowLevels.Home));
            }
            else if (read.Checkouts is { } checkouts)
            {
                reached.AddRange(InWorkspace(choices, checkouts, scope.Name, except: null));
            }
        }

        return await RowsAsync(choices, id, planned.Next.Version, graph,
            [.. reached.DistinctBy(each => each.Repository, StringComparer.OrdinalIgnoreCase)], [], ct).ConfigureAwait(false);
    }
}
