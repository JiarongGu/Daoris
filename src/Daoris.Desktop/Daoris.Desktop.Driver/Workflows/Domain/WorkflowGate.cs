using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// Where a run's own part of the landing gate stands (WORKFLOW1f, D157 points 4, 6 and 10; the workflow design §2.3, §4.4–§4.6,
/// §5): the workflow its run was bound to, read before the second opinion and the person's look. Three let the work go.
/// </summary>
public static class WorkflowGateStates
{
    /// <summary>Its run follows Current, or has no binding here: every gate reads the rules live, as today.</summary>
    public const string None = "none";

    /// <summary>A named version governs it, and nothing of the run's own holds it.</summary>
    public const string Follows = "follows";

    /// <summary>The person kept the kind's workflow for this work, though it changed paths outside the kind's (§4.4).</summary>
    public const string Kept = "kept";

    /// <summary>The workflow its run was bound to cannot be read here, so none of its steps can start (§3.7).</summary>
    public const string CannotStart = "cannot-start";

    /// <summary>The kind's workflow asks less of the person, and the chain changed paths outside the kind's (§4.4).</summary>
    public const string KindPaths = "kind-paths";

    /// <summary>Whether it holds could not be read: the service named no chain, or git no paths.</summary>
    public const string Unread = "unread";
}

/// <summary>
/// The process a chain's work in a repository follows at its gate (WORKFLOW1f, D157 points 4 and 6; the workflow design §2.3,
/// §4.6): how it lands, the review rule its look reads and what rows 5 and 6 say, and the second opinion it waits for: its bound graph
/// version over what the repository declares, each node choosing among what is declared and never beyond it. Work with no binding follows
/// no workflow and is held.
/// </summary>
/// <param name="Landing">The landing intent the work lands by: the bound version's <c>land</c>.</param>
/// <param name="Review">The review rule as declared: its environments, which the chain's and the ask's <c>on</c> reads (§2.3).</param>
/// <param name="Opinion">The second-opinion rule the gate reads; null where none is set, or the version has no opinion node.</param>
public sealed record WorkflowProcess(string Repository, Landing Landing, ResolvedReview? Review, ResolvedOpinion? Opinion) : IWorkplaceProcess, IReviewedProcess, ICheckedProcess
{
    /// <summary>The run it was read for: the chain's first quest in the repository; null where none was named.</summary>
    public string? Run { get; init; }

    /// <summary>The run's binding here; null where it has none.</summary>
    public WorkflowRunBinding? Binding { get; init; }

    /// <summary>The bound version's person part as steps; empty where the version could not be read.</summary>
    public IReadOnlyList<NamedStep> Steps { get; init; } = [];

    /// <summary>
    /// The options of the bound graph version's <c>claimed</c> node, each its JSON text as written (FABRIC-PIPELINE1b3; the pipeline design
    /// §3.1): its sessions' <c>checks</c> and its <c>callMinutes</c>; empty where the graph holds no <c>claimed</c>. Null where the graph
    /// version is not the one its run bound.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Claimed { get; init; }

    /// <summary>
    /// The bound graph version's graph, as its file holds it and never decoded (FABRIC-CUTOVER1e; D190 point 4, the cutover design §4):
    /// what the run's view draws from the run's record, and the landing gate's graph part reads it from (FABRIC-CUTOVER1d). Null where the
    /// graph version is not the one its run bound.
    /// </summary>
    public NodeGraph? Graph { get; init; }

    /// <inheritdoc/>
    public string? IntegrateRequest => Graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Integrate) is { } integrate
        ? IntegrateNodes.RequestOf(integrate)
        : null;

    /// <inheritdoc/>
    public bool JudgesHolds => Graph is not { } graph || !graph.Nodes.Any(node => node.Kind == NodeKinds.Land);

    /// <inheritdoc/>
    public bool LandsByRuntime => Graph?.Nodes.Any(node => node.Kind == NodeKinds.Land) == true
                                  && Binding is { Digest: not null } && Problem is null && Unread is null;

    /// <inheritdoc/>
    public BoundLand? Land(LandingSubject subject)
    {
        if (Graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Land) is not { } land
            || Binding is not { Digest: { } digest } binding || Problem is not null || Unread is not null)
        {
            return null;
        }

        string? Text(string name) => land.Options.TryGetValue(name, out var raw) ? NodeOptions.Value(raw) as string : null;
        var rule = Landing;
        var method = Text("method") ?? (rule.Form == LandingForm.Merge ? LandNodes.Merge : LandNodes.Branch);
        var squash = (land.Options.TryGetValue("squash", out var written) ? NodeOptions.Value(written) as bool? : null) ?? rule.Squash;
        squash &= method is LandNodes.Branch;
        var template = Text("message");
        var ticket = template?.Contains("{ticket}", StringComparison.Ordinal) == true ? BranchPatterns.Ticket(subject, rule.Key) : null;

        // Its options as the bind resolved them: what the version wrote, the bind's resolutions where it left them open, the method and the squash.
        var options = new System.Text.Json.Nodes.JsonObject();
        foreach (var (name, raw) in land.Options) options[name] = System.Text.Json.Nodes.JsonNode.Parse(raw);
        foreach (var (name, value) in binding.Land) options[name] ??= value;
        options["method"] = method;
        options["squash"] = squash;
        return new BoundLand(method, Text("target"), squash,
            template is null ? null : LandNodes.RenderMessage(template, subject.Title, subject.Quest, ticket, subject.Session), ticket,
            options.ToJsonString(), digest, $"workflow:{binding.Workflow}@{binding.Version}:{Repository}");
    }

    /// <summary>Rows 5 and 6 of D154's table as the version says them; null where no version was read.</summary>
    public WorkflowLook? Look { get; init; }

    /// <summary>Why the version's opinion step cannot start (§3.7): it names reviewers the repository does not declare.</summary>
    public string? OpinionCannot { get; init; }

    /// <summary>Why the version's landing step cannot start (§3.7): it lands on a branch no pattern names.</summary>
    public string? LandingCannot { get; init; }

    /// <summary>Why the named workflow its run was bound to cannot be read here: none of its steps can start, and nothing is swapped in.</summary>
    public string? Problem { get; init; }

    /// <summary>Why it could not be read whether a named workflow governs this work: the service named no chain.</summary>
    public string? Unread { get; init; }

    /// <summary>Whether a saved workflow governs this work: its run was bound to one.</summary>
    public bool Named => Binding is { IsCurrent: false };

    /// <summary>How the work is said to follow it: <c>`docs-to-pr` v2</c>, or no workflow.</summary>
    public string Name => Binding is { IsCurrent: false } named
        ? named.Version is { } version ? $"`{named.Workflow}` v{version}" : $"`{named.Workflow}`"
        : "no workflow";

    /// <summary>What the landing record keeps of it (design §5.5): the version that decided; null where none was read.</summary>
    public LandingWorkflow? Record => Binding is { IsCurrent: false, Version: { } version } named && Problem is null && Unread is null
        ? new LandingWorkflow(named.Workflow, version, named.Level) { Kind = named.Kind }
        : null;

    /// <inheritdoc/>
    public string? BoundRun => Binding?.Run;

    /// <inheritdoc/>
    public string Decided => Binding is { } binding ? WorkflowRunBinding.Why(binding) : "since nothing names a workflow";

    /// <inheritdoc/>
    public string LandingSays => WorkflowGate.LandingSays(this);

    /// <inheritdoc/>
    public string? WorkChecks => (string?)Steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Work)?.Field("checks");

    /// <summary>
    /// What the place declares, before any workflow: the review rule's environments and the opinion rule's reviewers, which a graph's look and
    /// opinion choose among. It lands nothing of its own: its landing is the merge a graph's <c>land</c> replaces.
    /// </summary>
    public static WorkflowProcess Declared(DriverConfig config, string repository, string? workspace) =>
        new(repository, Landing.Merge, ReviewRules.Resolve(config, repository, workspace),
            OpinionRules.Resolve(config, repository, workspace));

    /// <summary>Why work that follows no saved workflow is held: nothing runs or lands it.</summary>
    public static string NoWorkflow(string repository) =>
        $"no workflow is chosen for `{repository}`, so nothing runs or lands its work: Set up… chooses one.";

    /// <summary>
    /// A graph version holding a <c>land</c> as a process (WORKFLOWCLEAN1 1c, design §2.6): the landing is the intent it binds
    /// (<see cref="LandNodes.LandingOf"/>) and the gates are read from the node options by <see cref="GraphGates.Of"/>, no step between.
    /// </summary>
    public static WorkflowProcess Of(DriverConfig config, string repository, string? workspace, WorkflowRunBinding binding, GraphGateReading gates, Landing bound)
    {
        var declared = Declared(config, repository, workspace);
        return declared with
        {
            Landing = bound,
            Opinion = gates.Opinion,
            Binding = binding,
            Look = gates.Look,
            OpinionCannot = gates.OpinionCannot,
            LandingCannot = BoundCannot(bound),
        };
    }

    private static string? BoundCannot(Landing bound) =>
        bound.Form == LandingForm.Branch && bound.Pattern is null
            ? "its `land` puts the work on a branch, and neither the node nor the bind names a pattern for it."
            : null;

    /// <summary>The look (D154 rows 5 and 6): its presence is <c>required</c>, in an environment the review rule declares.</summary>
    internal static WorkflowLook ReadLook(WorkflowProcess declared, string repository, string? id, string? asked)
    {
        if (id is null) return new WorkflowLook(null, null);
        if (declared.Review is not { Rule.IsNone: false } reviews)
        {
            return new WorkflowLook(asked, $"its look `{id}` reads the work in {(asked is null ? "an environment" : $"`{asked}`")}, and "
                + $"`{repository}` declares no review environment: `daoris place review {repository} {asked ?? "<environment>"} --kind "
                + "local|deployed --procedure <path>` declares one.");
        }

        var environment = asked ?? reviews.Rule.Environments[0].Name;
        return reviews.Rule.Environments.Any(each => each.Name == environment)
            ? new WorkflowLook(environment, null)
            : new WorkflowLook(environment, $"its look `{id}` reads the work in `{environment}`, which `{repository}` does not declare (it declares "
                + $"{And(reviews.Rule.Environments.Select(each => each.Name))}): `daoris place review {repository} {environment} --kind "
                + "local|deployed --procedure <path>` declares it.");
    }

    /// <summary>The second opinion: when it reads, whether it is required and read again the node's; which reviewers among those declared.</summary>
    internal static (ResolvedOpinion? Opinion, string? Cannot) ReadOpinion(
        WorkflowProcess declared, string repository, string id, IReadOnlyList<string>? named, bool onSteps, bool required, bool recheck)
    {
        IReadOnlyList<string> on = onSteps ? [OpinionRules.Landing, OpinionRules.Steps] : [OpinionRules.Landing];
        if (declared.Opinion is not { Rule.IsNone: false } listed)
        {
            return (new ResolvedOpinion(new OpinionRule(named ?? [], on, required, Recheck: recheck), declared.Opinion?.Source ?? OpinionSource.Repository),
                $"its second opinion `{id}` reads with reviewers `{repository}` declares, and it declares none: "
                + $"`daoris place opinion {repository} --agents <adapter,adapter>` declares them.");
        }

        string? cannot = null;
        var beyond = (named ?? []).Where(each => !listed.Rule.Reviewers.Contains(each, StringComparer.OrdinalIgnoreCase)).ToList();
        if (beyond.Count > 0)
        {
            cannot = $"its second opinion `{id}` names {And(beyond)}, which `{repository}` does not declare among its reviewers "
                + $"({And(listed.Rule.Reviewers)}): a step chooses among them, and `daoris place opinion {repository} --agents "
                + "<adapter,adapter>` declares more.";
        }

        // Named reviewers in the step's order, spelled as the rule declares them, since the walk finds them by that name.
        var chosen = named is null
            ? listed.Rule.Reviewers
            : [.. named.Select(each => listed.Rule.Reviewers.FirstOrDefault(name => string.Equals(name, each, StringComparison.OrdinalIgnoreCase)) ?? each)];
        return (new ResolvedOpinion(listed.Rule with { Reviewers = chosen, On = on, Required = required, Recheck = recheck }, listed.Source), cannot);
    }

    /// <summary>Names in backticks, the last after <c>and</c>.</summary>
    internal static string And(IEnumerable<string> names)
    {
        var ticked = names.Select(name => $"`{name}`").ToList();
        return ticked.Count <= 1 ? string.Concat(ticked) : $"{string.Join(", ", ticked.Take(ticked.Count - 1))} and {ticked[^1]}";
    }
}

/// <summary>
/// The person's part in a workflow, compared (WORKFLOW1f, D157 point 10; the workflow design §4.3–§4.4): lowering is a table, not a
/// reading. A look or an opinion dropped, an opinion made not required, a landing or a stage made automatic, a go-ahead removed or a
/// pull request skipped each lower it; raising one never does. WORKFLOW1i's intake reads the same table. The routes' and the
/// verification pipelines' rows are <see cref="InvolvementRoutes"/>'s (FABRIC-PIPELINE1a).
/// </summary>
public static class Involvement
{
    /// <summary>
    /// How <paramref name="chosen"/> asks less of the person than <paramref name="otherwise"/> (design §4.3), each said as what it
    /// does; empty where it lowers nothing.
    /// </summary>
    public static IReadOnlyList<string> Lowers(IReadOnlyList<NamedStep> chosen, IReadOnlyList<NamedStep> otherwise)
    {
        NamedStep? Of(IReadOnlyList<NamedStep> steps, string kind) => steps.FirstOrDefault(step => step.Kind == kind);
        var lowered = new List<string>();
        if (Of(otherwise, WorkflowKinds.Look) is not null && Of(chosen, WorkflowKinds.Look) is null) lowered.Add("it drops your look");

        var opinion = Of(otherwise, WorkflowKinds.Opinion);
        var kept = Of(chosen, WorkflowKinds.Opinion);
        if (opinion is not null && kept is null) lowered.Add("it drops the second opinion");
        else if ((bool?)opinion?.Field("required") == true && (bool?)kept?.Field("required") != true)
        {
            lowered.Add("it no longer requires the second opinion");
        }

        if ((string?)Of(otherwise, WorkflowKinds.Landing)?.Field("accept") == "you" && (string?)Of(chosen, WorkflowKinds.Landing)?.Field("accept") == "automatic")
        {
            lowered.Add("it lands with no press of yours");
        }

        if (Of(otherwise, WorkflowKinds.PullRequest) is not null && Of(chosen, WorkflowKinds.PullRequest) is null) lowered.Add("it skips the pull request");

        var acts = All(chosen).Where(step => step.Kind == WorkflowKinds.GoAhead).Select(step => (string?)step.Field("act")).ToHashSet(StringComparer.Ordinal);
        foreach (var goAhead in All(otherwise).Where(step => step.Kind == WorkflowKinds.GoAhead && !acts.Contains((string?)step.Field("act"))))
        {
            lowered.Add($"it removes the go-ahead \"{goAhead.Field("act")}\"");
        }

        // FABRIC-PIPELINE1b (the pipeline design §1.3's fourth row): light leaves the heavy gates to a verification, and no route runs one
        // in this build (FABRIC-PIPELINE1a's `landed` route is its row), so light lowers from anything else.
        if ((string?)Of(chosen, WorkflowKinds.Work)?.Field("checks") == SessionChecks.Light
            && (string?)Of(otherwise, WorkflowKinds.Work)?.Field("checks") != SessionChecks.Light)
        {
            lowered.Add("its sessions leave the heavy gates to a verification nothing here runs");
        }

        var started = All(chosen).Where(step => step.Kind == WorkflowKinds.Stage && (string?)step.Field("start") == "automatic")
            .Select(step => (string?)step.Field("stage")).ToHashSet(StringComparer.Ordinal);
        foreach (var stage in All(otherwise).Where(step => step.Kind == WorkflowKinds.Stage && (string?)step.Field("start") == "you"
                     && started.Contains((string?)step.Field("stage"))))
        {
            lowered.Add($"it starts `{stage.Field("stage")}` with no press of yours");
        }

        // INTEGRATE1n (§17.6 point 9, C29): the graph-only kinds have no step of their own, so each is a part of its own, matched by its node's
        // id, which a version keeps (§2.5). An `integrate` whose `request` moves off the owner (none is the owner's, the install's table) to
        // `done` or `steward` asks you for nothing where it asked for your card; moving among those, adding and removing one lower nothing.
        foreach (var before in otherwise.Where(step => step.Kind == NodeKinds.Integrate && RequestOf(step) == LandingIntegrate.Owner))
        {
            if (chosen.FirstOrDefault(step => step.Kind == NodeKinds.Integrate && step.Id == before.Id) is { } after
                && RequestOf(after) is LandingIntegrate.Done or LandingIntegrate.Steward)
            {
                lowered.Add("it requests the integration with no press of yours");
            }
        }

        // INTEGRATE1o (§17.6 point 9, C29): a `work.requests` the chosen version no longer holds under its node id. It held the work on the quests
        // its sessions published and asked you about one declined, so its removal asks you for less; one kept or added lowers nothing.
        var held = chosen.Where(step => step.Kind == NodeKinds.WorkRequests).Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        if (otherwise.Any(step => step.Kind == NodeKinds.WorkRequests && !held.Contains(step.Id)))
        {
            lowered.Add("it no longer holds the work on its requests, nor asks you about one declined");
        }

        // LANDNODE1k: a `quest.holds` the chosen version no longer holds under its node id, or holds with its `yes` off, lands a done held for your
        // yes; one kept, or added, lowers nothing.
        foreach (var before in otherwise.Where(step => step.Kind == NodeKinds.QuestHolds && step.Field(HoldNodes.Yes) is not false))
        {
            if (chosen.FirstOrDefault(step => step.Kind == NodeKinds.QuestHolds && step.Id == before.Id) is not { } after || after.Field(HoldNodes.Yes) is false)
            {
                lowered.Add("it lands a done held for your yes");
            }
        }

        // INTEGRATE1p (§17.6 point 9, C29): a `request` that sends with no press of yours where, under its node id, it waited for your Send or
        // was not there. Each is named, so of two requests only the one moved is said; one removed, or added with `start: you`, lowers nothing.
        foreach (var after in chosen.Where(step => step.Kind == NodeKinds.Request && StartOf(step) == RequestOptions.Automatic))
        {
            if (otherwise.FirstOrDefault(step => step.Kind == NodeKinds.Request && step.Id == after.Id) is { } before
                && StartOf(before) == RequestOptions.Automatic)
            {
                continue;
            }

            lowered.Add($"it sends `{after.Field("title")}` to `{after.Field("to")}` with no press of yours");
        }

        return lowered;
    }

    /// <summary>A <c>request</c> step's <c>start</c>, the person's <c>you</c> where it names none.</summary>
    private static string StartOf(NamedStep step) => step.Field("start") as string ?? RequestOptions.You;

    /// <summary>An <c>integrate</c> step's <c>request</c>, the owner's where it names none or a value that is no choice (the install's table).</summary>
    private static string RequestOf(NamedStep step) =>
        step.Field("request") is string request && LandingIntegrate.Requests.Contains(request) ? request : LandingIntegrate.Owner;

    /// <summary>
    /// A graph version's person part as the steps <see cref="Lowers"/> reads (FABRIC-CUTOVER1e; D190 point 4, the cutover design §11.1): its
    /// gate and go-ahead nodes in the graph's order, each as the step of its kind, its options as that step's fields (the work's checks, the
    /// opinion's switches, the look's environment, the landing's form and press, each go-ahead's act). A plugin's node, a person's task, a
    /// stop and every other node is no part of it, as no step stood for one; except the graph-only kinds the comparison reads by their node's
    /// id (INTEGRATE1n: <c>integrate</c>; INTEGRATE1o: <c>work.requests</c>, each kept under its own kind). An option absent is a field absent, so a graph that leaves the
    /// landing's press to the repository's rule lowers nothing by it, as 1d reads that press from the rule.
    /// </summary>
    public static IReadOnlyList<NamedStep> StepsOf(NodeGraph graph) =>
    [
        .. graph.Nodes.Select(node =>
        {
            var kind = node.Kind switch
            {
                NodeKinds.Claimed => WorkflowKinds.Work,
                NodeKinds.Opinion => WorkflowKinds.Opinion,
                NodeKinds.Look => WorkflowKinds.Look,
                NodeKinds.Land => WorkflowKinds.Landing,
                NodeKinds.GoAhead => WorkflowKinds.GoAhead,
                // INTEGRATE1n: a graph-only kind, kept by its own kind and its node's id for the comparison's graph part (<see cref="Lowers"/>).
                NodeKinds.Integrate => NodeKinds.Integrate,
                NodeKinds.WorkRequests => NodeKinds.WorkRequests,
                NodeKinds.Request => NodeKinds.Request,
                // LANDNODE1k: the quest's holds, by its node id, whose `yes` the comparison reads.
                NodeKinds.QuestHolds => NodeKinds.QuestHolds,
                // A plugin's pull-request step (G8) is the person's merge on the platform, named by the plugin that opens it.
                _ when PullRequestSteps.PluginOf(node.Kind) is not null => WorkflowKinds.PullRequest,
                _ => null,
            };
            if (kind == WorkflowKinds.PullRequest) return new NamedStep(node.Id, kind, [new WorkflowSetting("plugin", PullRequestSteps.PluginOf(node.Kind))]);
            // LANDNODE1c2: a `land`'s press is the graph's `review`, so a review taken away lowers as an `accept` moved to automatic did.
            IEnumerable<WorkflowSetting> pressed = node.Kind == NodeKinds.Land
                ? [new WorkflowSetting(LandNodes.AcceptOption, LandNodes.Reviewed(graph) ? "you" : "automatic")]
                : [];
            return kind is null ? null
                : new NamedStep(node.Id, kind, [.. node.Options.Select(option => new WorkflowSetting(option.Key, NodeOptions.Value(option.Value))), .. pressed]);
        }).OfType<NamedStep>(),
    ];

    private static IEnumerable<NamedStep> All(IReadOnlyList<NamedStep> steps) => steps.Concat(steps.SelectMany(step => step.Members));
}

/// <summary>
/// What a run's kind paths came to at the gate (design §4.4): what its workflow lowers against the one it would follow without the
/// kind, that one's name, and the paths the chain changed, or null where git could not say.
/// </summary>
/// <param name="Lowered">How the kind's workflow asks less of the person (<see cref="Involvement.Lowers"/>); empty where it lowers nothing.</param>
/// <param name="Otherwise">The workflow the work would follow without the kind, as said.</param>
/// <param name="Changed">The paths the chain changed, from where its work leaves the line to its tip; null where git could not say.</param>
public sealed record KindPathsRead(IReadOnlyList<string> Lowered, string Otherwise, IReadOnlyList<string>? Changed);

/// <summary>
/// The run's own part of the landing gate (WORKFLOW1f; the workflow design §3.7, §4.4–§4.5, §5.2): the process it read, and what of
/// the run's own holds the work before the second opinion and the look.
/// </summary>
/// <param name="State">One of <see cref="WorkflowGateStates"/>.</param>
public sealed record WorkflowGateState(string State, WorkflowProcess Process) : IWorkflowGatePart
{
    /// <summary>The working session the door lands, which the terminal's doors name.</summary>
    public string? Session { get; init; }

    /// <summary>The changed paths outside the kind's, for <see cref="WorkflowGateStates.KindPaths"/>.</summary>
    public IReadOnlyList<string> Outside { get; init; } = [];

    /// <summary>How the kind's workflow asks less of the person, where the paths were checked.</summary>
    public IReadOnlyList<string> Lowered { get; init; } = [];

    /// <summary>The workflow the work would follow without its kind, where the paths were checked.</summary>
    public string? Otherwise { get; init; }

    /// <summary>Why it could not be read, for <see cref="WorkflowGateStates.Unread"/>.</summary>
    public string? Problem { get; init; }

    /// <summary>Whether the run's own part lets the work on to the second opinion and the look.</summary>
    public bool LetsGo => State is WorkflowGateStates.None or WorkflowGateStates.Follows or WorkflowGateStates.Kept;

    /// <summary>What it says, in the driver's words: the terminal's line and every door's refusal.</summary>
    public string Says => WorkflowGate.Says(this);

    /// <summary>Whether it has anything to say: its state is not <see cref="WorkflowGateStates.None"/>.</summary>
    public bool Speaks => State != WorkflowGateStates.None;

    /// <summary>The process the part was read with, as the review's gate reads it.</summary>
    IWorkplaceProcess IWorkflowGatePart.Process => Process;

    /// <summary>The machine log's line for a landing this part held: a landing step that cannot start is told as such (see <see cref="WorkflowLines.Held"/>).</summary>
    LandingLine IWorkflowGatePart.Held(string session, string? repository, string? workspace, string door) =>
        WorkflowLines.Held(session, repository, workspace, this, door, LetsGo ? WorkflowKinds.Landing : null);
}

/// <summary>
/// The run's own part of the landing gate (WORKFLOW1f, D157 points 6, 10 and 11; the workflow design §3.7, §4.4–§4.6, §5): whether
/// the workflow its run was bound to can be read here, and whether a kind's paths hold. A table: the binding, the version, git's
/// changed paths and the person's say-so, and no model reads any of it (§9).
/// </summary>
/// <remarks>
/// <para><b>One gate, in its order</b> (D155 §7, design §2.2, §4.4): the content the line already holds, then the run's own part,
/// then the second opinion, then the person's look, then the landing step's own declarations. Every landing door reads it through
/// <see cref="SessionTreeGates.GateAsync"/>, and <see cref="SessionTrees.LandAsync"/> refuses the first part that holds.</para>
///
/// <para><b>A kind's paths</b> (§4.4) are checked only where the kind chose the workflow (its level is the repository's or the
/// workspace's for the kind), the kind declares paths, and its workflow asks less of the person than the one the work would follow
/// without the kind. Inside them the work goes on; outside them it holds for the person, whose <i>Keep</i> is kept on the run. It
/// never switches by itself, and nothing infers a kind from the paths.</para>
/// </remarks>
public static class WorkflowGate
{
    /// <summary>
    /// The run's own part for a process and, where its kind's paths were read, what they came to (design §4.4): Current is today's;
    /// a workflow that cannot be read holds; a kind's paths hold where the kind's workflow lowers the person's part and the chain
    /// changed a path outside them, unless the person kept it.
    /// </summary>
    public static WorkflowGateState Judge(WorkflowProcess process, KindPathsRead? paths = null)
    {
        if (process.Unread is { } unread) return new WorkflowGateState(WorkflowGateStates.Unread, process) { Problem = unread };
        if (!process.Named) return new WorkflowGateState(WorkflowGateStates.None, process);
        if (process.Problem is not null) return new WorkflowGateState(WorkflowGateStates.CannotStart, process);
        if (paths is null || paths.Lowered.Count == 0) return new WorkflowGateState(WorkflowGateStates.Follows, process);

        var read = new WorkflowGateState(WorkflowGateStates.Follows, process) { Lowered = paths.Lowered, Otherwise = paths.Otherwise };
        if (process.Binding!.Kept is not null) return read with { State = WorkflowGateStates.Kept };
        if (paths.Changed is not { } changed)
        {
            return read with
            {
                State = WorkflowGateStates.Unread,
                Problem = "git could not say which paths this work changed, so whether it keeps to its kind's paths is not known.",
            };
        }

        var outside = KindPaths.Outside(process.Binding.Paths, changed);
        return outside.Count == 0 ? read : read with { State = WorkflowGateStates.KindPaths, Outside = outside };
    }

    /// <summary>Whether a kind's paths are checked for this run (design §4.4): the kind chose its workflow, and declares paths.</summary>
    public static bool ChecksPaths(WorkflowProcess process) =>
        process is { Named: true, Problem: null, Binding: { Level: WorkflowLevels.RepositoryKind or WorkflowLevels.WorkspaceKind, Paths.Count: > 0 } };

    /// <summary>What the run's own part says (design §3.7, §4.4, §5.2): what holds it and the door that moves it, or what it follows.</summary>
    public static string Says(WorkflowGateState gate)
    {
        var process = gate.Process;
        var binding = process.Binding;
        var why = binding is null ? "" : $", {WorkflowRunBinding.Why(binding)}";
        return gate.State switch
        {
            WorkflowGateStates.None => "It follows Current: the rules as they stand, read at each gate.",
            WorkflowGateStates.Follows => $"It follows {process.Name}{why}." + (gate.Lowered.Count > 0 ? " It keeps to its kind's paths." : ""),
            WorkflowGateStates.Kept => $"It follows {process.Name}{why}: you kept it for this work, though it changed paths outside its kind's"
                + (binding?.Kept?.Words is { Length: > 0 } words ? $", saying: \"{words}\"" : "") + ".",
            WorkflowGateStates.CannotStart =>
                $"Its run was bound to {process.Name}{why}, which cannot start it here: {process.Problem} Nothing lands until it can be read "
                + $"(`daoris workflow show {binding!.Workflow}` says what is wrong with it), and no other workflow is followed in its place.",
            WorkflowGateStates.KindPaths =>
                $"Holds: this work changed {Plural(gate.Outside.Count, "path")} outside {Kind(binding!)}'s ({WorkflowProcess.And(binding!.Paths)}): "
                + $"{Listed(gate.Outside)}; and {process.Name}, chosen for it, asks less of you than {gate.Otherwise} would without the kind "
                + $"({string.Join("; ", gate.Lowered)}). `daoris workflow keep {gate.Session ?? "<session>"} \"…\"` keeps {process.Name} for "
                + "this work with your words; nothing switches by itself.",
            _ => $"Whether this work's workflow holds it could not be read: {gate.Problem} Nothing lands until it can be.",
        };
    }

    /// <summary>What a landing step that cannot start says (design §3.7): the door that declares what it needs.</summary>
    public static string LandingSays(WorkflowProcess process) =>
        $"It follows {process.Name}, and {process.LandingCannot} Nothing was landed.";

    /// <summary>The kind as said: its label, or its id.</summary>
    private static string Kind(WorkflowRunBinding binding) => binding.Label ?? $"`{binding.Kind}`";

    /// <summary>The first three paths, then how many more.</summary>
    private static string Listed(IReadOnlyList<string> paths) =>
        string.Join(", ", paths.Take(3).Select(path => $"`{path}`")) + (paths.Count > 3 ? $" and {paths.Count - 3} more" : "");

    private static string Plural(int count, string noun) =>
        count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? noun : noun + "s");
}

/// <summary>
/// The machine log's workflow lines (WORKFLOW1f, the workflow design §5.5, D94): <c>workflow.chosen</c> and <c>workflow.held</c>,
/// codes, ids and counts only, never a sentence, a path or anyone's words. They ride the landing line's channel
/// (<see cref="ServiceClient.LandingSaid"/>), which writes the event each names.
/// </summary>
public static class WorkflowLines
{
    /// <summary>A run bound at its first start: which level chose, whether a named workflow or Current, and its version.</summary>
    public static LandingLine Chosen(WorkflowRunBinding binding) =>
        new("workflow.chosen",
        [
            ("run", binding.Run), ("repository", binding.Repository), ("workspace", binding.Workspace), ("level", binding.Level),
            ("kind", binding.Kind), ("named", !binding.IsCurrent), ("workflow", binding.IsCurrent ? null : binding.Workflow),
            ("version", binding.Version), ("problem", binding.Problem is not null),
        ]);

    /// <summary>A landing the run's own part held: whose, where, its code, the step that cannot start, and the door it was tried at.</summary>
    /// <param name="step">The kind of step that cannot start, for a landing step's own; null for the run's.</param>
    public static LandingLine Held(string session, string? repository, string? workspace, WorkflowGateState gate, string door, string? step = null) =>
        new("workflow.held",
        [
            ("session", session), ("repository", repository), ("workspace", workspace),
            ("code", step is null ? gate.State : WorkflowGateStates.CannotStart), ("step", step),
            ("workflow", gate.Process.Named ? gate.Process.Binding!.Workflow : null), ("version", gate.Process.Binding?.Version),
            ("outside", gate.State == WorkflowGateStates.KindPaths ? gate.Outside.Count : null), ("door", door),
        ]);

    /// <summary>
    /// A landing a run's graph part held (FABRIC-CUTOVER1d): whose, where, <c>graph</c> as its code and the part's state, the workflow and its
    /// version, the node <c>handed</c> waits on where it waits, and the door it was tried at. Ids and codes, never a sentence.
    /// </summary>
    public static LandingLine GraphHeld(string session, string? repository, string? workspace, WorkflowProcess? process, GraphGateState graph, string door) =>
        new("workflow.held",
        [
            ("session", session), ("repository", repository), ("workspace", workspace), ("code", "graph"), ("graph", graph.State),
            ("workflow", process?.Named == true ? process.Binding!.Workflow : null), ("version", process?.Binding?.Version), ("node", graph.Node),
            ("door", door),
        ]);
}
