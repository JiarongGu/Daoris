using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What one real tick may start of git: a count over a budget, or a start of a read the git library answers in process (a revision
/// verified, <c>HEAD</c> read), fails with the starts grouped by their first words, so the read that came back is named.
/// </summary>
internal static class TickGitBudget
{
    internal static void Holds(IEnumerable<GitLaunched> starts, int budget)
    {
        var all = starts.ToList();
        var said = string.Join("; ", all.GroupBy(Words).OrderByDescending(group => group.Count()).Select(group => $"{group.Count()} x {group.Key}"));
        Assert.True(all.Count <= budget, $"a tick started {all.Count} gits, over {budget}: {said}");
        Assert.True(all.All(start => !start.Arguments.Contains("rev-parse") || GitLibraryReads.Asked(start.Arguments) is null), $"a revision the library answers was started: {said}");
    }

    private static string Words(GitLaunched start) =>
        string.Join(' ', start.Arguments.Where(argument => !argument.StartsWith("--no-optional", StringComparison.Ordinal)).Take(start.Kind == GitKind.Layout ? 4 : 2));
}
