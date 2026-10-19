using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The workspaces and their repositories (HELP1a): each repository with how it is driven, its line and how
/// its work lands, from the driver's own answers, and its standing answer where it keeps one (KNOWUSE1b) — or, with none
/// registered, where to begin. Since WORKFLOW1h-b, the workflow each one's work follows and each workspace's kinds (§8.4).
/// </summary>
internal sealed class HelpRoomWorkspaces : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources)
    {
        var config = sources.Config;
        var lineOf = sources.Lines.ToDictionary(line => line.Repository, StringComparer.OrdinalIgnoreCase);
        bool Named(IReadOnlyList<string> list, string repository) => list.Contains(repository, StringComparer.OrdinalIgnoreCase);

        return machine with
        {
            Repositories = [.. sources.Snapshot.Repositories.Select(known => new HelpRepository(known.Repository, known.Workspace)
            {
                Checkout = known.Root is { Length: > 0 },
                Drivable = Named(config.Drivable, known.Repository),
                Held = Named(config.Holds, known.Repository),
                OwnTree = config.OpensOwnTree(known.Repository),
                Line = lineOf.TryGetValue(known.Repository, out var line) ? new Line(line.Branch, line.Source) : new Line(null, LineSource.None),
                // PARALLEL1a: a checkout whose line the facts have not judged yet, said so rather than as none git can name.
                LineNotReadYet = Named(sources.NotReadYet.Repositories, known.Repository),
                Landing = LandingRules.Choose(config, known.Repository, known.Workspace),
                // KNOWUSE1b: what the person says holds for every session there, in their words.
                Standing = config.StandingFor(known.Repository)?.Says,
                // REVIEWENV1a: where its work is shown to the person before it lands, and where that was set.
                Review = ReviewRules.Resolve(config, known.Repository, known.Workspace),
                // XAGENT1a: which other agent reads its work before it lands, and where that was set.
                Opinion = OpinionRules.Resolve(config, known.Repository, known.Workspace),
                // WORKFLOW1h-b (design §8.4): the workflow its new work follows, and what chose it.
                Workflow = Followed(config, sources.Workflows, known.Repository, known.Workspace),
            })],
            WorkflowKinds =
            [
                .. config.WorkspaceWorkflows.OrderBy(pair => pair.Key, StringComparer.Ordinal).SelectMany(pair => pair.Value.Kinds
                    .Select(kind => new HelpWorkflowKind(pair.Key, kind.Key, kind.Value.Label, kind.Value.Paths, kind.Value.Workflow))),
            ],
        };
    }

    /// <summary>
    /// The workflow a repository's work follows with no task's kind (design §4.1, rows 3, 5 and 6), Current where nothing chooses
    /// one, its newest version where it is saved here, and a repository's own kinds.
    /// </summary>
    private static HelpWorkflowFollowed Followed(DriverConfig config, IReadOnlyList<WorkflowFileFound>? saved, string repository, string workspace)
    {
        var selected = WorkflowSelection.Resolve(config, repository, workspace, null);
        var own = config.Workflows.FirstOrDefault(pair => string.Equals(pair.Key, repository, StringComparison.OrdinalIgnoreCase)).Value;
        var followed = new HelpWorkflowFollowed(selected.Workflow, selected.Level)
        {
            Kinds = [.. own?.Kinds.Select(kind => (kind.Key, kind.Value.Workflow)) ?? []],
        };
        if (selected.Workflow == WorkflowSelection.Current || saved is null) return followed;

        var found = saved.FirstOrDefault(each => each.Id == selected.Workflow);
        return found switch
        {
            null => followed with { Problem = "not saved here" },
            { Read.Problem: not null } => followed with { Problem = "which cannot be read here" },
            _ => followed with { Name = found.Read.Name, Version = found.Read.Versions[^1].Version },
        };
    }

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        if (machine.Repositories.Count == 0)
        {
            text.Append("No repository is registered on this machine yet. The first step is `daoris connect` from inside\n");
            text.Append("one, or Repositories → Import a folder as a workspace, to register a folder of them at once\n");
            text.Append("(`daoris import <folder> --workspace <name>`).\n\n");
        }

        foreach (var circle in machine.Repositories
                     .GroupBy(repository => repository.Workspace, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(circle => circle.Key, StringComparer.Ordinal))
        {
            text.Append($"### Workspace `{circle.Key}`\n\n");
            // REVIEWENV1a (design §1.7–§1.8): the room's table names each repository's review rule, and *no review
            // environment* where nothing is set, which is today's behaviour. XAGENT1a (design §2.5–§2.6) adds each one's second
            // opinion, and *no second opinion* where nothing is set.
            // WORKFLOW1h-b (the workflow design §8.4) adds the workflow each one's new work follows, Current where nothing chooses one.
            text.Append("| Repository | Driven | Line | Work lands | Reviewed before landing | Second opinion before landing | Workflow |\n");
            text.Append("|---|---|---|---|---|---|---|\n");
            foreach (var repository in circle.OrderBy(repository => repository.Name, StringComparer.Ordinal))
            {
                var line = repository.LineNotReadYet ? "not read yet" : LineOf(repository.Line);
                text.Append($"| `{repository.Name}` | {Driven(repository)} | {line} | {Lands(repository.Landing)} | "
                    + $"{ReviewRules.RoomCell(repository.Review)} | {OpinionRules.RoomCell(repository.Opinion)} | {Follows(repository.Workflow)} |\n");
            }

            text.Append('\n');

            // WORKFLOW1h-b (§8.4): the kinds of work the workspace declares, each with the workflow it maps to.
            var kinds = machine.WorkflowKinds.Where(kind => string.Equals(kind.Workspace, circle.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            if (kinds.Count > 0)
            {
                text.Append($"Kinds of work `{circle.Key}` declares (`daoris driver workflow kind <workspace> <kind> --label \"…\" [--paths …]`):\n\n");
                foreach (var kind in kinds)
                {
                    var paths = kind.Paths.Count == 0 ? "" : ", paths " + string.Join(", ", kind.Paths.Select(path => $"`{path}`"));
                    text.Append($"- `{kind.Kind}`{(kind.Label is { } label ? $" ({label})" : "")}{paths}: {Mapped(kind.Workflow, "the level's default")}\n");
                }

                text.Append('\n');
            }

            // KNOWUSE1b: each standing answer in the person's words, after the table, only where one is kept.
            var standing = circle.Where(repository => repository.Standing is not null).OrderBy(repository => repository.Name, StringComparer.Ordinal).ToList();
            if (standing.Count > 0)
            {
                text.Append($"Standing answers in `{circle.Key}`, each handed to every session in its repository beneath its quest "
                            + "(`daoris driver standing <repository> \"…\"|--clear`):\n\n");
                foreach (var repository in standing) text.Append($"- `{repository.Name}`: \"{TextBounds.OneLine(repository.Standing!, trim: false)}\"\n");
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>The table's workflow cell: the workflow, its newest version and name where saved here, and what chose it.</summary>
    private static string Follows(HelpWorkflowFollowed? followed)
    {
        if (followed is null) return "Current (the default)";
        var source = followed.Level switch
        {
            WorkflowLevels.Repository => "set for it",
            WorkflowLevels.Workspace => "its workspace's",
            _ => "the default",
        };
        var what = followed.Workflow == WorkflowSelection.Current ? "Current"
            : followed.Problem is { } problem ? $"`{followed.Workflow}`, {problem}"
            : followed.Version is { } version ? $"`{followed.Workflow}` v{version}, {followed.Name}"
            : $"`{followed.Workflow}`";
        var kinds = followed.Kinds.Count == 0 ? ""
            : "; by kind: " + string.Join(", ", followed.Kinds.Select(kind => $"`{kind.Kind}` → {Mapped(kind.Workflow, "its default")}"));
        return $"{what} ({source}){kinds}";
    }

    /// <summary>What a kind maps to: a workflow's id, Current, or, for none, the words given.</summary>
    private static string Mapped(string? workflow, string none) => workflow switch
    {
        null => none,
        WorkflowSelection.Current => "Current",
        _ => $"`{workflow}`",
    };

    private static string Driven(HelpRepository repository)
    {
        var parts = new List<string> { repository.Drivable ? "driven" : "not driven" };
        if (repository.Held) parts.Add("held");
        if (repository.Drivable && repository.OwnTree) parts.Add("in its own tree");
        if (!repository.Checkout) parts.Add("no checkout here");
        return string.Join(", ", parts);
    }

    private static string LineOf(Line line) => line.Branch is not { Length: > 0 } branch
        ? "none git can name"
        : line.Source switch
        {
            LineSource.Repository => $"`{branch}` (set for it)",
            LineSource.Workspace => $"`{branch}` (set for its workspace)",
            _ => $"`{branch}` (the checkout's own)",
        };

    private static string Lands(Landing landing)
    {
        var rule = landing.Rule.Form == "branch"
            ? $"on a branch `{landing.Rule.Pattern}`"
            : "merged into the line";
        if (landing.Rule.Plugin is { } plugin) rule += $", pushed with a pull request opened by plugin `{plugin}`";
        if (landing.Rule.Tidy) rule += ", tree removed once landed";
        // LAND2a (D145): the switch rides the rule, so the room says it where it says the rule.
        if (landing.Rule.AutoAccept) rule += ", accepted automatically when its quest is done";
        var source = landing.Source switch
        {
            LandingSource.Repository => "set for it",
            LandingSource.Workspace => "its workspace's rule",
            _ => "the default",
        };
        return $"{rule} ({source})";
    }
}

/// <summary>One repository as Ask Daoris's room tells it: how it is driven, its line, how its work lands.</summary>
/// <param name="Name">The registry's name for it.</param>
/// <param name="Workspace">Its circle.</param>
public sealed record HelpRepository(string Name, string Workspace)
{
    /// <summary>Whether it has a checkout on this machine.</summary>
    public bool Checkout { get; init; }

    public bool Drivable { get; init; }

    public bool Held { get; init; }

    public bool OwnTree { get; init; }

    public Line Line { get; init; } = new(null, LineSource.None);

    /// <summary>Whether its checkout's line was not judged yet when the room was written (PARALLEL1a).</summary>
    public bool LineNotReadYet { get; init; }

    public Landing Landing { get; init; } = new(LandingRule.Merge, LandingSource.Default);

    /// <summary>Its standing answer on this machine, the person's words (KNOWUSE1b); null where it keeps none.</summary>
    public string? Standing { get; init; }

    /// <summary>The review rule standing for it and where it was set (REVIEWENV1a); null where nothing is set anywhere.</summary>
    public ResolvedReview? Review { get; init; }

    /// <summary>The second-opinion rule standing for it and where it was set (XAGENT1a); null where nothing is set anywhere.</summary>
    public ResolvedOpinion? Opinion { get; init; }

    /// <summary>The workflow its new work follows and what chose it (WORKFLOW1h-b, the workflow design §8.4); null reads as Current.</summary>
    public HelpWorkflowFollowed? Workflow { get; init; }
}

/// <summary>
/// The workflow a repository's work follows with no task's kind (WORKFLOW1h-b, design §4.1, §8.4): its id or <c>current</c>, the level
/// that chose it, its name and newest version where it is saved here, or why it is not, and the repository's own kinds.
/// </summary>
/// <param name="Level">One of <see cref="WorkflowLevels"/>.</param>
public sealed record HelpWorkflowFollowed(string Workflow, string Level)
{
    public string? Name { get; init; }

    public int? Version { get; init; }

    /// <summary>Why the chosen workflow's version is not named: not saved here, or unreadable; null where it is, or none was read.</summary>
    public string? Problem { get; init; }

    /// <summary>The repository's own kinds, each with the workflow it maps to, null for its default.</summary>
    public IReadOnlyList<(string Kind, string? Workflow)> Kinds { get; init; } = [];
}

/// <summary>A kind of work a workspace declares (WORKFLOW1e, design §4.2), its label, paths and the workflow it maps to, null for none.</summary>
public sealed record HelpWorkflowKind(string Workspace, string Kind, string? Label, IReadOnlyList<string> Paths, string? Workflow);
