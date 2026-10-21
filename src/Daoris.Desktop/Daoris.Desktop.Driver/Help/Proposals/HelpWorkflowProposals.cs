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

    public IReadOnlyList<string> Doors { get; } = ["new", "edit", "use"];

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
        };

        // The box writes no door: a base makes it an edit, none a new workflow (D157's WORKFLOW1h-b note), and a route with no version
        // the use door alone (FABRIC-PIPELINE1i).
        var door = baseVersion is not null ? "edit" : read.Steps is null && read.Graph is null && read.Route is not null ? "use" : "new";
        return proposal.Kind == Kind && proposal.Door.Length == 0 ? read with { Door = door } : read;
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var id = proposal.Workflow?.Trim() ?? "";
        if (WorkflowNamed.IdProblem(id) is { } idProblem) return Refused(idProblem);
        if (proposal.Steps is not null && proposal.Graph is not null) return Refused("a workflow proposal carries its `steps` or its `graph`, never both.");
        var route = string.IsNullOrWhiteSpace(proposal.Route) ? null : proposal.Route.Trim();
        if (route is not null && RouteProblem(route) is { } routeProblem) return Refused(routeProblem);
        if (proposal.Steps is null && proposal.Graph is null && route is null)
        {
            return Refused("a workflow proposal carries its whole `steps`, a JSON list of step objects, or a verification pipeline's `graph`; "
                + "or, with neither, the `route` it chooses a saved workflow for.");
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
        // is made a verification pipeline, since the person's part in a development pipeline's graph is read nowhere yet, so only a
        // verification pipeline's graph is proposed; its steps, a development pipeline's.
        var verifies = found?.Read.Verifies ?? proposal.Graph is not null;
        if (proposal.Steps is not null && verifies)
        {
            return Refused($"`{id}` is a verification pipeline, whose versions are graphs holding its checks: a proposal changes it with its "
                + "`graph`, never `steps`.");
        }

        if (proposal.Graph is not null && !verifies)
        {
            return Refused($"`{id}` is a development pipeline, and Ask Daoris proposes a graph only for a verification pipeline, since the "
                + $"person's part in a development pipeline's graph is not read yet: `daoris driver workflow apply {id} <file>` saves one.");
        }

        if (saves && BaseProblem(id, proposal.Base, found) is { } stale) return Refused(stale);

        var newest = found?.Read.Versions is [.., var last] ? last : null;
        var before = !saves || proposal.Base is null ? null : newest;
        var version = !saves ? newest!.Version : before is null ? 1 : before.Version + 1;
        IReadOnlyList<NamedStep>? steps = null;
        NodeGraph? graph = null;
        if (proposal.Steps is { } raw)
        {
            var prior = found?.Read.Versions.Where(each => each.Steps is not null).ToList() ?? [];
            (steps, var shape) = WorkflowNamed.ReadShape(raw, version, prior);
            if (steps is null) return Refused(shape!);
        }
        else if (proposal.Graph is { } drawn)
        {
            // Judged as the store judges a save under the mark (§2.1): the graph reader, then the verification rule.
            var read = WorkflowNamed.ReadVersion(GraphVersion(drawn, version), version, [], WorkflowPipelines.Verification);
            if (read.Graph is null) return Refused(read.Problem!);
            graph = read.Graph;
        }

        // An edit keeps its workflow's name; a new workflow takes the person's words, else its id (WORKFLOW1h-name).
        var given = proposal.Base is null ? proposal.Name?.Trim() : null;
        if (!string.IsNullOrEmpty(given) && WorkflowNamed.NameProblem(given) is { } nameProblem) return Refused(nameProblem);
        var name = found?.Read.Name ?? (string.IsNullOrEmpty(given) ? id : given);
        var unchanged = !saves
            || (steps is not null && before?.Steps is not null && before.Digest == WorkflowNamed.Digest(steps))
            || (graph is not null && before?.Graph is not null && before.Digest == NodeGraphs.Digest(graph));

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
        var stepsNow = steps ?? (saves ? null : newest!.Steps);
        List<string> needs = (unchanged && use is null) || stepsNow is null
            ? []
            : Needs(after, facts, id, version, stepsNow, [.. reached.Union(Scoped(after, facts, repository, workspace), StringComparer.OrdinalIgnoreCase)]);
        var diff = steps is not null ? WorkflowDiff.Of(before?.Steps, steps) : null;
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
        if (!unchanged && diff is not null) said.AddRange(diff.Said);
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

        if (needs.Count > 0)
        {
            said.Add("Needs first:");
            said.AddRange(needs.Select(need => $"  {need}"));
        }

        // §4.7: a repository's choice replaces its workspace's whole, as the terminal's `use` says it.
        // The landed route resolves on its own and replaces nothing (FABRIC-PIPELINE1a).
        if (use is { Repository: { } chooser, Landed: false } && OwnOf(config, chooser) is null)
        {
            said.Add($"This replaces the workspace's for every kind: `{chooser}` follows its own kinds and its own default, and Current where "
                + "it names none.");
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
            id, name, before?.Version, version, repository ?? workspace!, diff?.Steps ?? nodes, diff?.Part ?? Part(graph), diff?.Outward ?? Outward(graph),
            needs, applies, chosen is not null)
        {
            ScopeKind = repository is not null ? "repository" : "workspace",
            Steps = unchanged || proposal.Steps is not { } stepsText ? null : JsonNode.Parse(stepsText.GetRawText()),
            Graph = unchanged || proposal.Graph is not { } graphText ? null : JsonNode.Parse(graphText.GetRawText()),
            Use = use,
            Route = chosen,
            Verifies = verifies,
            Lowers = judged.Lowered,
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

    /// <summary>
    /// What each step needs first (§8.1, §8.2) in each repository it reaches: the declarations the gate reads it over
    /// (<see cref="WorkflowProcess.Of"/>, its sentences naming their doors), a landing plugin that cannot land here, then each
    /// step of a kind this build does not run (§3.9), never guessed past.
    /// </summary>
    private static List<string> Needs(
        DriverConfig config, HelpMachineFacts facts, string id, int version, IReadOnlyList<NamedStep> steps, IReadOnlyList<string> repositories)
    {
        var needs = new List<string>();
        var plugins = WorkflowCurrent.PluginsOf(facts.Plugins);
        var land = steps.First(step => step.Kind == WorkflowKinds.Landing);
        foreach (var repository in repositories)
        {
            var workspace = WorkspaceOf(facts, repository);
            var binding = new WorkflowRunBinding("", repository, RemoteTarget.Workspace(workspace), id, WorkflowLevels.Repository, DateTimeOffset.MinValue)
            {
                Version = version,
            };
            var process = WorkflowProcess.Of(config, repository, workspace, binding, steps);
            if (process.OpinionCannot is { } opinion) needs.Add(opinion);
            if (process.Look?.Cannot is { } look) needs.Add(look);
            if (process.LandingCannot is { } landing) needs.Add(landing);
            if (process.Landing.Rule is { Form: LandingForm.Branch, Plugin: { } plugin }
                && !plugins.Any(each => Same(each.Id, plugin) && each.Points.Contains(HookPoints.Land, StringComparer.Ordinal)))
            {
                needs.Add($"its landing `{land.Id}` hands `{repository}`'s branch to `{plugin}`. {WorkflowLimits.Says(WorkflowLimits.PluginUnready)}");
            }
        }

        foreach (var step in steps.SelectMany(step => new[] { step }.Concat(step.Members)))
        {
            if (WorkflowKindTable.Of(step.Kind)!.Runtime == WorkflowKindTable.None)
            {
                needs.Add($"its step `{step.Id}` is a `{step.Kind}`, which this build does not run yet.");
            }
        }

        return [.. needs.Distinct(StringComparer.Ordinal)];
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

    /// <summary>Whether the workflow is a verification pipeline, or is made one by the save (the pipeline design §2.1).</summary>
    public bool Verifies { get; init; }

    /// <summary>
    /// What the change asks less of the person (D157 point 10, the pipeline design §1.3), each said as what it does; empty where it
    /// lowers nothing. An agent's change that lowers anything is applied only by the person's press, as every card is.
    /// </summary>
    public IReadOnlyList<string> Lowers { get; init; } = [];

    /// <summary>Whether Apply saves a version.</summary>
    public bool Saves => Steps is not null || Graph is not null;
}
