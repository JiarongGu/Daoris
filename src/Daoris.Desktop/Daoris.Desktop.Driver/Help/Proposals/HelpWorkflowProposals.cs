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
        };

        // The box writes no door: a base makes it an edit, none a new workflow (D157's WORKFLOW1h-b note).
        return proposal.Kind == Kind && proposal.Door.Length == 0 ? read with { Door = baseVersion is null ? "new" : "edit" } : read;
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var id = proposal.Workflow?.Trim() ?? "";
        if (WorkflowNamed.IdProblem(id) is { } idProblem) return Refused(idProblem);
        if (proposal.Steps is not { } raw) return Refused("a workflow proposal carries its whole `steps`, a JSON list of step objects.");

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
        if (BaseProblem(id, proposal.Base, found) is { } stale) return Refused(stale);

        var before = proposal.Base is null ? null : found!.Read.Versions[^1];
        var version = before is null ? 1 : before.Version + 1;
        var prior = found?.Read.Versions.Where(each => each.Steps is not null).ToList() ?? [];
        var (steps, shape) = WorkflowNamed.ReadShape(raw, version, prior);
        if (steps is null) return Refused(shape!);

        // An edit keeps its workflow's name; a new workflow takes the person's words, else its id (WORKFLOW1h-name).
        var given = proposal.Base is null ? proposal.Name?.Trim() : null;
        if (!string.IsNullOrEmpty(given) && WorkflowNamed.NameProblem(given) is { } nameProblem) return Refused(nameProblem);
        var name = found?.Read.Name ?? (string.IsNullOrEmpty(given) ? id : given);
        var unchanged = before?.Steps is not null && before.Digest == WorkflowNamed.Digest(steps);

        // The choice: the scope's default made this workflow, judged by the edit that makes it; none where it is made already.
        WorkflowUse? use = null;
        var after = config;
        if (proposal.Choose && !Chooses(ChoiceOf(repository is not null ? config.Workflows : config.WorkspaceWorkflows, scope), id, kinds: false))
        {
            use = repository is not null ? new WorkflowUse(id, repository, null, null) : new WorkflowUse(id, null, workspace, null);
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
        var reached = Reached(after, facts, id);
        var kept = facts.WorkflowRuns.Count(run => run.Workflow == id && !run.IsCurrent);
        var applies = AppliesTo(id, reached, kept, use is not null ? workspace : null);
        List<string> needs = unchanged && use is null
            ? []
            : Needs(after, facts, id, version, steps, [.. reached.Union(Scoped(after, facts, repository, workspace), StringComparer.OrdinalIgnoreCase)]);
        var diff = WorkflowDiff.Of(before?.Steps, steps);

        var forScope = repository is not null ? $"for `{repository}`" : $"for the workspace `{workspace}`";
        var said = new List<string>
        {
            unchanged
                ? $"`{id}` v{before!.Version} already says this"
                  + (use is null ? ": applying changes nothing." : $", so no version is saved: applying chooses it {forScope}.")
                : (before is null ? $"Save a new workflow `{id}` as v1" : $"Save `{id}` v{before.Version} → v{version} ({name})")
                  + (use is null ? "" : $", and choose it {forScope}") + ".",
        };
        if (proposal.Choose && use is null) said.Add($"{Capital(whose)} chooses `{id}` already.");
        if (!unchanged) said.AddRange(diff.Said);
        if (needs.Count > 0)
        {
            said.Add("Needs first:");
            said.AddRange(needs.Select(need => $"  {need}"));
        }

        // §4.7: a repository's choice replaces its workspace's whole, as the terminal's `use` says it.
        if (use is { Repository: { } chooser } && ChoiceOf(config.Workflows, chooser) is null)
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
            terminals.Add(repository is not null
                ? $"daoris driver workflow use {id} --repository {repository}"
                : $"daoris driver workflow use {id} --workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}");
        }

        var card = new HelpWorkflowCard(
            id, name, before?.Version, version, repository ?? workspace!, diff.Steps, diff.Part, diff.Outward, needs, applies, proposal.Choose)
        {
            ScopeKind = repository is not null ? "repository" : "workspace",
            Steps = unchanged ? null : JsonNode.Parse(raw.GetRawText()),
            Use = use,
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
                var saved = WorkflowStore.Add(applying.Home, new WorkflowVersionAdded(
                    card.Id, card.Base is null ? card.Name : null, $"ask-daoris:{applying.Id}", Moment(), card.Steps));
                said.Add($"`{card.Id}` v{saved} is saved");
            }
            catch (DriverException error)
            {
                return Task.FromResult(applying.Settled(false, $"Not applied: `#{applying.Id}` — {error.Message}", error.Message));
            }
        }

        if (card.Use is { } use)
        {
            try
            {
                applying.Doors.Change(config => WorkflowSelection.Apply(config, use));
                said.Add(use.Repository is not null ? $"`{use.Repository}` chooses it now" : $"the workspace `{use.Workspace}` chooses it now");
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

    /// <summary>The repositories whose new work would follow the workflow (§4.1): each whose own choice names it, else whose workspace's does.</summary>
    private static List<string> Reached(DriverConfig config, HelpMachineFacts facts, string id) =>
    [
        .. facts.Repositories
            .Where(repository => ChoiceOf(config.Workflows, repository) is { } own
                ? Chooses(own, id, kinds: true)
                : Chooses(ChoiceOf(config.WorkspaceWorkflows, RemoteTarget.Workspace(WorkspaceOf(facts, repository))), id, kinds: true))
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>The repositories the proposal is for: its repository, or each of its workspace's whose own choice does not replace it.</summary>
    private static IEnumerable<string> Scoped(DriverConfig config, HelpMachineFacts facts, string? repository, string? workspace) =>
        repository is not null
            ? [repository]
            : facts.Repositories
                .Where(each => Same(RemoteTarget.Workspace(WorkspaceOf(facts, each)), workspace!) && ChoiceOf(config.Workflows, each) is null)
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

    /// <summary>The choice Apply makes; null where none is asked, or it is made already.</summary>
    public WorkflowUse? Use { get; init; }

    /// <summary>Whether Apply saves a version.</summary>
    public bool Saves => Steps is not null;
}
