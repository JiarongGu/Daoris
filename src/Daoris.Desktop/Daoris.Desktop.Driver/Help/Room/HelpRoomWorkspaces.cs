using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The workspaces and their repositories (HELP1a): each repository with how it is driven, its line and how
/// its work lands, from the driver's own answers, and its standing answer where it keeps one (KNOWUSE1b) — or, with none
/// registered, where to begin.
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
                Landing = LandingRules.Choose(config, known.Repository, known.Workspace),
                // KNOWUSE1b: what the person says holds for every session there, in their words.
                Standing = config.StandingFor(known.Repository)?.Says,
            })],
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
            text.Append("| Repository | Driven | Line | Work lands |\n");
            text.Append("|---|---|---|---|\n");
            foreach (var repository in circle.OrderBy(repository => repository.Name, StringComparer.Ordinal))
            {
                text.Append($"| `{repository.Name}` | {Driven(repository)} | {LineOf(repository.Line)} | {Lands(repository.Landing)} |\n");
            }

            text.Append('\n');

            // KNOWUSE1b: each standing answer in the person's words, after the table, only where one is kept.
            var standing = circle.Where(repository => repository.Standing is not null).OrderBy(repository => repository.Name, StringComparer.Ordinal).ToList();
            if (standing.Count > 0)
            {
                text.Append($"Standing answers in `{circle.Key}`, each handed to every session in its repository beneath its quest "
                            + "(`daoris driver standing <repository> \"…\"|--clear`):\n\n");
                foreach (var repository in standing) text.Append($"- `{repository.Name}`: \"{repository.Standing!.ReplaceLineEndings(" ")}\"\n");
                text.Append('\n');
            }
        }

        return text.ToString();
    }

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

    public Landing Landing { get; init; } = new(LandingRule.Merge, LandingSource.Default);

    /// <summary>Its standing answer on this machine, the person's words (KNOWUSE1b); null where it keeps none.</summary>
    public string? Standing { get; init; }
}

public sealed partial record HelpMachine
{
    public IReadOnlyList<HelpRepository> Repositories { get; init; } = [];
}
