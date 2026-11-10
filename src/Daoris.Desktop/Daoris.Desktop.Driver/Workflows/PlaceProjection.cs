namespace Daoris.Driver;

/// <summary>Where a place's effective value is set, as the page words it: *set here*, *from the workspace*, *from this machine*, *default*.</summary>
public static class PlaceSource
{
    public const string Here = "here";
    public const string Workspace = "workspace";
    public const string Machine = "machine";
    public const string Default = "default";

    /// <summary>The tiers of a place, nearest first; a reader's own word for where it found a value is placed on one.</summary>
    public const string RepositoryLevel = "repository";
    public const string WorkspaceLevel = "workspace";

    /// <summary>
    /// A reader's source word (<c>repository</c>, <c>workspace-kind</c>, <c>machine</c>, <c>default</c>, <c>current</c>, <c>none</c>…) said
    /// from <paramref name="level"/>'s page: what the place itself holds is *set here*.
    /// </summary>
    public static string At(string raw, string level)
    {
        var tier = raw.StartsWith("repository", StringComparison.Ordinal) ? RepositoryLevel
            : raw.StartsWith("workspace", StringComparison.Ordinal) ? WorkspaceLevel
            : raw == "machine" ? Machine
            : Default;
        return tier == level ? Here : tier;
    }
}

/// <summary>One effective value of a place and where it is set; the value is null where it reads as nothing set.</summary>
public sealed record PlaceValue(string? Value, string Source);

/// <summary>Why a place needs a look, judged from the configuration alone: a code the page words, and what it names.</summary>
public sealed record PlaceNeed(string Code, string? Detail = null);

/// <summary>
/// One work at a place, light: the role a start finds (<see cref="RoleCatalog.RoleFor"/>) with its name as its record holds it and the scope
/// it sits at, and the rule's consent (<see cref="WorkPolicies.Of"/>). <paramref name="Source"/> is the rule's, never the role's.
/// </summary>
public sealed record PlaceWork(
    string Work, string? Role, string? Held, string? Consent, string Source, string? PolicyFailed,
    AuthoredText? RoleName = null, RoleScope? RoleScope = null);

/// <summary>
/// The default a place follows, by its id, and each option the place says otherwise, as <c>accept `automatic`</c>: under Current
/// the default its landing rule matches, and for a chosen version the default it copies.
/// </summary>
public sealed record PlaceDefault(string? Default, IReadOnlyList<string> Differs)
{
    public bool Equals(PlaceDefault? other) => other is not null && Default == other.Default && Differs.SequenceEqual(other.Differs);

    public override int GetHashCode() => HashCode.Combine(Default, Differs.Count);
}

/// <summary>The values a head's line says, each as its execution reader resolves it.</summary>
/// <param name="Follows">The default its workflow follows; null where it follows none, or its version is not read here.</param>
/// <param name="Lands">
/// How the work it follows lands (<see cref="SetupPresets.Match"/> over <see cref="SetupPresets.LandOf"/> of its steps, read as driven), with
/// the options of its own; null where the version is not read here.
/// </param>
/// <param name="Look">Whether a review environment holds its work for your look: <c>on</c> where one is required, <c>off</c> otherwise; none set reads null.</param>
/// <param name="Helpers">What a helper its agent asks for does: the helpers rule's mode.</param>
/// <param name="Answer">The standing answer's words, a repository's own; null for none.</param>
public sealed record PlaceValues(
    PlaceValue Line, PlaceValue Landing, PlaceValue Workflow, PlaceValue Landed, PlaceValue Commits, PlaceValue Opinion, PlaceValue HandOver,
    PlaceValue Language, PlaceValue Access, PlaceDefault? Follows, SetupMatch? Lands = null, PlaceValue? Look = null, PlaceValue? Helpers = null,
    PlaceValue? Answer = null);

/// <summary>A place: this machine (no workspace), a workspace (no repository) or a repository.</summary>
/// <param name="Adoption">A repository's set-up: <c>adopted</c>, <c>running</c> (its set-up quest is open or taken) or <c>none</c>; null for the others.</param>
public sealed record PlaceRow(
    string? Workspace, string? Repository, bool Driven, bool Held, bool Trees, bool Checkout, bool Adopted, PlaceValues? Values,
    IReadOnlyList<PlaceWork> Works, IReadOnlyList<PlaceNeed> Needs, string? Adoption = null);

/// <summary>A workspace's row with the counts the fold says; <paramref name="Running"/> of the not driven stand among the driven, their adoption running.</summary>
public sealed record PlaceWorkspace(PlaceRow Place, int Driven, int NotDriven, int Running = 0);

/// <summary>A not-driven repository, named only.</summary>
public sealed record UndrivenRow(string Workspace, string Repository, bool Checkout);

/// <summary>One page of a workspace's not-driven repositories.</summary>
public sealed record UndrivenPage(string Workspace, IReadOnlyList<UndrivenRow> Rows, string? Next, int Total);

/// <summary>A place asked for whole: a repository of a workspace, or a workspace when no repository is named.</summary>
public sealed record PlaceTarget(string? Workspace, string? Repository);

/// <summary>One repository of a workspace, as its chooser needs it.</summary>
public sealed record PlaceMember(string Repository, bool Driven, bool Checkout, string Adoption);

/// <summary>Every repository of a workspace, not the page in hand.</summary>
public sealed record PlaceMembers(string Workspace, IReadOnlyList<PlaceMember> Rows);

/// <summary>
/// What <c>PLACES</c> asks: the page, its search and filter, the not-driven page when the fold is open, one place whole
/// (<paramref name="Detail"/>) and a workspace's members (<paramref name="Members"/>).
/// </summary>
public sealed record PlacesAsk(
    int Limit = PlacesAsk.DefaultLimit, string? After = null, string? Search = null, bool NeedsLook = false, string? Fold = null,
    string? FoldAfter = null, string? FoldSearch = null, PlaceTarget? Detail = null, string? Members = null)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}

/// <summary>This machine, each workspace and a page of the driven repositories, and the detail and members asked for.</summary>
public sealed record PlacesRead(
    PlaceRow Machine, IReadOnlyList<PlaceWorkspace> Workspaces, IReadOnlyList<PlaceRow> Repositories, string? Next, int Matching, UndrivenPage? Undriven,
    PlaceRow? Detail = null, PlaceMembers? Members = null);

/// <summary>
/// The Agents place's effective projection: this machine, each workspace and every driven
/// repository, each with its driving, its works' roles and rules, its head's values and where each is set, and why it needs a look. Each
/// value is read by the reader a start uses (<see cref="RoleCatalog.RoleFor"/>, <see cref="CanonicalLine"/>, <see cref="LandingRules"/>,
/// <see cref="WorkflowSelection"/>, <see cref="WorkPolicies.Of"/>), so the page cannot say another thing. It is a read of the
/// configuration and the registry alone: no git, no process, no account. A repository not driven is only counted for its workspace's
/// fold and named a page at a time.
/// </summary>
public static class PlaceProjection
{
    /// <param name="home">The home whose saved workflows a place's choice names, read for the default each version follows and how it lands; null reads none.</param>
    /// <param name="quests">The service's quests, read for each set-up that runs (<see cref="SetupQuests.OpenFor"/>, the reader the set-up press refuses a second by); null reads none, so none runs.</param>
    public static PlacesRead Of(
        DriverConfig config, IReadOnlyList<RepoView> registry, Func<RoleCatalog> catalogue, PlacesAsk ask, string? home = null,
        IReadOnlyList<QuestView>? quests = null)
    {
        var workflows = home is null ? null : WorkflowPorts.Store(home);
        RoleCatalog? read = null;
        string? failed = null;
        try
        {
            read = catalogue();
        }
        catch (DriverException error)
        {
            failed = error.Message;
        }

        bool Named(IReadOnlyList<string> list, string repository) => list.Contains(repository, StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<PlaceWork> WorksAt(RolePlace place)
        {
            var works = new List<PlaceWork>();
            foreach (var work in RoleWork.Codes)
            {
                WorkPolicy? policy = null;
                string? policyFailed = null;
                try
                {
                    policy = WorkPolicies.Of(config, work, place.Repository, place.Workspace);
                }
                catch (DriverException error)
                {
                    policyFailed = error.Message;
                }

                string? held = failed;
                RoleRecord? role = null;
                if (read is not null)
                {
                    var found = read.RoleFor(work, place);
                    (role, held) = (found.Role, found.Held);
                }

                var level = place.Repository is not null ? PlaceSource.RepositoryLevel : place.Workspace is not null ? PlaceSource.WorkspaceLevel : PlaceSource.Machine;
                works.Add(new PlaceWork(
                    work, role?.Id, held, policy?.Consent, policy is null ? PlaceSource.Default : PlaceSource.At(policy.Source, level), policyFailed,
                    role?.Name, role?.Scope));
            }

            return works;
        }

        static List<PlaceNeed> NeedsOf(IReadOnlyList<PlaceWork> works)
        {
            var needs = new List<PlaceNeed>();
            if (works.FirstOrDefault(work => work.Held is not null) is { } held) needs.Add(new PlaceNeed("work-held", held.Work));
            if (works.FirstOrDefault(work => work.PolicyFailed is not null) is { } bad) needs.Add(new PlaceNeed("policy-failed", bad.Work));
            return needs;
        }

        PlaceValues ValuesOf(string repository, string? workspace, string level) => ValuesAt(config, repository, workspace, level, workflows);

        var machineWorks = WorksAt(new RolePlace());
        var machineNeeds = NeedsOf(machineWorks);
        if (failed is not null) machineNeeds.Insert(0, new PlaceNeed("catalogue-unread", failed));
        var machine = new PlaceRow(null, null, false, false, false, false, false, null, machineWorks, machineNeeds);

        var rows = registry.OrderBy(known => known.Workspace, StringComparer.Ordinal).ThenBy(known => known.Repository, StringComparer.Ordinal).ToList();

        // A repository's row, the one the list and the detail share: a driven one's needs add its checkout and adoption.
        PlaceRow RepositoryRow(RepoView known)
        {
            var workspace = RemoteTarget.Workspace(known.Workspace);
            var driven = Named(config.Drivable, known.Repository);
            var works = WorksAt(new RolePlace(workspace, known.Repository));
            var needs = NeedsOf(works);
            var checkout = known.Root is { Length: > 0 };
            if (driven && !checkout) needs.Add(new PlaceNeed("no-checkout"));
            if (driven && !known.Adopted) needs.Add(new PlaceNeed("not-adopted"));
            return new PlaceRow(
                workspace, known.Repository, driven, Named(config.Holds, known.Repository),
                workflows is not null && PlaceIsolates.Of(workflows, config, known.Repository, known.Workspace) is not null, checkout, known.Adopted,
                ValuesOf(known.Repository, workspace, PlaceSource.RepositoryLevel), works, needs, AdoptionOf(known, quests));
        }

        bool Stands(RepoView known) => Named(config.Drivable, known.Repository) || AdoptionOf(known, quests) == Running;

        var workspaces = new List<PlaceWorkspace>();
        foreach (var group in rows.GroupBy(known => RemoteTarget.Workspace(known.Workspace), StringComparer.Ordinal))
        {
            var driven = group.Count(known => Named(config.Drivable, known.Repository));
            var works = WorksAt(new RolePlace(group.Key));
            workspaces.Add(new PlaceWorkspace(
                new PlaceRow(group.Key, null, false, false, false, false, false, ValuesOf("", group.Key, PlaceSource.WorkspaceLevel), works, NeedsOf(works)),
                driven, group.Count() - driven, group.Count(known => !Named(config.Drivable, known.Repository) && Stands(known))));
        }

        var limit = Math.Clamp(ask.Limit, 1, PlacesAsk.MaxLimit);
        var drivenRows = new List<PlaceRow>();
        foreach (var known in rows.Where(Stands))
        {
            if (ask.Search is { Length: > 0 } search && !known.Repository.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            var row = RepositoryRow(known);
            if (ask.NeedsLook && row.Needs.Count == 0) continue;
            drivenRows.Add(row);
        }

        PlaceRow? detail = null;
        if (ask.Detail is { } target)
        {
            if (target.Repository is { Length: > 0 } repository)
            {
                var found = rows.FirstOrDefault(known => string.Equals(known.Repository, repository, StringComparison.OrdinalIgnoreCase)
                    && (target.Workspace is not { Length: > 0 } || RemoteTarget.Workspace(known.Workspace) == RemoteTarget.Workspace(target.Workspace)));
                detail = found is null ? null : RepositoryRow(found);
            }
            else detail = target.Workspace is { Length: > 0 } name ? workspaces.FirstOrDefault(each => each.Place.Workspace == RemoteTarget.Workspace(name))?.Place : machine;
        }

        PlaceMembers? members = null;
        if (ask.Members is { Length: > 0 } member)
        {
            var name = RemoteTarget.Workspace(member);
            members = new PlaceMembers(name, [.. rows.Where(known => RemoteTarget.Workspace(known.Workspace) == name)
                .Select(known => new PlaceMember(known.Repository, Named(config.Drivable, known.Repository), known.Root is { Length: > 0 }, AdoptionOf(known, quests)))]);
        }

        var page = drivenRows.Where(row => ask.After is null || string.CompareOrdinal(Cursor(row.Workspace!, row.Repository!), ask.After) > 0).Take(limit + 1).ToList();
        var next = page.Count > limit ? Cursor(page[limit - 1].Workspace!, page[limit - 1].Repository!) : null;
        return new PlacesRead(
            machine, workspaces, [.. page.Take(limit)], next, drivenRows.Count,
            ask.Fold is { Length: > 0 } fold ? Undriven(config, rows.Where(known => !Stands(known)).ToList(), fold, ask, limit) : null, detail, members);
    }

    private const string Running = "running";

    /// <summary>A repository's set-up: adopted as the registry marks it, else running while its set-up quest is open or taken, else none.</summary>
    private static string AdoptionOf(RepoView known, IReadOnlyList<QuestView>? quests) =>
        known.Adopted ? "adopted"
        : quests is not null && SetupQuests.OpenFor(quests, known.Repository) is not null ? Running
        : "none";

    /// <summary>
    /// The values a place's head says, each as its execution reader resolves it over <paramref name="config"/> and with where it is set,
    /// said from <paramref name="level"/>'s page; a workspace's is read with no repository named. A place's plan reads its before and after
    /// through this, so the two cannot say another thing.
    /// </summary>
    /// <param name="workflows">Where a chosen workflow's versions are read, for the default it follows; null reads none, and it follows none known.</param>
    public static PlaceValues ValuesAt(DriverConfig config, string repository, string? workspace, string level, IWorkflowStore? workflows = null)
    {
        var line = CanonicalLine.Choose(config, repository, workspace, guess: null);
        var flow = WorkflowSelection.Resolve(config, level == PlaceSource.RepositoryLevel ? repository : null, workspace, task: null);
        var landed = WorkflowSelection.ResolveLanded(config, level == PlaceSource.RepositoryLevel ? repository : null, workspace);
        var commits = CommitRules.Resolve(config, repository, workspace);
        var opinion = WorkPolicies.Opinion(config, repository, workspace);
        var handOver = WorkPolicies.HandOver(config, repository, workspace);
        var language = level == PlaceSource.RepositoryLevel
            ? SessionLanguages.Resolve(config, repository, workspace)
            : SessionLanguages.OfWorkspace(config, workspace);
        var access = AccessRules.Resolve(config, repository, workspace);
        var sources = access.Pauses.Select(pause => PlaceSource.At(pause.Source, level)).Distinct(StringComparer.Ordinal).ToList();
        var review = ReviewRules.Resolve(config, repository, workspace);
        var helpers = HelperRules.Resolve(config, repository, workspace);
        var standing = level == PlaceSource.RepositoryLevel ? config.StandingFor(repository) : null;
        var scope = level == PlaceSource.RepositoryLevel ? repository : "";
        var steps = StepsFollowed(flow.Workflow, workflows);
        var lands = steps is null ? null : SetupPresets.Match(driven: true, SetupPresets.LandOf(steps));
        // A saved workflow's delivery is the default its version copies, and what the copy changes.
        if (FollowsOf(flow.Workflow, workflows) is { Default: { } copied } held && SetupPresets.Of(copied) is not null)
        {
            lands = new SetupMatch(copied, held.Differs);
        }

        // How its work lands is its workflow's `land`: a merge, or a branch every other method makes.
        var land = steps?.FirstOrDefault(step => step.Kind == WorkflowKinds.Landing);
        var form = land is null ? null : (string?)land.Field("method") == LandNodes.Merge ? LandingForm.Merge : LandingForm.Branch;
        return new PlaceValues(
            new(line.Branch, PlaceSource.At(line.Source, level)),
            new(form, PlaceSource.At(flow.Level, level)),
            new(flow.Workflow, PlaceSource.At(flow.Level, level)),
            new(landed.Workflow, PlaceSource.At(landed.Level, level)),
            new(commits.Rule.Mode, PlaceSource.At(commits.Source, level)),
            opinion is null ? new(null, PlaceSource.Default) : new(opinion.Rule.IsNone ? "none" : string.Join(",", opinion.Rule.Reviewers), PlaceSource.At(opinion.Source, level)),
            new(handOver.Rule.Mode, PlaceSource.At(handOver.Source, level)),
            language is null ? new(null, PlaceSource.Default) : new(language.Code, PlaceSource.At(language.Source, level)),
            new(access.Full ? "full" : access.Today ? "today" : "custom", sources is [var one] ? one : "mixed"),
            FollowsOf(flow.Workflow, workflows),
            lands,
            review is null ? new(null, PlaceSource.Default) : new(review.Rule is { IsNone: false, Required: true } ? "on" : "off", PlaceSource.At(review.Source, level)),
            new(helpers.Rule.Mode, PlaceSource.At(helpers.Source, level)),
            standing is null ? new(null, PlaceSource.Default) : new(standing.Says, PlaceSource.Here));
    }

    /// <summary>
    /// The steps a place's workflow follows, as <c>WorkflowProcesses.Otherwise</c> reads them for a start: the newest graph version of the saved
    /// workflow. Null where none is chosen, or no version reads here.
    /// </summary>
    private static IReadOnlyList<NamedStep>? StepsFollowed(string chosen, IWorkflowStore? workflows) =>
        Newest(chosen, workflows)?.Graph is { } graph ? Involvement.StepsOf(graph) : null;

    /// <summary>
    /// The default a place's saved workflow follows: the default its newest graph version copies, with what the copy changes. Null for a
    /// version copied from none, one this build cannot read, or where none is chosen.
    /// </summary>
    private static PlaceDefault? FollowsOf(string chosen, IWorkflowStore? workflows)
    {
        if (Newest(chosen, workflows) is not { From: { } from, Graph: { } graph }) return null;
        if (WorkflowDefaults.Of(from.Default) is not { } shipped) return new PlaceDefault(from.Default, ["unknown here"]);
        return new PlaceDefault(shipped.Id, WorkflowDefaults.Differs(shipped, graph));
    }

    private static NamedVersion? Newest(string chosen, IWorkflowStore? workflows) =>
        chosen != WorkflowSelection.Current && WorkflowNamed.IsId(chosen) ? workflows?.Load(chosen)?.Read.Versions.LastOrDefault(each => each.Graph is not null) : null;

    private static string Cursor(string workspace, string repository) => workspace + "\u001f" + repository;

    private static UndrivenPage Undriven(DriverConfig config, List<RepoView> rows, string workspace, PlacesAsk ask, int limit)
    {
        var name = RemoteTarget.Workspace(workspace);
        var all = rows.Where(known => string.Equals(RemoteTarget.Workspace(known.Workspace), name, StringComparison.Ordinal)
                && !config.Drivable.Contains(known.Repository, StringComparer.OrdinalIgnoreCase)
                && (ask.FoldSearch is not { Length: > 0 } search || known.Repository.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var page = all.Where(known => ask.FoldAfter is null || string.CompareOrdinal(known.Repository, ask.FoldAfter) > 0).Take(limit + 1).ToList();
        return new UndrivenPage(
            name, [.. page.Take(limit).Select(known => new UndrivenRow(name, known.Repository, known.Root is { Length: > 0 }))],
            page.Count > limit ? page[limit - 1].Repository : null, all.Count);
    }

    /// <summary>The places a chosen place is read through, nearest first: itself, its workspace and this machine (<c>ROLE_STRUCTURE</c>'s chain).</summary>
    public static IReadOnlyList<RolePlace> ChainOf(RolePlace place)
    {
        var chain = new List<RolePlace>();
        if (place.Repository is not null) chain.Add(place);
        if (place.Workspace is not null) chain.Add(new RolePlace(place.Workspace));
        chain.Add(new RolePlace());
        return chain;
    }
}
