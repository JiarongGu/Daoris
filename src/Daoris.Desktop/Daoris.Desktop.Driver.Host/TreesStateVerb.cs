namespace Daoris.Driver.Host;

/// <summary>`daoris tree state`: the milestone of every unit of work, or the ask of one landed branch's plugin again.</summary>
internal static class TreesStateVerb
{
    /// <summary>
    /// The milestone of every unit: end reached, result kept, cleanup pending or completed, recovery retained, derived read-only
    /// from the records the tree lifecycle already keeps. Before the plugin's ask, which takes a name.
    /// </summary>
    public static async Task<int> AllAsync(string[] args, string home)
    {
        var (units, unread) = await WorkConsole.ReadAsync(home).ConfigureAwait(false);
        foreach (var line in unread) Console.Error.WriteLine($"trees: {line}");
        foreach (var line in WorkWords.All(units, args.Contains("--entries"))) Console.WriteLine(line);
        return 0;
    }

    /// <summary>
    /// Ask again: the review's press, from a terminal. The landed branch's plugin is asked
    /// about its pull request whatever the kept answer's age, the answer kept, and what it proves here said. Named by the session that
    /// landed it or by the branch, standing or a trace. It removes nothing: the clean-up and bringing up to date act on what it kept.
    /// Exempt from Ask Daoris as a read.
    /// </summary>
    public static async Task<int> AskAgainAsync(string[] args, string named, string home, SessionTrees trees, SessionTrees asking)
    {
        var repository = TreesConsole.Option(args, "--repository");
        // The milestone of the unit it names, before the plugin is asked; a name no unit has is said by the ask's own words.
        var (milestones, notRead) = await WorkConsole.ReadAsync(home).ConfigureAwait(false);
        foreach (var line in notRead) Console.Error.WriteLine($"trees: {line}");
        foreach (var unit in WorkInventories.Named(milestones, named)) foreach (var line in WorkWords.Milestone(unit)) Console.WriteLine(line);
        var found = trees.Recorded.FindAll(named, repository);
        if (found.Count == 0)
        {
            Console.Error.WriteLine($"trees: `{named}` names no branch a landing made on this machine, so there is no pull request of it to ask about.");
            return 1;
        }

        // A session means its newest landing; a branch name in two repositories means the person says which.
        var owners = found.Select(each => each.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (owners.Count > 1)
        {
            Console.Error.WriteLine($"trees: `{named}` names a landed branch in {string.Join(", ", owners.Select(r => $"`{r}`"))} — "
                + "say which with `--repository <name>`.");
            return 1;
        }

        var entry = found[0];
        using var service = ServiceClient.FromEnvironment();
        var root = (await service.RegistryAsync().ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, entry.Repository, StringComparison.OrdinalIgnoreCase))?.Root;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            Console.Error.WriteLine($"trees: `{entry.Repository}` has no checkout on this machine, where its plugin would ask and git would judge the answer.");
            return 1;
        }

        var again = await asking.AskAgainAsync(root, entry).ConfigureAwait(false);
        foreach (var line in PullRequestWords.AskedAgain(again)) Console.WriteLine($"trees: {line}");
        // 1 where nothing new is known: no plugin to ask, one not ready, or an ask that failed.
        return again.Answered || again.Final ? 0 : 1;
    }
}
