using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.HelpProposals;
using static Daoris.Driver.HelpWorkflowRoutes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>workflow</c> proposal: a whole version of a named workflow,
/// new or an edit on its newest, for a repository or a workspace (<c>scope</c>), and optionally its choice there (<c>choose</c>). The
/// file names no door: one with no <c>base</c> is a <c>new</c> workflow, one with a base an <c>edit</c>, and <c>choose</c> is the
/// <c>use</c> door. It is judged as the editor judges a version (<see cref="WorkflowNamed"/>), and applied by saving
/// it through <see cref="WorkflowStore.Add"/> with the door <c>ask-daoris:&lt;proposal&gt;</c>, then the choice through the
/// driver's file, as <c>daoris workflow use</c> makes it.
/// </summary>
/// <remarks>
/// <para><b>Stale is refused, never applied over</b>: a new workflow takes an id no workflow here has, and an edit's
/// base is the newest version, else the card is refused with the newest number in the terminal's words. The press judges it again,
/// so a version saved between the card and the press refuses it the same way.</para>
///
/// <para><b>The card</b> leads with the person's part, then the steps by id, then what leaves the machine with no press, as
/// <see cref="WorkflowDiff"/> says them (the CLI's twin), then what each step needs first where it applies, and what it applies to.
/// Its data rides on <see cref="HelpPlan.Workflow"/>, its words the page's.</para>
///
/// <para><b>Needs first make a draft</b>: a declaration a step needs that is not declared in a repository it reaches (the
/// gate's own <see cref="WorkflowProcess.Of"/>), a landing plugin that cannot land here, and a kind this build does not run. A
/// draft is shown with no Apply, and an Apply pressed anyway is refused before anything is saved, leaving it standing.</para>
///
/// <para><b>Routes and verification pipelines</b>: a <c>route</c> (<c>default</c>,
/// <c>landed</c> or <c>kind:&lt;id&gt;</c>) chooses the workflow for that route, and with no version it is the <c>use</c> door alone,
/// saving nothing. A <c>graph</c> beside <c>steps</c> is a verification pipeline's version, judged by the store's reader under its mark,
/// and a new workflow proposed as one is made a verification pipeline; a development pipeline's graph is not proposed, since its part
/// for the person is read nowhere yet. What the change lowers (<see cref="InvolvementRoutes"/>) is said on the card, which applies only
/// at the person's press; a landed route's gates are left to their own <i>Accept the gates</i>.</para>
///
/// <para><b>Not handled</b>: changes against a base, a kind, and an import; the box refuses them by name. A new workflow's
/// name is the person's words where the box carries them, else its id; an edit keeps its workflow's name.</para>
/// </remarks>
internal sealed class HelpWorkflowProposals : IHelpProposalKind
{
    public string Kind => "workflow";

    // A whole version, new or an edit, and its choice; changes, kinds and imports are left out.
    public string Tool => "workflow_propose";

    // `draft`, a change to the draft open in the person's editor, which saves and chooses nothing.
    public IReadOnlyList<string> Doors { get; } = ["new", "edit", "use", Drafts];

    private const string Drafts = "draft";

    public HelpProposal Read(HelpProposal proposal, JsonElement file)
    {
        if (file.ValueKind != JsonValueKind.Object) return proposal;
        int? baseVersion = file.TryGetProperty("base", out var number) && number.ValueKind == JsonValueKind.Number
            && number.TryGetInt32(out var whole) ? whole : null;
        var read = proposal with
        {
            Scope = Text(file, "scope"),
            Workflow = Text(file, "workflow"),
            Base = baseVersion,
            Name = Text(file, "name"),
            Choose = file.TryGetProperty("choose", out var choose) && choose.ValueKind == JsonValueKind.True,
            // Cloned: the file's document is gone once it is read.
            Steps = file.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array ? steps.Clone() : null,
            Graph = file.TryGetProperty("graph", out var graph) && graph.ValueKind == JsonValueKind.Object ? graph.Clone() : null,
            Route = Text(file, "route"),
            Draft = Text(file, "draft"),
        };

        // The box writes no door: a draft's fingerprint makes it a change to that draft, a base an edit, none a new workflow,
        // and a route with no version the use door alone.
        var door = read.Draft is not null ? Drafts
            : baseVersion is not null ? "edit" : read.Steps is null && read.Graph is null && read.Route is not null ? "use" : "new";
        return proposal.Kind == Kind && proposal.Door.Length == 0 ? read with { Door = door } : read;
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        // Judged before any save or use, so a draft never reaches a save's path.
        if (proposal.Door == Drafts) return HelpWorkflowDrafts.Plan(proposal, facts);
        var id = proposal.Workflow?.Trim() ?? "";
        if (WorkflowNamed.IdProblem(id) is { } idProblem) return Refused(idProblem);
        if (proposal.Steps is not null && proposal.Graph is not null) return Refused("a workflow proposal carries its `steps` or its `graph`, never both.");
        var route = string.IsNullOrWhiteSpace(proposal.Route) ? null : proposal.Route.Trim();
        if (route is not null && RouteProblem(route) is { } routeProblem) return Refused(routeProblem);
        if (proposal.Steps is null && proposal.Graph is null && route is null)
        {
            return Refused("a workflow proposal carries its whole `graph`; or, with none, the `route` it chooses a saved workflow for.");
        }

        var scope = proposal.Scope?.Trim() ?? "";
        var repository = facts.Repositories.FirstOrDefault(name => Same(name, scope));
        var workspace = facts.Workspaces.FirstOrDefault(name => Same(name, scope));
        if (repository is null && workspace is null)
        {
            return Refused($"there is no repository or workspace `{scope}` on this machine: a workflow proposal names a repository as "
                + $"Repositories lists it, or one of the workspaces {Names(facts.Workspaces)}.");
        }

        if (repository is not null && workspace is not null)
        {
            return Refused($"`{scope}` names both a repository and a workspace on this machine, and a workflow proposal does not say "
                + $"which: `daoris workflow use {id} --repository|--workspace {scope}` chooses it for either.");
        }

        var found = facts.Workflows.FirstOrDefault(each => each.Id == id);
        var saves = proposal.Steps is not null || proposal.Graph is not null;
        if (!saves && UseProblem(id, proposal, found) is { } unsaved) return Refused(unsaved);

        // The workflow's mark says which version it takes. A new workflow proposed as a graph
        // that lands its work (a `land`) is a development pipeline, as the person's part in its graph is read;
        // one that lands nothing is a verification pipeline.
        var verifies = found?.Read.Verifies ?? (proposal.Graph is { } proposed && !HoldsLanding(proposed));

        if (saves && BaseProblem(id, proposal.Base, found) is { } stale) return Refused(stale);

        // A list of steps is not saved, by this door or any other.
        if (proposal.Steps is not null) return Refused(WorkflowNamed.StepsRefused(id));

        var newest = found?.Read.Versions is [.., var last] ? last : null;
        var before = !saves || proposal.Base is null ? null : newest;
        var version = !saves ? newest!.Version : before is null ? 1 : before.Version + 1;
        NodeGraph? graph = null;
        if (proposal.Graph is { } drawn)
        {
            // Judged as the store judges a save under the mark: the graph reader, then the pipeline's own rule.
            var read = WorkflowNamed.ReadVersion(GraphVersion(drawn, version), version, [], verifies ? WorkflowPipelines.Verification : WorkflowPipelines.Development, onSave: true, id: id);
            if (read.Graph is null) return Refused(read.Problem!);
            graph = read.Graph;
        }

        // An edit keeps its workflow's name; a new workflow takes the person's words, else its id.
        var given = proposal.Base is null ? proposal.Name?.Trim() : null;
        if (!string.IsNullOrEmpty(given) && WorkflowNamed.NameProblem(given) is { } nameProblem) return Refused(nameProblem);
        var name = found?.Read.Name ?? (string.IsNullOrEmpty(given) ? id : given);
        var unchanged = !saves || (graph is not null && before?.Graph is not null && before.Digest == NodeGraphs.Digest(graph));

        // The choice: the scope's route made this workflow (its default where the proposal names no route), judged by the edit that
        // makes it; none where it is made already.
        var chosen = route ?? (proposal.Choose ? Routes.Default : null);
        var map = repository is not null ? config.Workflows : config.WorkspaceWorkflows;
        var inWorkspace = repository is not null ? WorkspaceOf(facts, repository) : workspace;
        WorkflowUse? use = null;
        var after = config;
        if (chosen is not null && !ChoosesRoute(ChoiceOf(map, scope), id, chosen))
        {
            var mark = verifies ? WorkflowPipelines.Verification : WorkflowPipelines.Development;
            use = UseOf(id, chosen, repository, workspace, inWorkspace) with { Pipeline = mark };
            try
            {
                after = WorkflowSelection.Apply(config, use);
            }
            catch (DriverException error)
            {
                return Refused(error.Message);
            }
        }

        var whose = repository is not null ? $"`{repository}`" : $"the workspace `{workspace}`";
        var landed = chosen == Routes.Landed;
        var reached = landed ? ReachedLanded(after, facts, id) : Reached(after, facts, id);
        var kept = facts.WorkflowRuns.Count(run => run.Workflow == id && !run.IsCurrent);
        var applies = AppliesTo(id, reached, kept, use is not null ? workspace : null);
        var needs = new List<string>();

        // A graph chosen with no save integrates its landing in each repository it reaches, so
        // each that cannot carry it is a need, and each whose rule integrates with no gates is said (F42).
        var integrates = new List<string>();
        var noGates = new List<string>();
        if (!saves && !verifies && !landed && use is not null && newest?.Graph is { } chosenGraph && IntegrateChoice.Matters(chosenGraph))
        {
            var asks = IntegrateChoice.NeedsAccepted(chosenGraph);
            foreach (var touched in reached.Union(Scoped(after, facts, repository, workspace), StringComparer.OrdinalIgnoreCase))
            {
                var accepted = !asks ? null
                    : facts.AcceptedGates.TryGetValue(touched, out var read) ? read
                    : AcceptedGates.NotRead($"`{touched}`'s accepted declaration was not read here");
                var answer = IntegrateChoice.Of(after, id, version, chosenGraph, touched, WorkspaceOf(facts, touched), accepted);
                needs.AddRange(answer.Cannots.Select(cannot => $"`{touched}`: {cannot}"));
                if (answer.NoGates)
                {
                    integrates.Add(IntegrateCarry.NoGatesSaid(touched));
                    noGates.Add(touched);
                }
            }
        }

        var nodes = graph is not null ? GraphLines(before?.Graph, graph) : [];

        // What the change asks less of the person. An agent's choice that lowers anything is
        // kept as this proposal, never applied on its own; only the person's press applies it.
        var lowers = new List<string>();
        if (graph is not null && before?.Graph is { } was && !unchanged) lowers.AddRange(InvolvementRoutes.LowersVerification(graph, was));
        if (use is { Landed: true })
        {
            NodeGraph? Newest(string each) => each == id && graph is not null ? graph
                : facts.Workflows.FirstOrDefault(file => file.Id == each) is { Read: { Problem: null, Versions: [.., var at] } } ? at.Graph : null;
            var then = WorkflowSelection.ResolveLanded(config, repository, inWorkspace);
            var now = WorkflowSelection.ResolveLanded(after, repository, inWorkspace);
            lowers.AddRange(InvolvementRoutes.LowersRoute(new WorkflowLanded(now.Workflow, now.Gates), new WorkflowLanded(then.Workflow, then.Gates), Newest));
        }

        var judged = InvolvementRoutes.Judge([.. lowers.Distinct(StringComparer.Ordinal)], byThePerson: false);

        var forScope = ForRoute(chosen, repository, workspace);
        var said = new List<string>
        {
            !saves
                ? use is null
                    ? $"{Capital(whose)} chooses `{id}`{RouteWords(chosen!)} already: applying changes nothing."
                    : $"Choose `{id}` ({name}) {forScope}; no version is saved, and it follows its newest, v{version}."
            : unchanged
                ? $"`{id}` v{before!.Version} already says this"
                  + (use is null ? ": applying changes nothing." : $", so no version is saved: applying chooses it {forScope}.")
                : (before is null ? $"Save a new {(verifies ? "verification pipeline" : "workflow")} `{id}` as v1" : $"Save `{id}` v{before.Version} → v{version} ({name})")
                  + (use is null ? "" : $", and choose it {forScope}") + ".",
        };
        if (saves && chosen is not null && use is null) said.Add($"{Capital(whose)} chooses `{id}`{RouteWords(chosen)} already.");
        if (!unchanged && graph is not null)
        {
            said.Add("Nodes, by id:");
            said.AddRange(nodes.Select(each => $"  {each}"));
        }

        if (!judged.Applies)
        {
            said.Add("It asks less of you, so only your press applies it:");
            said.AddRange(judged.Lowered.Select(each => $"  {each}"));
        }

        if (landed && use is not null)
        {
            // The declaration is accepted by the person's own door, never by a proposal.
            said.Add(repository is not null
                ? $"Its gates wait for your *Accept the gates* on `{repository}`'s Workflow tab, which reads `daoris.gates.json` at its line's tip."
                : "Each repository's gates wait for your *Accept the gates* on its Workflow tab, which reads `daoris.gates.json` at its line's tip.");
        }

        said.AddRange(integrates);

        if (needs.Count > 0)
        {
            said.Add("Needs first:");
            said.AddRange(needs.Select(need => $"  {need}"));
        }

        // A repository's choice replaces its workspace's whole, as the terminal's `use` says it.
        // The landed route resolves on its own and replaces nothing.
        if (use is { Repository: { } chooser, Landed: false } && OwnOf(config, chooser) is null)
        {
            said.Add($"This replaces the workspace's for every kind: `{chooser}` follows its own kinds and its own default, and this machine's "
                + "default where it names none.");
        }

        said.Add($"Applies to: {applies}");

        var terminals = new List<string>();
        if (!unchanged)
        {
            terminals.Add(before is null ? "daoris workflow import <file>" : $"daoris workflow apply {id} <file> --base {before.Version}");
        }

        if (use is not null)
        {
            var flag = use.Landed ? " --landed" : use.Kind is { } kind ? $" --kind {kind}" : "";
            terminals.Add(repository is not null
                ? $"daoris workflow use {id} --repository {repository}{flag}"
                : $"daoris workflow use {id} --workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}{flag}");
        }

        var card = new HelpWorkflowCard(
            id, name, before?.Version, version, repository ?? workspace!, nodes, Part(graph), Outward(graph),
            needs, applies, chosen is not null)
        {
            ScopeKind = repository is not null ? "repository" : "workspace",
            Steps = unchanged || proposal.Steps is not { } stepsText ? null : JsonNode.Parse(stepsText.GetRawText()),
            Graph = unchanged || proposal.Graph is not { } graphText ? null : JsonNode.Parse(graphText.GetRawText()),
            Use = use,
            Route = chosen,
            Verifies = verifies,
            Lowers = judged.Lowered,
            NoGates = noGates,
        };
        return new HelpPlan(null, string.Join('\n', said), terminals.Count > 0 ? string.Join(" && ", terminals) : $"daoris workflow show {id}", null)
        {
            Workflow = card,
        };
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var plan = applying.Plan;
        var card = plan.Workflow ?? throw new DriverException($"the card for `#{applying.Id}` was not judged as a workflow's.");

        // A draft's Apply, before any save or choice is looked at, writes neither.
        if (applying.Proposal.Door == Drafts || card.Draft is not null) return Task.FromResult(HelpWorkflowDrafts.Apply(applying, card));

        // A draft offers no Apply. One pressed anyway is refused before anything is saved, and the card stands.
        if (card.Needs.Count > 0)
        {
            throw new DriverException($"the card for `#{applying.Id}` is a draft: what it needs first is met before it applies — "
                + string.Join(" ", card.Needs.Select(Capital)));
        }

        if (!card.Saves && card.Use is null)
        {
            return Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — nothing changed: {plan.Describe}", null));
        }

        var said = new List<string>();
        if (card.Saves)
        {
            // Judged again at the press, so a version saved after the card is drawn makes it stale, never applied over.
            var stale = BaseProblem(card.Id, card.Base, WorkflowStore.Load(applying.Home, card.Id));
            if (stale is not null) return Task.FromResult(applying.Settled(false, $"Not applied: `#{applying.Id}` — {stale}", stale));
            try
            {
                // A verification pipeline is made so at its first save, and every later save names the mark its file keeps.
                var saved = WorkflowStore.Add(applying.Home, new WorkflowVersionAdded(
                    card.Id, card.Base is null ? card.Name : null, $"ask-daoris:{applying.Id}", Moment(), card.Steps, card.Graph)
                {
                    Pipeline = card.Verifies ? WorkflowPipelines.Verification : null,
                });
                said.Add($"`{card.Id}` v{saved} is saved");
            }
            catch (DriverException error)
            {
                return Task.FromResult(applying.Settled(false, $"Not applied: `#{applying.Id}` — {error.Message}", error.Message));
            }
        }

        if (card.Use is { } use)
        {
            // A route with no version chooses a workflow saved here: one removed after the card is drawn is chosen by nothing.
            if (!card.Saves && WorkflowStore.Load(applying.Home, card.Id) is null)
            {
                var gone = $"there is no workflow `{card.Id}` on this machine now, so nothing is chosen.";
                return Task.FromResult(applying.Settled(false, $"Not applied: `#{applying.Id}` — {gone}", gone));
            }

            var route = card.Route is { } named ? RouteWords(named) : "";
            try
            {
                applying.Doors.Change(config => WorkflowSelection.Apply(config, use));
                said.Add(use.Repository is not null ? $"`{use.Repository}` chooses it{route} now" : $"the workspace `{use.Workspace}` chooses it{route} now");
            }
            catch (DriverException error) when (said.Count > 0)
            {
                said.Add($"the choice was not made: {error.Message.TrimEnd('.')}");
            }
        }

        return Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` (`{plan.Terminal}`) — {string.Join(", and ", said)}.", null));
    }

    /// <summary>
    /// Why the base no longer stands (the terminal's <c>--base</c>): a new workflow's id taken, an edit's workflow gone or
    /// unreadable, or a newer version than its base saved. Null where it stands.
    /// </summary>
    private static string? BaseProblem(string id, int? @base, WorkflowFileFound? found)
    {
        var newest = found?.Read.Versions is [.., var last] ? last.Version : (int?)null;
        if (@base is not { } number)
        {
            return found is null ? null
                : newest is { } at ? $"`{id}` is a workflow here already, at v{at}: a change to it names its base, v{at}."
                : $"`{id}` is a workflow here already, and its file cannot be read: {found.Read.Problem}";
        }

        if (found is null) return $"there is no workflow `{id}` on this machine: a new workflow names no base.";
        if (found.Read.Problem is { } unread) return $"`{id}`'s file cannot be read here, and nothing is saved over it: {unread}";
        return newest == number ? null
            : $"`{id}` is at v{newest} now, not v{number}: read it again (`daoris workflow show {id}`) and make the change against v{newest}.";
    }


    /// <summary>A graph proposed, as the version a save would write around it (schema 2), for the store's own reader to judge.</summary>
    internal static JsonElement GraphVersion(JsonElement graph, int version)
    {
        var node = new JsonObject
        {
            ["version"] = version, ["at"] = Moment(), ["door"] = "screen", ["schema"] = WorkflowNamed.GraphSchema, ["graph"] = JsonNode.Parse(graph.GetRawText()),
        };
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    /// <summary>The repositories whose landed work the workflow would verify: each whose landed route resolves to it.</summary>
    private static List<string> ReachedLanded(DriverConfig config, HelpMachineFacts facts, string id) =>
    [
        .. facts.Repositories
            .Where(repository => WorkflowSelection.ResolveLanded(config, repository, WorkspaceOf(facts, repository)).Workflow == id)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>
    /// A graph's nodes and wires compared by id, as the steps' diff marks them (<see cref="WorkflowDiff"/>): <c>−</c> removed where it
    /// stood, <c>+</c> added, <c>~</c> changed, and a kept one unmarked; then each wire removed or added.
    /// </summary>
    private static List<string> GraphLines(NodeGraph? before, NodeGraph after)
    {
        var lines = new List<string>();
        var was = before?.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal) ?? [];
        var now = after.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        lines.AddRange(was.Values.Where(node => !now.Contains(node.Id)).Select(node => $"− {node.Id} · {node.Kind}"));
        foreach (var node in after.Nodes)
        {
            var mark = !was.TryGetValue(node.Id, out var old) ? "+" : old.Equals(node) ? " " : "~";
            lines.Add($"{mark} {node.Id} · {node.Kind}");
        }

        static string Said(Wire wire) => $"wire `{wire.Id}`: `{wire.From?.Node ?? "a wait"}` → `{wire.To.Node}`";
        var wires = before?.Wires.ToDictionary(wire => wire.Id, StringComparer.Ordinal) ?? [];
        var kept = after.Wires.Select(wire => wire.Id).ToHashSet(StringComparer.Ordinal);
        lines.AddRange(wires.Values.Where(wire => !kept.Contains(wire.Id)).Select(wire => $"− {Said(wire)}"));
        lines.AddRange(after.Wires.Where(wire => !wires.ContainsKey(wire.Id)).Select(wire => $"+ {Said(wire)}"));
        return lines;
    }

    /// <summary>A verification graph's part for the person: each check whose failure waits for their press.</summary>
    private static List<string> Part(NodeGraph? graph) =>
    [
        .. (graph?.Nodes ?? []).Where(node => node.Kind == NodeKinds.Check && (Failed(node) ?? InvolvementRoutes.FailedWait) == InvolvementRoutes.FailedWait)
            .Select(node => $"{node.Id}: you ask for a fix when it fails (*Ask for a fix…*)."),
    ];

    /// <summary>What a verification graph does with no press of the person's: a check whose failure asks for its fix, a plugin's node.</summary>
    private static List<string> Outward(NodeGraph? graph)
    {
        var said = new List<string>();
        foreach (var node in graph?.Nodes ?? [])
        {
            if (node.Kind == NodeKinds.Check && Failed(node) == "ask") said.Add($"`{node.Id}` asks for a fix of each failed gate with no press of yours.");
            if (node.Kind.Contains('/', StringComparison.Ordinal)) said.Add($"`{node.Id}`, a `{node.Kind}`, runs with no press of yours.");
        }

        return said;
    }

    /// <summary>A check's <c>failed</c>, or null where it names none.</summary>
    private static string? Failed(NodeDeclaration check)
    {
        if (!check.Options.TryGetValue("failed", out var text)) return null;
        using var value = JsonDocument.Parse(text);
        return value.RootElement.ValueKind == JsonValueKind.String ? value.RootElement.GetString() : null;
    }

    /// <summary>The repositories whose new work would follow the workflow: each whose own choice names it, else whose workspace's does.</summary>
    private static List<string> Reached(DriverConfig config, HelpMachineFacts facts, string id) =>
    [
        .. facts.Repositories
            .Where(repository => OwnOf(config, repository) is { } own
                ? Chooses(own, id, kinds: true)
                : Chooses(ChoiceOf(config.WorkspaceWorkflows, RemoteTarget.Workspace(WorkspaceOf(facts, repository))), id, kinds: true))
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>The repositories the proposal is for: its repository, or each of its workspace's whose own choice does not replace it.</summary>
    private static IEnumerable<string> Scoped(DriverConfig config, HelpMachineFacts facts, string? repository, string? workspace) =>
        repository is not null
            ? [repository]
            : facts.Repositories
                .Where(each => Same(RemoteTarget.Workspace(WorkspaceOf(facts, each)), workspace!) && OwnOf(config, each) is null)
                .Order(StringComparer.OrdinalIgnoreCase);

    /// <summary>The *Applies to*: new work only, where, and how many runs keep the version they started on.</summary>
    private static string AppliesTo(string id, IReadOnlyList<string> reached, int kept, string? chosenWorkspace)
    {
        var runs = kept switch
        {
            0 => "",
            1 => "; 1 run keeps the version it started on",
            _ => $"; {kept} runs keep the version they started on",
        };
        if (reached.Count > 0) return $"New work only, in {string.Join(", ", reached.Select(each => $"`{each}`"))}{runs}.";
        return chosenWorkspace is not null
            ? $"New work only, in the workspace `{chosenWorkspace}`'s repositories, none of which follows its choice here now{runs}."
            : $"New work only, once a repository or a workspace chooses `{id}`: nothing does now{runs}.";
    }

    private static string? WorkspaceOf(HelpMachineFacts facts, string repository) =>
        facts.Registered.FirstOrDefault(each => Same(each.Repository, repository)).Workspace;

    private static WorkflowChoice? ChoiceOf(IReadOnlyDictionary<string, WorkflowChoice> map, string name) =>
        map.FirstOrDefault(pair => Same(pair.Key, name)).Value;

    /// <summary>
    /// A repository's own development routes, which replace its workspace's, or null: an entry naming only its landed route
    /// names none, so its workspace's reach it.
    /// </summary>
    private static WorkflowChoice? OwnOf(DriverConfig config, string repository) =>
        ChoiceOf(config.Workflows, repository) is { NamesDevelopment: true } own ? own : null;

    /// <summary>Whether a choice names the workflow as its default, or, with <paramref name="kinds"/>, for any kind.</summary>
    private static bool Chooses(WorkflowChoice? choice, string id, bool kinds) =>
        choice is not null && (choice.Default == id || (kinds && choice.Kinds.Any(kind => kind.Value.Workflow == id)));

    internal static HelpPlan Refused(string why) => new(why, "", "", null);

    internal static bool Same(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    internal static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>The moment a version is saved, as the workflow's file writes it (ISO 8601, to the second, UTC).</summary>
    private static string Moment() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

