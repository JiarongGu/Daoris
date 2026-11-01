namespace Daoris.Driver;

/// <summary>Where a place's effective value was set, as the page words it: *set here*, *from the workspace*, *from this machine*, *default*.</summary>
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

/// <summary>One effective value of a place and where it was set; the value is null where it reads as nothing set.</summary>
public sealed record PlaceValue(string? Value, string Source);

/// <summary>Why a place needs a look, judged from the configuration alone: a code the page words, and what it names.</summary>
public sealed record PlaceNeed(string Code, string? Detail = null);

/// <summary>One work at a place, light: the role a start finds (<see cref="RoleCatalog.RoleFor"/>) and the rule's consent (<see cref="WorkPolicies.Of"/>).</summary>
public sealed record PlaceWork(string Work, string? Role, string? Held, string? Consent, string Source, string? PolicyFailed);

/// <summary>The values a head's line says, each as its execution reader resolves it.</summary>
public sealed record PlaceValues(
    PlaceValue Line, PlaceValue Landing, PlaceValue Workflow, PlaceValue Landed, PlaceValue Commits, PlaceValue Opinion, PlaceValue HandOver,
    PlaceValue Language, PlaceValue Access);

/// <summary>A place: this machine (no workspace), a workspace (no repository) or a driven repository.</summary>
public sealed record PlaceRow(
    string? Workspace, string? Repository, bool Driven, bool Held, bool Trees, bool Checkout, bool Adopted, PlaceValues? Values,
    IReadOnlyList<PlaceWork> Works, IReadOnlyList<PlaceNeed> Needs);

/// <summary>A workspace's row with the counts the fold says.</summary>
public sealed record PlaceWorkspace(PlaceRow Place, int Driven, int NotDriven);

/// <summary>A not-driven repository, named only.</summary>
public sealed record UndrivenRow(string Workspace, string Repository, bool Checkout);

/// <summary>One page of a workspace's not-driven repositories.</summary>
public sealed record UndrivenPage(string Workspace, IReadOnlyList<UndrivenRow> Rows, string? Next, int Total);

/// <summary>What <c>PLACES</c> asks: the page, its search and filter, and the not-driven page when the fold is open.</summary>
public sealed record PlacesAsk(
    int Limit = PlacesAsk.DefaultLimit, string? After = null, string? Search = null, bool NeedsLook = false, string? Fold = null,
    string? FoldAfter = null, string? FoldSearch = null)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}

/// <summary>This machine, each workspace and a page of the driven repositories.</summary>
public sealed record PlacesRead(
    PlaceRow Machine, IReadOnlyList<PlaceWorkspace> Workspaces, IReadOnlyList<PlaceRow> Repositories, string? Next, int Matching, UndrivenPage? Undriven);

/// <summary>
/// The Agents place's effective projection (AGENTSPLACE1a, the design §2 and §3): this machine, each workspace and every driven
/// repository, each with its driving, its works' roles and rules, its head's values and where each was set, and why it needs a look. Each
/// value is read by the reader a start uses (<see cref="RoleCatalog.RoleFor"/>, <see cref="CanonicalLine"/>, <see cref="LandingRules"/>,
/// <see cref="WorkflowSelection"/>, <see cref="WorkPolicies.Of"/>), so the page cannot say another thing. It is a read of the
/// configuration and the registry alone: no git, no process, no account. A repository not driven is only counted for its workspace's
/// fold and named a page at a time.
/// </summary>
public static class PlaceProjection
{
    public static PlacesRead Of(
        DriverConfig config, IReadOnlyList<RepoView> registry, Func<RoleCatalog> catalogue, PlacesAsk ask)
    {
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
        var unreadRepositories = config.UnreadLandings("landings");
        var unreadWorkspaces = config.UnreadLandings("workspaceLandings");

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
                works.Add(new PlaceWork(work, role?.Id, held, policy?.Consent, policy is null ? PlaceSource.Default : PlaceSource.At(policy.Source, level), policyFailed));
            }

            return works;
        }

        static List<PlaceNeed> NeedsOf(IReadOnlyList<PlaceWork> works, IEnumerable<UnreadLanding> unread)
        {
            var needs = new List<PlaceNeed>();
            needs.AddRange(unread.Select(each => new PlaceNeed("landing-unread", each.Said)));
            if (works.FirstOrDefault(work => work.Held is not null) is { } held) needs.Add(new PlaceNeed("work-held", held.Work));
            if (works.FirstOrDefault(work => work.PolicyFailed is not null) is { } bad) needs.Add(new PlaceNeed("policy-failed", bad.Work));
            return needs;
        }

        PlaceValues ValuesOf(string repository, string? workspace, string level) => ValuesAt(config, repository, workspace, level);

        var machineWorks = WorksAt(new RolePlace());
        var machineNeeds = NeedsOf(machineWorks, []);
        if (failed is not null) machineNeeds.Insert(0, new PlaceNeed("catalogue-unread", failed));
        var machine = new PlaceRow(null, null, false, false, false, false, false, null, machineWorks, machineNeeds);

        var rows = registry.OrderBy(known => known.Workspace, StringComparer.Ordinal).ThenBy(known => known.Repository, StringComparer.Ordinal).ToList();
        var workspaces = new List<PlaceWorkspace>();
        foreach (var group in rows.GroupBy(known => RemoteTarget.Workspace(known.Workspace), StringComparer.Ordinal))
        {
            var driven = group.Count(known => Named(config.Drivable, known.Repository));
            var works = WorksAt(new RolePlace(group.Key));
            var unread = unreadWorkspaces.Where(each => string.Equals(each.Name, group.Key, StringComparison.OrdinalIgnoreCase));
            workspaces.Add(new PlaceWorkspace(
                new PlaceRow(group.Key, null, false, false, false, false, false, ValuesOf("", group.Key, PlaceSource.WorkspaceLevel), works, NeedsOf(works, unread)),
                driven, group.Count() - driven));
        }

        var limit = Math.Clamp(ask.Limit, 1, PlacesAsk.MaxLimit);
        var drivenRows = new List<PlaceRow>();
        foreach (var known in rows.Where(known => Named(config.Drivable, known.Repository)))
        {
            var workspace = RemoteTarget.Workspace(known.Workspace);
            if (ask.Search is { Length: > 0 } search && !known.Repository.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            var works = WorksAt(new RolePlace(workspace, known.Repository));
            var needs = NeedsOf(works, unreadRepositories.Where(each => string.Equals(each.Name, known.Repository, StringComparison.OrdinalIgnoreCase)));
            var checkout = known.Root is { Length: > 0 };
            if (!checkout) needs.Add(new PlaceNeed("no-checkout"));
            if (!known.Adopted) needs.Add(new PlaceNeed("not-adopted"));
            if (ask.NeedsLook && needs.Count == 0) continue;
            drivenRows.Add(new PlaceRow(
                workspace, known.Repository, true, Named(config.Holds, known.Repository), config.OpensOwnTree(known.Repository), checkout, known.Adopted,
                ValuesOf(known.Repository, workspace, PlaceSource.RepositoryLevel), works, needs));
        }

        var page = drivenRows.Where(row => ask.After is null || string.CompareOrdinal(Cursor(row.Workspace!, row.Repository!), ask.After) > 0).Take(limit + 1).ToList();
        var next = page.Count > limit ? Cursor(page[limit - 1].Workspace!, page[limit - 1].Repository!) : null;
        return new PlacesRead(machine, workspaces, [.. page.Take(limit)], next, drivenRows.Count, ask.Fold is { Length: > 0 } fold ? Undriven(config, rows, fold, ask, limit) : null);
    }

    /// <summary>
    /// The values a place's head says, each as its execution reader resolves it over <paramref name="config"/> and with where it was set,
    /// said from <paramref name="level"/>'s page; a workspace's is read with no repository named. A place's plan reads its before and after
    /// through this (AGENTSPLACE1a2), so the two cannot say another thing.
    /// </summary>
    public static PlaceValues ValuesAt(DriverConfig config, string repository, string? workspace, string level)
    {
        var line = CanonicalLine.Choose(config, repository, workspace, guess: null);
        var landing = LandingRules.Choose(config, repository, workspace);
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
        return new PlaceValues(
            new(line.Branch, PlaceSource.At(line.Source, level)),
            new(landing.Rule.Form, PlaceSource.At(landing.Source, level)),
            new(flow.Workflow, PlaceSource.At(flow.Level, level)),
            new(landed.Workflow, PlaceSource.At(landed.Level, level)),
            new(commits.Rule.Mode, PlaceSource.At(commits.Source, level)),
            opinion is null ? new(null, PlaceSource.Default) : new(opinion.Rule.IsNone ? "none" : string.Join(",", opinion.Rule.Reviewers), PlaceSource.At(opinion.Source, level)),
            new(handOver.Rule.Mode, PlaceSource.At(handOver.Source, level)),
            language is null ? new(null, PlaceSource.Default) : new(language.Code, PlaceSource.At(language.Source, level)),
            new(access.Full ? "full" : access.Today ? "today" : "custom", sources is [var one] ? one : "mixed"));
    }

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
