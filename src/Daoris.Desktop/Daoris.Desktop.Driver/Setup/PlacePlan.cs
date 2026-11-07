namespace Daoris.Driver;

/// <summary>
/// What a place's quick set-up asks: the repositories, a delivery preset with its second opinion
/// and your look, adoption with what it builds, and general driving, three parts kept apart.
/// </summary>
public sealed record PlaceAsk(IReadOnlyList<string> Repositories, string Preset)
{
    public bool Opinion { get; init; }
    public bool Look { get; init; }
    public bool Adoption { get; init; }
    public string? Builds { get; init; }
    public bool Drive { get; init; }

    /// <summary>
    /// How many adoption quests the driver keeps open at once; an adoption covering more of a workspace's repositories than this is paced by that
    /// workspace's plan. None said is the plan's default, one.
    /// </summary>
    public int? AtOnce { get; init; }

    /// <summary>How many adoptions go alone before the paced plan pauses itself; none said is the workspace plan's default.</summary>
    public int? Pilot { get; init; }
}

/// <summary>What a place's plan reads: the service's registry and quests, and each repository's line as its checkout here holds it.</summary>
/// <remarks>A port, so the plan is held without a service or git; <see cref="PlaceWorld"/> is the real one. It holds no door that writes.</remarks>
public interface IPlaceWorld
{
    Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct);

    Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct);

    /// <summary>The line's tip and its declaration, read as git objects, and the host of its <c>origin</c>; never a fetch.</summary>
    Task<PlaceTip> TipAsync(RepoView repository, DriverConfig config, CancellationToken ct);
}

/// <summary>
/// A repository's line as its checkout here holds it: the line, the commit at its tip, the declaration's blob there (<c>none</c> where it
/// holds none) and its text, and the host of its remote; or why the line could not be read.
/// </summary>
public sealed record PlaceTip(string? Line, string? Commit, string? Gates, string? Text, string? Host, string? Problem = null);

/// <summary>A work's consent as <see cref="WorkPolicies.Of"/> resolves it at the repository, where it was set, or why it did not resolve.</summary>
public sealed record PlacePolicy(string Work, string? Consent, string Source, string? Failed);

/// <summary>A repository read through the resolvers, before the plan or after it: driving, the land its work follows and the preset it matches.</summary>
public sealed record PlaceSide(
    bool Driven, bool Held, bool Trees, SetupLand? Land, bool Opinion, bool Look, SetupMatch Match, PlaceValues Values, IReadOnlyList<PlacePolicy> Policies);

/// <summary>One change a part makes through an existing door (<see cref="Key"/>, its route), said without the repository it is made at.</summary>
public sealed record PlaceChange(string Key, string Part, string Said, string Before, string After);

/// <summary>A change and every repository it is made at, said once.</summary>
public sealed record PlaceChanged(PlaceChange Change, IReadOnlyList<string> Repositories);

/// <summary>A refusal: the repository it is of (null for the ask's own), the part, a code a screen keys on, and the sentence naming its door.</summary>
public sealed record PlaceRefusal(string? Repository, string Part, string Code, string Sentence);

/// <summary>An acceptance a plan may carry: its purpose (<c>landed</c> or <c>integration</c>), where it stands now, and what accepting does.</summary>
public sealed record PlaceAcceptance(string Purpose, string Standing, string Said);

/// <summary>The declaration at a repository's line's tip: its line, commit, blob and text, and the acceptances it is offered for.</summary>
public sealed record PlaceDeclaration(string Line, string Commit, string Blob, string? Text, IReadOnlyList<PlaceAcceptance> Acceptances);

/// <summary>A queued quest enabling driving would release, by its title.</summary>
public sealed record PlaceReleased(string Quest, string Title);

/// <summary>What the page preselects: the delivery read from the driver's facts and why, and adoption for a repository that has not adopted.</summary>
public sealed record PlaceSuggestion(string Preset, bool Adoption, SuggestionWhy Why);

/// <summary>One repository of a plan: before and after, the changes it makes there, what lowers your part, the workflows' reach, and the rest.</summary>
public sealed record PlaceRepository(
    string Repository, string Workspace, PlaceSide Before, PlaceSide After, IReadOnlyList<PlaceChange> Changes, IReadOnlyList<string> Lowers,
    IReadOnlyList<WorkflowReach> Reach, IReadOnlyList<PlaceReleased> Released, PlaceDeclaration? Declaration, string? Unread, PlaceSuggestion Suggested);

/// <summary>A plan: what was asked, each repository, the changes each said once, every refusal, and the terminal's twin of it.</summary>
public sealed record PlacePlanned(
    PlaceAsk Asked, IReadOnlyList<PlaceRepository> Repositories, IReadOnlyList<PlaceChanged> Changes, IReadOnlyList<PlaceRefusal> Refusals, string Terminal)
{
    /// <summary>The copies of shipped defaults the home saves first, each once, however many repositories choose it.</summary>
    public IReadOnlyList<PlaceSave> Saves { get; init; } = [];

    /// <summary>Whether nothing is refused: an apply could take it.</summary>
    public bool Applies => Refusals.Count == 0;

    /// <summary>What an apply is pinned to (<see cref="PlaceDigests.Of"/>): the plan's changes and declarations, said once.</summary>
    public string Digest => PlaceDigests.Of(this);
}

/// <summary>The parts applied to one repository's copy of the configuration: the copy, what each part changes there, and its refusals.</summary>
public sealed record PlaceApplied(DriverConfig After, IReadOnlyList<PlaceChange> Changes, IReadOnlyList<PlaceRefusal> Refusals)
{
    /// <summary>The copies of shipped defaults the home must save first (<c>WORKFLOW_SAVE</c>); the choice refers to them.</summary>
    public IReadOnlyList<PlaceSave> Saves { get; init; } = [];

    /// <summary>The home's workflows with those copies in them, unwritten: what the plan's after reads the followed workflow through.</summary>
    internal IWorkflowStore? Store { get; init; }
}

/// <summary>A copy of a shipped default to save: its id, the default it copies, and the gates its variant adds.</summary>
public sealed record PlaceSave(string Id, string Default, bool Opinion, bool Look);

/// <summary>
/// A place's plan (<c>PLACE_PLAN</c> and <c>daoris place … --plan</c>): the parts applied to a
/// copy of the configuration through today's doors' own edits (driving, trees, the workflow choice, the landing rule by
/// the line's policy, the second opinion and the review rule), read before and after through the resolvers a start uses
/// (<see cref="PlaceProjection.ValuesAt"/>, <see cref="WorkflowProcesses.Otherwise(IWorkflowStore, DriverConfig, string, string?, string?)"/>,
/// <see cref="WorkPolicies.Of"/>). It says every refusal at once, what lowers the person's part, the workflows' reach, the work driving releases or,
/// with adoption and driving off, that the set-up quest starts alone, and each declaration with what accepting it is for. It
/// writes nothing and asks no account, plugin, tool or remote.
/// </summary>
public static class PlacePlans
{
    public const string Delivery = "delivery";
    public const string Driving = "driving";
    public const string Adoption = "adoption";

    /// <summary>The key of the changes an adoption part makes: the set-up press's, which writes no configuration.</summary>
    public const string SetupPressKey = "SETUP_PRESS";

    /// <summary>The key of the change that saves a copy of a shipped default, its own operation in the receipt (<c>WORKFLOW_SAVE</c>).</summary>
    public const string WorkflowSaveKey = "WORKFLOW_SAVE";

    /// <summary>The purposes an acceptance is chosen for, each kept by its own record.</summary>
    public const string Landed = "landed";
    public const string Integration = "integration";

    public const string NoLandingPlugin = "no-landing-plugin";

    /// <summary>What an adoption part says where general driving stays off: the set-up quest is admitted alone.</summary>
    public const string AloneSaid = "what starts here while general driving stays off";

    /// <summary>The ask's own refusals, said before any repository is read: the delivery, what rides it, and what it builds.</summary>
    public static IReadOnlyList<PlaceRefusal> AskRefusals(PlaceAsk ask)
    {
        var refusals = new List<PlaceRefusal>();
        if (ask.Repositories.Count == 0) refusals.Add(new(null, Delivery, "no-place", "name the repositories the plan is for."));
        if (ask.Preset == SetupPresets.Keep)
        {
            // Keeping how it lands rides every tick and driving: nothing of the delivery is asked, so nothing of it is refused.
        }
        else if (SetupPresets.Of(ask.Preset) is not { } preset)
        {
            refusals.Add(new(null, Delivery, "preset",
                $"`{ask.Preset}` is not a delivery here — {string.Join(", ", SetupPresets.Asks.Select(each => $"`{each}`"))}."));
        }
        else if (preset.Land is null)
        {
            if (ask.Drive) refusals.Add(new(null, Driving, "not-driven", "*Not driven* takes no general driving: choose another delivery, or leave driving off."));
            if (ask.Opinion || ask.Look)
            {
                refusals.Add(new(null, Delivery, "not-driven",
                    "a second opinion and your look ride a delivery, and *Not driven* delivers nothing: choose another, or leave them off."));
            }
        }

        if (ask.Builds is { } builds)
        {
            if (!SetupBuilds.Is(builds)) refusals.Add(new(null, Adoption, "builds", SetupBuilds.Problem(builds)));
            else if (!ask.Adoption)
            {
                refusals.Add(new(null, Adoption, "builds", "what it builds shapes only the set-up quest's request: ask for adoption too, or leave it out."));
            }
        }

        return refusals;
    }

    /// <summary>
    /// The pacing an adoption asks (the workspace plan's): <c>atOnce</c> at least one and held to the cap less one so other work keeps a
    /// slot, <c>pilot</c> not negative, and neither without adoption.
    /// </summary>
    public static IReadOnlyList<PlaceRefusal> PacingRefusals(PlaceAsk ask, DriverConfig config)
    {
        var refusals = new List<PlaceRefusal>();
        if (ask.AtOnce is null && ask.Pilot is null) return refusals;

        if (!ask.Adoption)
        {
            refusals.Add(new(null, Adoption, "pacing", "how many set-ups are asked at once and the pilot pace an adoption: ask for adoption too, or leave them out."));
            return refusals;
        }

        var most = WorkspaceSetupRules.MostAtOnce(config.Cap);
        if (ask.AtOnce is { } atOnce && (atOnce < 1 || atOnce > most))
        {
            refusals.Add(new(null, Adoption, "at-once", atOnce < 1
                ? "at least one set-up must be open at once, or the plan would never ask anything."
                : $"{atOnce} at once is more than this machine's cap of {config.Cap} leaves: at most {most}, so other work always keeps a slot. "
                  + "Raise the cap with `daoris settings cap <n>` first, or ask for fewer."));
        }

        if (ask.Pilot is < 0) refusals.Add(new(null, Adoption, "pilot", "the pilot is a number, zero for none."));
        return refusals;
    }

    /// <summary>
    /// The parts applied to <paramref name="row"/>'s copy of <paramref name="config"/>, each through the edit its door makes, only where the
    /// effective value differs; every refusal said, none thrown. The copy is the plan's after, and is never written here.
    /// </summary>
    public static PlaceApplied Apply(
        DriverConfig config, RepoView row, PlaceAsk ask, PlaceTip tip, IWorkplacePluginCatalog plugins, bool unadopted, IReadOnlyList<QuestView> quests,
        string? home = null)
    {
        var name = row.Repository;
        var workspace = RemoteTarget.Workspace(row.Workspace);
        var after = config;
        var changes = new List<PlaceChange>();
        var refusals = new List<PlaceRefusal>();
        void Refuse(string part, string code, string sentence) => refusals.Add(new(name, part, code, sentence));
        bool Driven(DriverConfig each) => each.Drivable.Contains(name, StringComparer.OrdinalIgnoreCase);

        var preset = SetupPresets.Of(ask.Preset);
        var planned = home is null ? null : new PlannedWorkflows(home);
        var saves = new List<PlaceSave>();
        if (ask.Preset == SetupPresets.Keep)
        {
            // Keeping how it lands is a subset of a delivery: no landing and no workflow choice is written, so a saved workflow survives.
            if ((ask.Drive || Driven(after)) && !after.OpensOwnTree(name))
            {
                after = after.WithTrees(name, true);
                changes.Add(new("SET_TREES", Delivery, "a tree per session", "off", "on"));
            }

            // A step is the workflow's, edited at its one door: the ticks choose a copy's variant, and none is kept.
            if (ask.Opinion || ask.Look) Refuse(Delivery, "keeps-workflow", KeepsSteps);
            if (ask.Drive && !Driven(after))
            {
                after = after.WithDrivable(name, true);
                changes.Add(new("SET_DRIVABLE", Driving, "general driving", "off", "on"));
            }
        }
        else if (preset is { Land: null } && Driven(after))
        {
            after = after.WithDrivable(name, false);
            changes.Add(new("SET_DRIVABLE", Delivery, "driven here", "on", "off"));
        }
        else if (preset?.Land is { } land)
        {
            if (!after.OpensOwnTree(name))
            {
                after = after.WithTrees(name, true);
                changes.Add(new("SET_TREES", Delivery, "a tree per session", "off", "on"));
            }

            after = Workflow(after, name, workspace, ask, land, tip, plugins, planned, saves, changes, Refuse);
            if (ask.Drive && !Driven(after))
            {
                after = after.WithDrivable(name, true);
                changes.Add(new("SET_DRIVABLE", Driving, "general driving", "off", "on"));
            }
        }

        if (ask.Adoption)
        {
            // The press's own rules over the configuration the plan would leave, so the two refuse alike.
            foreach (var refusal in SetupPress.ConfigurationRefusals(after, unadopted, row)) Refuse(Adoption, refusal.Code, refusal.Sentence);
            if (SetupPress.OpenRefusal(quests, name) is { } open) Refuse(Adoption, open.Code, open.Sentence);
            changes.Add(new(SetupPressKey, Adoption, "a set-up asked of its own session", "not asked",
                ask.Builds is { } builds && SetupBuilds.Is(builds) ? $"asked, for {SetupBuilds.Named(builds)}" : "asked"));
            // Not driven after the plan, the press admits its quest alone, and the queued work stays held.
            if (!Driven(after)) changes.Add(new(SetupPressKey, Adoption, AloneSaid, "nothing", "its set-up quest alone"));
        }

        return new PlaceApplied(after, changes, refusals) { Saves = saves, Store = planned };
    }

    /// <summary>What a set-up says to a tick under <i>Keep how it lands</i>: a step is added at the workflow's own door.</summary>
    public const string KeepsSteps = "to add a step to the workflow it keeps, use *Edit steps…*.";

    /// <summary>
    /// A delivery as the workflow a place follows: the home's saved copy of the shipped default, in
    /// the variant the ticks choose, saved where the home has none, then chosen through <c>WORKFLOW_USE</c>'s own edit. The ticks refused
    /// where nothing is declared to choose among stay refused. Writes no landing rule.
    /// </summary>
    private static DriverConfig Workflow(
        DriverConfig config, string name, string workspace, PlaceAsk ask, SetupLand land, PlaceTip tip, IWorkplacePluginCatalog plugins,
        PlannedWorkflows? planned, List<PlaceSave> saves, List<PlaceChange> changes, Action<string, string, string> refuse)
    {
        if (land.Method == SetupLand.PullRequest && SetupPresets.PluginFor(tip.Host, plugins) is null)
        {
            refuse(Delivery, NoLandingPlugin, tip.Host is { } host
                ? $"`{name}`'s remote is on `{host}`, and no landing plugin here opens a pull request there: a pull request is its platform's "
                  + "plugin's to open. Add one on Plugins, or choose *A branch to push*."
                : $"`{name}` names no remote here, so there is no platform to open a pull request on: add its `origin`, or choose *A branch to push*.");
            return config;
        }

        var refused = false;
        if (land.Method == LandNodes.Integrate && CanonicalLine.Choose(config, name, workspace, tip.Line).Branch is null)
        {
            refuse(Delivery, "no-line",
                $"`{name}`'s line is not known here, so there is no line to integrate into: name it on its Setup → Line, or with `daoris place line {name} <branch>`.");
            refused = true;
        }

        // The ticks choose among what is declared, never beyond it: the agents that read and the environment a look runs in.
        var reviewersNamed = OpinionRules.Resolve(config, name, workspace) is { Rule.IsNone: false };
        if (ask.Opinion && !reviewersNamed)
        {
            refuse(Delivery, "no-reviewers",
                $"a second opinion names the agents that read `{name}`'s work, and none is set for it: they are yours to name, never guessed. "
                + $"Name them on its Setup → Second opinion, or with `daoris place opinion {name}`.");
            refused = true;
        }

        if (ask.Look && ReviewRules.Resolve(config, name, workspace) is not { Rule.IsNone: false })
        {
            refuse(Delivery, "no-review-environment",
                $"your look needs a review environment for `{name}`, where its work runs for you before it lands, and none is set: add one on "
                + $"its Setup → Review, or with `daoris place review {name}`.");
            refused = true;
        }

        var shipped = WorkflowDefaults.Of(SetupPresets.Of(ask.Preset)!.Id);
        if (refused || shipped is null || planned is null) return config;

        // The copy of the default in the variant the ticks choose: the home's own where it has one, else one saved now.
        var copy = planned.CopyOf(shipped, ask.Opinion, ask.Look);
        if (copy is null)
        {
            bool Taken(string id) => planned.Load(id) is not null;
            var made = WorkflowDefaults.Copy(shipped, ask.Opinion, ask.Look, Taken);
            planned.Write(made.Id, WorkflowOperations.PlanGraph(planned.Home, null, made.Id, null, made.Name, "screen", PlannedWorkflows.At, made.Graph, made.From).Plan.Result!);
            var save = new PlaceSave(made.Id, shipped.Id, ask.Opinion, ask.Look);
            if (!saves.Contains(save)) saves.Add(save);
            changes.Add(new("WORKFLOW_SAVE", Delivery, "a saved copy of the workflow Daoris ships", "none", $"`{made.Id}`"));
            copy = made.Id;
        }

        var chosen = WorkflowSelection.Resolve(config, name, workspace, task: null).Workflow;
        if (chosen != copy)
        {
            try
            {
                config = WorkflowSelection.Apply(config, new WorkflowUse(copy, name, null, null));
                changes.Add(new("WORKFLOW_USE", Delivery, "the workflow its work follows", chosen == WorkflowSelection.Current ? "Current" : $"`{chosen}`", $"`{copy}`"));
            }
            catch (DriverException failed)
            {
                refuse(Delivery, "workflow", failed.Message);
                return config;
            }
        }

        // An integration needs the line's policy, however the line is integrated: the default one where the line has none.
        if (land.Method == LandNodes.Integrate && config.LinePolicyOf(name, workspace) is null)
        {
            config = config.WithLinePolicy(name, new LandingIntegrate());
            changes.Add(new("SET_LINE", Delivery, "how its line integrates", "no policy", "the default policy"));
        }

        return config;
    }

    /// <summary>
    /// The plan over <paramref name="ask"/>'s repositories: each read before and after, its changes, what lowers the person's part, the workflows'
    /// reach, the work driving releases and its declaration; the changes made at several repositories said once. Reads the registry, the
    /// quests where driving or adoption asks them, and each line's tip; writes nothing.
    /// </summary>
    public static async Task<PlacePlanned> PlanAsync(
        IPlaceWorld world, DriverConfig config, string home, IWorkplacePluginCatalog plugins, bool unadopted, PlaceAsk ask, CancellationToken ct = default)
    {
        var names = ask.Repositories.Select(each => each.Trim()).Where(each => each.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        ask = ask with { Repositories = names };
        var refusals = AskRefusals(ask).ToList();
        refusals.AddRange(PacingRefusals(ask, config));
        var planned = refusals.Count == 0;
        var registry = await world.RegistryAsync(ct).ConfigureAwait(false);
        IReadOnlyList<QuestView> quests = ask.Drive || ask.Adoption ? await world.QuestsAsync(ct).ConfigureAwait(false) : [];
        var store = WorkflowPorts.Store(home);
        WorkflowOperations.RegistryRead read = _ =>
            Task.FromResult(new ChoicesRegistry([.. registry.Select(each => (each.Repository, (string?)each.Workspace, each.Root))], null));

        var rows = new List<PlaceRepository>();
        var saves = new List<PlaceSave>();
        var made = new List<(PlaceChange Change, string Repository)>();
        foreach (var name in names)
        {
            if (registry.FirstOrDefault(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase)) is not { } row)
            {
                var missing = SetupPress.NotOnRegistry(name);
                refusals.Add(new(name, Delivery, missing.Code, missing.Sentence));
                continue;
            }

            var workspace = RemoteTarget.Workspace(row.Workspace);
            var tip = await world.TipAsync(row, config, ct).ConfigureAwait(false);
            var applied = planned ? Apply(config, row, ask, tip, plugins, unadopted, quests, home) : new PlaceApplied(config, [], []);
            refusals.AddRange(applied.Refusals);
            saves.AddRange(applied.Saves.Where(save => !saves.Contains(save)));
            var before = SideOf(config, row.Repository, workspace, store);
            var after = SideOf(applied.After, row.Repository, workspace, applied.Store ?? store);
            var reach = await WorkflowOperations.PlaceReachAsync(applied.After, home, read, row.Repository, null, ct).ConfigureAwait(false);
            refusals.AddRange(reach.Where(each => each.Cannots.Count > 0).Select(each => new PlaceRefusal(row.Repository, Delivery, "reach", each.Said!)));
            IReadOnlyList<PlaceReleased> released = !before.Driven && after.Driven && !after.Held
                ? [.. quests.Where(quest => quest.Status == "Open" && string.Equals(quest.To, row.Repository, StringComparison.OrdinalIgnoreCase))
                    .Select(quest => new PlaceReleased(quest.Id, quest.Short ?? quest.Title))]
                : [];
            var declares = tip.Text is { } text && IntegrationDeclaration.Read(text).Facts is not null;
            // A delivery is the person's choice where it is driven by a rule set somewhere; the default rule chose nothing.
            var chosen = before.Driven && before.Values.Landing.Source != PlaceSource.Default;
            var (suggested, why) = SetupPresets.Suggestion(before.Match, chosen, declares);
            rows.Add(new PlaceRepository(
                row.Repository, workspace, before, after, applied.Changes, LowersOf(store, config, applied, row.Repository, workspace), reach, released,
                DeclarationOf(tip, applied.After, row.Repository, workspace, after), tip.Problem,
                new PlaceSuggestion(suggested, !row.Adopted, why)));
            made.AddRange(applied.Changes.Select(change => (change, row.Repository)));
        }

        var changes = made.GroupBy(each => each.Change).Select(group => new PlaceChanged(group.Key, [.. group.Select(each => each.Repository)])).ToList();
        return new PlacePlanned(ask, rows, changes, refusals, Terminal(ask)) { Saves = saves };
    }

    /// <summary>A repository read through the resolvers a start uses over <paramref name="config"/>.</summary>
    public static PlaceSide SideOf(DriverConfig config, string repository, string workspace, IWorkflowStore store)
    {
        var driven = config.Drivable.Contains(repository, StringComparer.OrdinalIgnoreCase);
        var (followed, steps) = WorkflowProcesses.Otherwise(store, config, repository, workspace);
        var land = steps is null ? null : SetupPresets.LandOf(steps);
        var match = !driven ? new SetupMatch(SetupPresets.NotDriven, [])
            : land is null ? new SetupMatch(null, [steps is null && WorkflowSelection.Resolve(config, repository, workspace, task: null).Workflow == WorkflowSelection.Current
                ? "no workflow is chosen" : $"{followed} cannot be read here"])
            : SetupPresets.Match(true, land);
        var policies = RoleWork.Codes.Select(work =>
        {
            try
            {
                var policy = WorkPolicies.Of(config, work, repository, workspace);
                return new PlacePolicy(work, policy.Consent, PlaceSource.At(policy.Source, PlaceSource.RepositoryLevel), null);
            }
            catch (DriverException error)
            {
                return new PlacePolicy(work, null, PlaceSource.Default, error.Message);
            }
        }).ToList();
        return new PlaceSide(
            driven, config.Holds.Contains(repository, StringComparer.OrdinalIgnoreCase), config.OpensOwnTree(repository), land,
            steps?.Any(step => step.Kind == WorkflowKinds.Opinion) == true, steps?.Any(step => step.Kind == WorkflowKinds.Look) == true, match,
            PlaceProjection.ValuesAt(config, repository, workspace, PlaceSource.RepositoryLevel), policies);
    }

    /// <summary>
    /// What the plan lowers of the person's part at a repository: the steps its work follows compared before and after
    /// (<see cref="Involvement.Lowers"/>), and its landed route (<see cref="InvolvementRoutes.LowersRoute"/>).
    /// </summary>
    private static IReadOnlyList<string> LowersOf(IWorkflowStore store, DriverConfig config, PlaceApplied applied, string repository, string workspace)
    {
        if (applied.Changes.Count == 0) return [];
        var (_, was) = WorkflowProcesses.Otherwise(store, config, repository, workspace);
        var after = applied.Store ?? store;
        var (_, now) = WorkflowProcesses.Otherwise(after, applied.After, repository, workspace);
        // No workflow chosen asks nothing of the person, so what a copy asks of them replaces nothing it could lower.
        if (WorkflowSelection.Resolve(config, repository, workspace, task: null).Workflow == WorkflowSelection.Current) was = [];
        if (WorkflowSelection.Resolve(applied.After, repository, workspace, task: null).Workflow == WorkflowSelection.Current) now = [];
        List<string> lowered = was is null || now is null
            ? ["what its work follows could not be read here, so the change is taken as asking less of you"]
            : [.. Involvement.Lowers(now, was)];
        NodeGraph? Newest(string id) => after.Load(id) is { Read: { Problem: null, Versions: [.., var last] } } ? last.Graph : null;
        var landedBefore = WorkflowSelection.ResolveLanded(config, repository, workspace);
        var landedAfter = WorkflowSelection.ResolveLanded(applied.After, repository, workspace);
        lowered.AddRange(InvolvementRoutes.LowersRoute(
            new WorkflowLanded(landedAfter.Workflow, landedAfter.Gates), new WorkflowLanded(landedBefore.Workflow, landedBefore.Gates), Newest));
        return lowered;
    }

    /// <summary>The declaration at the line's tip and the acceptances the plan's after offers for it: landed verification and integration.</summary>
    private static PlaceDeclaration? DeclarationOf(PlaceTip tip, DriverConfig after, string repository, string workspace, PlaceSide side)
    {
        if (tip is not { Line: { } line, Commit: { } commit, Gates: { } gates }) return null;
        var acceptances = new List<PlaceAcceptance>();
        var landed = WorkflowSelection.ResolveLanded(after, repository, workspace);
        if (landed.Workflow != WorkflowSelection.Current)
        {
            acceptances.Add(new(Landed, LandedGates.StateOf(landed, new LandedTip(line, commit, gates)),
                $"Accept for landed verification: `{landed.Workflow}` verifies the work that lands by this declaration's gates, and a changed "
                + "declaration stops that verification until you accept it again."));
        }

        if (side.Land?.Method == LandNodes.Integrate)
        {
            acceptances.Add(new(Integration, IntegrationAcceptance.StateOf(IntegrationAcceptance.Of(after, repository), gates),
                "Accept for integration: the line integrates by this declaration's `integration`, and a changed declaration waits for you "
                + "while integration goes on under the one last accepted."));
        }

        return new PlaceDeclaration(line, commit, gates, tip.Text, acceptances);
    }

    /// <summary>The terminal's twin of a plan: <c>daoris place … --plan</c>, in the order its usage names the words.</summary>
    public static string Terminal(PlaceAsk ask) => string.Join(' ', ["daoris", "place", .. PlaceCommand.ArgsOf(ask)]);
}
