using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A repository's workspace as its wider place for the supervisor's rules (the context counters design §4.4): a line of
/// <c>repository:engine</c> reads <c>workspace:main</c>'s rules where its repository holds none on a count, so the judge weighs a write
/// on the same chain the executors read. The map is the registry's, as the driver's last look read it on this home; where none was read
/// (a terminal's own process), a place is judged on its own chain to the machine.
/// </summary>
public sealed class RuleWiderPlaces(IReadOnlyDictionary<string, string> workspaceOf)
{
    private static readonly ConcurrentDictionary<string, RuleWiderPlaces> _kept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>No repository's workspace known.</summary>
    public static RuleWiderPlaces None { get; } = new(new Dictionary<string, string>());

    /// <summary>The map from each repository to its workspace, for those registered in one.</summary>
    public static RuleWiderPlaces From(IEnumerable<(string Repository, string? Workspace)> registered) =>
        new(registered.Where(each => each.Workspace is { Length: > 0 })
            .GroupBy(each => each.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Workspace!, StringComparer.OrdinalIgnoreCase));

    /// <summary>Kept for <paramref name="home"/> at each look, so the role door in the same process judges by the registry the lines read.</summary>
    public static void Keep(string home, IEnumerable<(string Repository, string? Workspace)> registered) => _kept[home] = From(registered);

    /// <summary>The map the last look kept for <paramref name="home"/>, or <see cref="None"/>.</summary>
    public static RuleWiderPlaces Read(string home) => _kept.TryGetValue(home, out var kept) ? kept : None;

    /// <summary>The places <paramref name="place"/>'s lines also read before the machine: a repository's workspace; none for any other place.</summary>
    public IReadOnlyList<string> Of(string place)
    {
        const string repository = "repository:";
        return place.StartsWith(repository, StringComparison.Ordinal) && workspaceOf.TryGetValue(place[repository.Length..], out var workspace)
            ? [$"workspace:{workspace}"]
            : [];
    }
}
