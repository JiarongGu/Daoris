namespace Daoris.Driver;

/// <summary>The checkouts the service's registry names, or why it could not be read: what a review's procedure is looked for in.</summary>
public sealed record ChoicesRegistry(IReadOnlyList<(string Repository, string? Workspace, string? Root)>? Checkouts, string? Unread)
{
    /// <summary>Each department's place kind (<see cref="PlaceKind"/>) by its name; one not named is a repository's, as the registry's default is.</summary>
    public IReadOnlyDictionary<string, string> Kinds { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The registry's rows as the choices read them: each checkout, and each department's kind.</summary>
    public static ChoicesRegistry Of(IEnumerable<IRegistryRow> rows)
    {
        var listed = rows.ToList();
        return new([.. listed.Select(row => (row.Repository, (string?)row.Workspace, row.Root))], null)
        {
            Kinds = listed.DistinctBy(row => row.Repository, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(row => row.Repository, row => row.Kind, StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>The place kind of <paramref name="repository"/>, a git repository's where the registry names it otherwise or not at all.</summary>
    public string KindOf(string repository) => Kinds.TryGetValue(repository.Trim(), out var kind) ? kind : PlaceKind.Git;
}
