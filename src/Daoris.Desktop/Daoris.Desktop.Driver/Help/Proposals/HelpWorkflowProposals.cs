using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.HelpProposals;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>workflow</c> proposal (WORKFLOW1h-b, D157 point 13, the workflow design §8): a whole version of a named workflow,
/// new or an edit on its newest, for a repository or a workspace (<c>scope</c>), and optionally its choice there (<c>choose</c>). The
/// file names no door: one with no <c>base</c> is a <c>new</c> workflow, one with a base an <c>edit</c>, and <c>choose</c> is the
/// <c>use</c> door. It is judged as the editor judges a version (<see cref="WorkflowNamed"/>, §2.5, §3.9), and applied by saving
/// it through <see cref="WorkflowStore.Add"/> with the door <c>ask-daoris:&lt;proposal&gt;</c>, then the choice through the
/// driver's file, as <c>daoris driver workflow use</c> makes it.
/// </summary>
/// <remarks>
/// <para><b>Stale is refused, never applied over</b> (§8.1, §2.6): a new workflow takes an id no workflow here has, and an edit's
/// base is the newest version, else the card is refused with the newest number in the terminal's words. The press judges it again
/// (§8.4), so a version saved between the card and the press refuses it the same way.</para>
///
/// <para><b>The card</b> (§8.2) leads with the person's part, then the steps by id, then what leaves the machine with no press, as
/// <see cref="WorkflowDiff"/> says them (the CLI's twin), then what each step needs first where it applies, and what it applies to.
/// Its data rides on <see cref="HelpPlan.Workflow"/>, its words the page's (WORKFLOW1h-d).</para>
///
/// <para><b>Needs first make a draft</b> (§8.1): a declaration a step needs that is not declared in a repository it reaches (the
/// gate's own <see cref="WorkflowProcess.Of"/>), a landing plugin that cannot land here, and a kind this build does not run. A
/// draft is shown with no Apply, and an Apply pressed anyway is refused before anything is saved, leaving it standing.</para>
///
/// <para><b>Routes and verification pipelines</b> (FABRIC-PIPELINE1i, D192 point 2, the pipeline design §1.3): a <c>route</c> (<c>default</c>,
/// <c>landed</c> or <c>kind:&lt;id&gt;</c>) chooses the workflow for that route, and with no version it is the <c>use</c> door alone,
/// saving nothing. A <c>graph</c> beside <c>steps</c> is a verification pipeline's version, judged by the store's reader under its mark,
/// and a new workflow proposed as one is made a verification pipeline; a development pipeline's graph is not proposed, since its part
/// for the person is read nowhere yet. What the change lowers (<see cref="InvolvementRoutes"/>) is said on the card, which applies only
/// at the person's press; a landed route's gates are left to their own <i>Accept the gates</i> (D156).</para>
///
/// <para><b>Left for WORKFLOW1h2</b>: changes against a base, a kind, and an import; the box refuses them by name. A new workflow's
/// name is the person's words where the box carries them (WORKFLOW1h-name), else its id; an edit keeps its workflow's name.</para>
/// </remarks>
internal sealed class HelpWorkflowProposals : IHelpProposalKind
{
    public string Kind => "workflow";

    // WORKFLOW1h-b: a whole version, new or an edit, and its choice; changes, kinds and imports are WORKFLOW1h2's.
    public string Tool => "workflow_propose";

    // FLOWCHART2e: `draft`, a change to the draft open in the person's editor, which saves and chooses nothing.
    public IReadOnlyList<string> Doors { get; } = ["new", "edit", "use", Drafts];

    private const string Drafts = "draft";

    /// <summary>ASKSCOPE1c: its scope is one word, a repository's name or a workspace's, which its judge finds which of.</summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts) =>
        new([proposal.Workspace], [proposal.Repository]) { Either = [proposal.Scope] };

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

        // The box writes no door: a draft's fingerprint makes it a change to that draft (FLOWCHART2e), a base an edit, none a new workflow
        // (D157's WORKFLOW1h-b note), and a route with no version the use door alone (FABRIC-PIPELINE1i).
        var door = read.Draft is not null ? Drafts
            : baseVersion is not null ? "edit" : read.Steps is null && read.Graph is null && read.Route is not null ? "use" : "new";
        return proposal.Kind == Kind && proposal.Door.Length == 0 ? read with { Door = door } : read;
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        // FLOWCHART2e (the flowchart design §8.4): judged before any save or use, so a draft never reaches a save's path.
        if (proposal.Door == Drafts) return PlanDraft(proposal, facts);
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
                + $"which: `daoris driver workflow use {id} --repository|--workspace {scope}` chooses it for either.");
        }

        var found = facts.Workflows.FirstOrDefault(each => each.Id == id);
        var saves = proposal.Steps is not null || proposal.Graph is not null;
        if (!saves && UseProblem(id, proposal, found) is { } unsaved) return Refused(unsaved);

        // FABRIC-PIPELINE1i (the pipeline design §2.1): the workflow's mark says which version it takes. A new workflow proposed as a graph
        // that lands its work (a `land` or a `handed`) is a development pipeline, as the person's part in its graph is read since CUTOVER1e;
        // one that lands nothing is a verification pipeline.
        var verifies = found?.Read.Verifies ?? (proposal.Graph is { } proposed && !HoldsLanding(proposed));

        if (saves && BaseProblem(id, proposal.Base, found) is { } stale) return Refused(stale);

        // WORKFLOWCLEAN1 1b: a list of steps is no longer saved, by this door as by every other.
        if (proposal.Steps is not null) return Refused(WorkflowNamed.StepsRefused(id));

        var newest = found?.Read.Versions is [.., var last] ? last : null;
        var before = !saves || proposal.Base is null ? null : newest;
        var version = !saves ? newest!.Version : before is null ? 1 : before.Version + 1;
        NodeGraph? graph = null;
        if (proposal.Graph is { } drawn)
        {
            // Judged as the store judges a save under the mark (§2.1): the graph reader, then the pipeline's own rule.
            var read = WorkflowNamed.ReadVersion(GraphVersion(drawn, version), version, [], verifies ? WorkflowPipelines.Verification : WorkflowPipelines.Development, onSave: true, id: id);
            if (read.Graph is null) return Refused(read.Problem!);
            graph = read.Graph;
        }

        // An edit keeps its workflow's name; a new workflow takes the person's words, else its id (WORKFLOW1h-name).
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
        if (chosen is not null && !Chooses(ChoiceOf(map, scope), id, chosen))
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

        // INTEGRATE1r part r2 (the integration design §17.7): a graph chosen with no save integrates its landing in each repository it reaches, so
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

        // D157 point 10 (the pipeline design §1.3): what the change asks less of the person. An agent's choice that lowers anything is
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
            // D156: the declaration is accepted by the person's own door, never by a proposal.
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

        // §4.7: a repository's choice replaces its workspace's whole, as the terminal's `use` says it.
        // The landed route resolves on its own and replaces nothing (FABRIC-PIPELINE1a).
        if (use is { Repository: { } chooser, Landed: false } && OwnOf(config, chooser) is null)
        {
            said.Add($"This replaces the workspace's for every kind: `{chooser}` follows its own kinds and its own default, and this machine's "
                + "default where it names none.");
        }

        said.Add($"Applies to: {applies}");

        var terminals = new List<string>();
        if (!unchanged)
        {
            terminals.Add(before is null ? "daoris driver workflow import <file>" : $"daoris driver workflow apply {id} <file> --base {before.Version}");
        }

        if (use is not null)
        {
            var flag = use.Landed ? " --landed" : use.Kind is { } kind ? $" --kind {kind}" : "";
            terminals.Add(repository is not null
                ? $"daoris driver workflow use {id} --repository {repository}{flag}"
                : $"daoris driver workflow use {id} --workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}{flag}");
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
        return new HelpPlan(null, string.Join('\n', said), terminals.Count > 0 ? string.Join(" && ", terminals) : $"daoris driver workflow show {id}", null)
        {
            Workflow = card,
        };
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var plan = applying.Plan;
        var card = plan.Workflow ?? throw new DriverException($"the card for `#{applying.Id}` was not judged as a workflow's.");

        // FLOWCHART2e: a draft's Apply, before any save or choice is looked at, writes neither.
        if (applying.Proposal.Door == Drafts || card.Draft is not null) return Task.FromResult(ApplyDraft(applying, card));

        // §8.1: a draft offers no Apply. One pressed anyway is refused before anything is saved, and the card stands.
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
            // §8.4: judged again at the press, so a version saved since the card was drawn makes it stale, never applied over.
            var stale = BaseProblem(card.Id, card.Base, WorkflowStore.Load(applying.Home, card.Id));
            if (stale is not null) return Task.FromResult(applying.Settled(false, $"Not applied: `#{applying.Id}` — {stale}", stale));
            try
            {
                // A verification pipeline is made so at its first save, and every later save names the mark its file keeps (§2.1).
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
            // A route with no version chooses a workflow saved here: one removed since the card was drawn is chosen by nothing.
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

    /// <summary>The code a save's refusal of the version whole is said by on a draft's card, where no graph rule names it (its pipeline's shape).</summary>
    public const string VersionRefused = "version";

    /// <summary>
    /// The <c>draft</c> door (FLOWCHART2e, D223; the flowchart design §4, §8.4): a whole graph proposed against the draft this conversation
    /// was told under its fingerprint, of either pipeline, read and judged as a save would judge it (each refusal the card's to put right,
    /// never the proposal's), and said as <see cref="WorkflowGraphDiff"/>'s changes. The candidate rides apart from what a save takes, so
    /// <see cref="HelpWorkflowCard.Saves"/> stays false; a base, a choice, a route or a name beside it is refused.
    /// </summary>
    private static HelpPlan PlanDraft(HelpProposal proposal, HelpMachineFacts facts)
    {
        var fingerprint = proposal.Draft?.Trim() ?? "";
        if (proposal.Steps is not null || proposal.Graph is not { } drawn) return Refused("a change to the open draft carries its whole `graph`, never `steps`.");
        if (proposal.Base is not null) return Refused("a change to the open draft saves nothing, so it names no `base`: the draft names where it came from.");
        if (proposal.Choose || !string.IsNullOrWhiteSpace(proposal.Route))
        {
            return Refused("a change to the open draft chooses nothing, so it names no `choose` and no `route`: the person chooses where the workflow is used.");
        }

        if (!string.IsNullOrWhiteSpace(proposal.Name)) return Refused("a change to the open draft names no `name`: the person names the workflow as they save it.");
        if (!facts.Drafts.TryGetValue(fingerprint, out var told))
        {
            return Refused($"the draft `{fingerprint}` was never shown to this conversation: ask again from the editor, whose draft goes beside your words.");
        }

        var scope = proposal.Scope?.Trim() ?? "";
        if (!Same(scope, told.Place)) return Refused($"the draft `{fingerprint}` is for `{told.Place}`, not `{scope}`: a change to it names the place it is for.");
        var named = proposal.Workflow?.Trim() ?? "";
        if (named.Length > 0 && told.Workflow is { } held && named != held) return Refused($"the draft `{fingerprint}` is `{held}`'s, not `{named}`'s.");
        var id = told.Workflow ?? named;
        if (id.Length > 0 && WorkflowNamed.IdProblem(id) is { } idProblem) return Refused(idProblem);

        if (NodeGraphs.Parts(drawn.GetRawText(), out var unread) is not { } parts) return Refused(unread!.Words);
        if (NodeGraphs.Parts(told.Graph.GetRawText(), out var baseUnread) is not { } was)
        {
            return Refused($"the draft `{fingerprint}` does not read as a graph, so nothing is changed against it: {baseUnread!.Words}");
        }

        var proposed = new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") { SchemaVersion = parts.SchemaVersion, Cleanup = parts.Cleanup ?? GraphCleanup.Held };
        var before = new NodeGraph(was.Nodes, was.Wires, was.Entries, was.End ?? "") { SchemaVersion = was.SchemaVersion, Cleanup = was.Cleanup ?? GraphCleanup.Held };
        var found = id.Length > 0 ? facts.Workflows.FirstOrDefault(each => each.Id == id) : null;
        var pipeline = found?.Read.Pipeline
            ?? (told.Pipeline == WorkflowPipelines.Verification ? WorkflowPipelines.Verification : WorkflowPipelines.Development);

        // Judged as the editor's save judges it: each graph rule's refusal at its step or way, and where those hold, the pipeline's own shape.
        var candidate = JsonNode.Parse(drawn.GetRawText())!;
        var problems = facts.Home is { } home ? WorkflowOperations.GraphProblems(home, candidate) : NodeGraphs.Problems(proposed, onSave: true);
        if (problems.Count == 0 && WorkflowNamed.ReadVersion(GraphVersion(drawn, 1), 1, [], pipeline, onSave: true).Problem is { } shape)
        {
            problems = [new WireProblem(VersionRefused, null, null, shape.StartsWith("v1: ", StringComparison.Ordinal) ? shape[4..] : shape)];
        }

        var changes = WorkflowGraphDiff.Of(before, proposed);
        var repository = facts.Repositories.FirstOrDefault(each => Same(each, told.Place));
        var whose = id.Length > 0 ? $"`{id}`" : "a new workflow";
        var said = new List<string>
        {
            (changes.Count == 0 ? $"A change to your workflow draft of {whose} for `{told.Place}` that changes nothing in it."
                : $"A change to your workflow draft of {whose} for `{told.Place}`: {changes.Count} change{(changes.Count == 1 ? "" : "s")}.")
            + " Nothing is saved or chosen: you take the changes into the draft in its editor, and only your Save there saves it.",
        };
        said.AddRange(changes.Select(change => $"  {change.Said}"));
        if (problems.Count > 0)
        {
            said.Add("To put right before it saves:");
            said.AddRange(problems.Select(problem => $"  {Capital(problem.Words)}"));
        }

        var card = new HelpWorkflowCard(
            id, found?.Read.Name ?? id, told.Base, 0, told.Place, [.. changes.Select(change => change.Said)], [], [], [],
            "Nothing is saved or chosen: you take the changes into the draft in its editor, and only your Save there saves it.", false)
        {
            ScopeKind = repository is not null ? "repository" : "workspace",
            Verifies = pipeline == WorkflowPipelines.Verification,
            Draft = fingerprint,
            Changes = changes,
            Problems = problems,
            Proposed = candidate,
        };

        // The place's Workflow, where its editor is.
        return new HelpPlan(null, string.Join('\n', said), "", null)
        {
            Workflow = card,
            Go = new HelpPlace("projects", null, repository is null ? "workspace-workflow" : null),
        };
    }

    /// <summary>
    /// A draft card's Apply (FLOWCHART2e; the flowchart design §8.6): it writes no workflow and no choice. Pressed from the card, with no
    /// change named, it takes the person to the place's Workflow, where the editor shows the proposal, and settles nothing; pressed by the
    /// editor with every change taken or left once, it settles applied, the file recording which. A press naming them otherwise stands.
    /// </summary>
    private static HelpApplied ApplyDraft(HelpApplying applying, HelpWorkflowCard card)
    {
        var proposal = applying.Proposal;
        if (proposal.Taken is null && proposal.Left is null)
        {
            return new HelpApplied(true, $"`#{applying.Id}` is a change to your workflow draft: it is shown in the draft's editor, and nothing is saved or "
                + "chosen until you take its changes into the draft and press Save there.")
            {
                Go = applying.Plan.Go,
                Stands = true,
            };
        }

        IReadOnlyList<string> taken = proposal.Taken ?? [], left = proposal.Left ?? [];
        var ids = card.Changes.Select(change => change.Id).ToHashSet(StringComparer.Ordinal);
        var named = taken.Concat(left).ToList();
        if (named.Count != ids.Count || !ids.SetEquals(named) || named.Distinct(StringComparer.Ordinal).Count() != named.Count)
        {
            return new HelpApplied(false, $"Not applied: `#{applying.Id}` — the press names each of its {ids.Count} changes once, taken or left, and this one did not, "
                + "so the card stands for the editor to name them.")
            {
                Stands = true,
            };
        }

        var path = Path.Combine(FolderOf(applying.Home), $"{Path.GetFileName(applying.Id)}.json");
        var file = JsonNode.Parse(AtomicFile.ReadText(path))!.AsObject();
        file["taken"] = new JsonArray([.. taken.Select(each => (JsonNode)JsonValue.Create(each)!)]);
        file["left"] = new JsonArray([.. left.Select(each => (JsonNode)JsonValue.Create(each)!)]);
        JsonFile.Write(path, file);
        return applying.Settled(true, $"Applied: `#{applying.Id}` — {taken.Count} of its {ids.Count} changes taken into your workflow draft and {left.Count} left; "
            + "nothing is saved until you press Save in its editor.", $"taken {taken.Count}, left {left.Count}");
    }

    /// <summary>
    /// Why the base no longer stands (§2.6, the terminal's <c>--base</c>): a new workflow's id taken, an edit's workflow gone or
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
            : $"`{id}` is at v{newest} now, not v{number}: read it again (`daoris driver workflow show {id}`) and make the change against v{newest}.";
    }


    /// <summary>The routes a workflow proposal chooses its workflow for (FABRIC-PIPELINE1i, the pipeline design §1.3).</summary>
    private static class Routes
    {
        public const string Default = "default";
        public const string Landed = "landed";
        public const string KindPrefix = "kind:";
    }

    /// <summary>Why a route is none a proposal names, or null: <c>default</c>, <c>landed</c>, or <c>kind:&lt;id&gt;</c>.</summary>
    private static string? RouteProblem(string route) =>
        route is Routes.Default or Routes.Landed || (KindOf(route) is { } kind && WorkflowSelection.IsKindId(kind))
            ? null
            : $"`{route}` is not a route: `default`, `landed` for the work that lands, or `kind:<id>` for one of the scope's kinds, such as `kind:docs`.";

    private static string? KindOf(string? route) =>
        route is not null && route.StartsWith(Routes.KindPrefix, StringComparison.Ordinal) ? route[Routes.KindPrefix.Length..] : null;

    /// <summary>
    /// Why a route with no version cannot choose its workflow (FABRIC-PIPELINE1i), or null: the workflow is saved here and its newest
    /// version reads, and the proposal names no base and no name, which only a save takes.
    /// </summary>
    private static string? UseProblem(string id, HelpProposal proposal, WorkflowFileFound? found)
    {
        if (proposal.Base is not null || !string.IsNullOrWhiteSpace(proposal.Name)) return "a `route` with no version saves nothing, so it names no `base` and no `name`.";
        if (found is null) return $"there is no workflow `{id}` on this machine: a `route` with no version chooses a workflow saved here.";
        if (found.Read.Problem is { } unread) return $"`{id}`'s file cannot be read here, so nothing chooses it: {unread}";
        return found.Read.Versions[^1] is { Steps: null, Graph: null } newest
            ? $"`{id}` v{newest.Version} cannot be read here, so nothing chooses it: {newest.Problem}"
            : null;
    }

    /// <summary>A graph proposed, as the version a save would write around it (schema 2), for the store's own reader to judge.</summary>
    /// <summary>Whether a proposed graph holds a landing node, which makes a new workflow a development pipeline.</summary>
    private static bool HoldsLanding(JsonElement graph) =>
        graph.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array
        && nodes.EnumerateArray().Any(node => node.ValueKind == JsonValueKind.Object && node.TryGetProperty("kind", out var kind)
            && kind.ValueKind == JsonValueKind.String && kind.GetString() is NodeKinds.Land or NodeKinds.Handed);

    private static JsonElement GraphVersion(JsonElement graph, int version)
    {
        var node = new JsonObject
        {
            ["version"] = version, ["at"] = Moment(), ["door"] = "screen", ["schema"] = WorkflowNamed.GraphSchema, ["graph"] = JsonNode.Parse(graph.GetRawText()),
        };
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    /// <summary>Whether a choice names the workflow for the route already.</summary>
    private static bool Chooses(WorkflowChoice? choice, string id, string route) => choice is not null && route switch
    {
        Routes.Default => choice.Default == id,
        Routes.Landed => choice.Landed?.Workflow == id,
        _ => choice.KindOf(KindOf(route)!)?.Workflow == id,
    };

    /// <summary>
    /// The edit choosing the workflow for the route, as the terminal's <c>use</c> makes it: a repository's kind checked against its
    /// workspace's kinds, and a landed route with no gates accepted, which only the person's <i>Accept the gates</i> does (D156).
    /// </summary>
    private static WorkflowUse UseOf(string id, string route, string? repository, string? workspace, string? inWorkspace)
    {
        var kind = KindOf(route);
        return new WorkflowUse(id, repository, repository is null ? workspace : null, kind)
        {
            InKnown = repository is not null && kind is not null,
            In = inWorkspace,
            Landed = route == Routes.Landed,
        };
    }

    /// <summary>Who the choice is for, as the card's first line says it.</summary>
    private static string ForRoute(string? route, string? repository, string? workspace)
    {
        var whose = repository is not null ? $"`{repository}`" : $"the workspace `{workspace}`";
        return route switch
        {
            Routes.Landed => $"for the work that lands in {whose}",
            not null when KindOf(route) is { } kind => repository is not null ? $"for `{repository}`'s kind `{kind}`" : $"for the workspace `{workspace}`'s kind `{kind}`",
            _ => $"for {whose}",
        };
    }

    /// <summary>The route as said after <i>chooses it</i>: nothing for the default.</summary>
    private static string RouteWords(string route) => route switch
    {
        Routes.Landed => " for the work that lands",
        _ when KindOf(route) is { } kind => $" for its kind `{kind}`",
        _ => "",
    };

    /// <summary>The repositories whose landed work the workflow would verify (§1.2): each whose landed route resolves to it.</summary>
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

    /// <summary>A verification graph's part for the person (the pipeline design §4.5): each check whose failure waits for their press.</summary>
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

    /// <summary>The repositories whose new work would follow the workflow (§4.1): each whose own choice names it, else whose workspace's does.</summary>
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

    /// <summary>§8.2's *Applies to*: new work only, where, and how many runs keep the version they started on.</summary>
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
    /// A repository's own development routes, which replace its workspace's (§4.1), or null: an entry naming only its landed route
    /// names none, so its workspace's reach it (FABRIC-PIPELINE1a).
    /// </summary>
    private static WorkflowChoice? OwnOf(DriverConfig config, string repository) =>
        ChoiceOf(config.Workflows, repository) is { NamesDevelopment: true } own ? own : null;

    /// <summary>Whether a choice names the workflow as its default, or, with <paramref name="kinds"/>, for any kind.</summary>
    private static bool Chooses(WorkflowChoice? choice, string id, bool kinds) =>
        choice is not null && (choice.Default == id || (kinds && choice.Kinds.Any(kind => kind.Value.Workflow == id)));

    private static HelpPlan Refused(string why) => new(why, "", "", null);

    private static bool Same(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>The moment a version is saved, as the workflow's file writes it (ISO 8601, to the second, UTC).</summary>
    private static string Moment() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

/// <summary>
/// A workflow card's data (WORKFLOW1h-b, the workflow design §8.2), as the page's <c>HelpWorkflowShown</c> takes it: the workflow
/// and its name, the version it is made on (null for a new one) and the one it becomes, the scope, the steps' diff, the person's
/// part and each outward act as <see cref="WorkflowDiff"/> says them, what it needs first, what it applies to, and whether the
/// scope also chooses it. Lines are the driver's English; the page words them (WORKFLOW1h-d).
/// </summary>
/// <param name="Needs">What is missing; while any is, the card is a draft and offers no Apply.</param>
public sealed record HelpWorkflowCard(
    string Id, string Name, int? Base, int Version, string Scope, IReadOnlyList<string> Diff, IReadOnlyList<string> YourPart,
    IReadOnlyList<string> Outward, IReadOnlyList<string> Needs, string AppliesTo, bool Choose)
{
    /// <summary>Whether the scope is a <c>repository</c> or a <c>workspace</c>.</summary>
    public string ScopeKind { get; init; } = "repository";

    /// <summary>The steps Apply saves, as the proposal carried them; null where the version says what the newest says already.</summary>
    public JsonNode? Steps { get; init; }

    /// <summary>The graph Apply saves (FABRIC-PIPELINE1i), as the proposal carried it; null where it carries steps or saves nothing.</summary>
    public JsonNode? Graph { get; init; }

    /// <summary>The choice Apply makes; null where none is asked, or it is made already.</summary>
    public WorkflowUse? Use { get; init; }

    /// <summary>The route the card chooses for (FABRIC-PIPELINE1i): <c>default</c>, <c>landed</c> or <c>kind:&lt;id&gt;</c>; null where none.</summary>
    public string? Route { get; init; }

    /// <summary>
    /// The repositories a chosen graph integrates its landing in with no gates (INTEGRATE1r part r3; the integration design §17.7, F42), by name,
    /// so the page words the line in its own language; the plan's text says the same sentence for the terminal. Empty where none, or where
    /// the card saves a version.
    /// </summary>
    public IReadOnlyList<string> NoGates { get; init; } = [];

    /// <summary>Whether the workflow is a verification pipeline, or is made one by the save (the pipeline design §2.1).</summary>
    public bool Verifies { get; init; }

    /// <summary>
    /// What the change asks less of the person (D157 point 10, the pipeline design §1.3), each said as what it does; empty where it
    /// lowers nothing. An agent's change that lowers anything is applied only by the person's press, as every card is.
    /// </summary>
    public IReadOnlyList<string> Lowers { get; init; } = [];

    /// <summary>
    /// The fingerprint of the draft a <c>draft</c> card changes (FLOWCHART2e), the base its changes are against; null for every other card,
    /// which saves or chooses. A draft card saves no version, so its <see cref="Version"/> is 0.
    /// </summary>
    public string? Draft { get; init; }

    /// <summary>A draft card's changes against its draft, in <see cref="WorkflowGraphDiff"/>'s order, each with what it needs; empty for every other.</summary>
    public IReadOnlyList<GraphChange> Changes { get; init; } = [];

    /// <summary>What a save of a draft card's graph would refuse, each at its step or way: the card's to put right, never a refusal of it.</summary>
    public IReadOnlyList<WireProblem> Problems { get; init; } = [];

    /// <summary>
    /// A draft card's candidate graph, whole, as the proposal carried it, for the editor to take changes from: never <see cref="Graph"/>,
    /// which a save takes, so a draft card never saves. Null for every other card.
    /// </summary>
    public JsonNode? Proposed { get; init; }

    /// <summary>Whether Apply saves a version.</summary>
    public bool Saves => Steps is not null || Graph is not null;
}
