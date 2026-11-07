namespace Daoris.Driver;

/// <summary>
/// A repository's workspace as its wider place for the supervisor's rules: a line of
/// <c>repository:engine</c> reads <c>workspace:main</c>'s rules where its repository holds none on a count, so the judge weighs a write
/// on the same chain the executors read. The map is the registry's as the store keeps it (<see cref="RuleWiderPlaceStore"/>).
/// </summary>
public sealed class RuleWiderPlaces(IReadOnlyDictionary<string, string> workspaceOf)
{
    /// <summary>No repository's workspace known.</summary>
    public static RuleWiderPlaces None { get; } = new(new Dictionary<string, string>());

    /// <summary>
    /// Why the kept map did not read, where it did not: a store that fails is told apart from one before its step, so the supervisors'
    /// resolver makes the decisions it cannot place the person's rather than skipping a repository's workspace.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>A map that did not read, said by <paramref name="error"/>: no repository placed, every place on its own chain.</summary>
    public static RuleWiderPlaces Unread(string error) => new(new Dictionary<string, string>()) { Error = error };

    /// <summary>Each repository's workspace, for those registered in one.</summary>
    public IReadOnlyDictionary<string, string> WorkspaceOf { get; } = workspaceOf;

    /// <summary>The map from each repository to its workspace, for those registered in one.</summary>
    public static RuleWiderPlaces From(IEnumerable<(string Repository, string? Workspace)> registered) =>
        new(registered.Where(each => each.Workspace is { Length: > 0 })
            .GroupBy(each => each.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Workspace!, StringComparer.OrdinalIgnoreCase));

    /// <summary>The places <paramref name="place"/>'s lines also read before the machine: a repository's workspace; none for any other place.</summary>
    public IReadOnlyList<string> Of(string place)
    {
        const string repository = "repository:";
        return place.StartsWith(repository, StringComparison.Ordinal) && WorkspaceOf.TryGetValue(place[repository.Length..], out var workspace)
            ? [$"workspace:{workspace}"]
            : [];
    }

    /// <summary>Each repository place's wider places, as <c>ROLES</c> answers them.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Chains() =>
        WorkspaceOf.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => $"repository:{pair.Key}", pair => (IReadOnlyList<string>)[$"workspace:{pair.Value}"], StringComparer.Ordinal);
}
