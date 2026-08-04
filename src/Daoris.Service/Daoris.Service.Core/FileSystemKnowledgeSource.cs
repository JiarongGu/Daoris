namespace Daoris.Knowledge;

/// <summary>Reads repositories from a directory on this machine — the local-first source.</summary>
public sealed class FileSystemKnowledgeSource(IReadOnlyList<string> repositoryRoots) : IKnowledgeSource
{
    private readonly RepositoryScanner _scanner = new();

    /// <summary>Every immediate subdirectory of a folder — the usual "all my repositories" case.</summary>
    public static FileSystemKnowledgeSource UnderFolder(string folder) =>
        new(Directory.Exists(folder)
            ? Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal).ToList()
            : []);

    public string Name { get; init; } = "filesystem";

    public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default)
    {
        var entries = new List<KnowledgeEntry>();
        foreach (var root in repositoryRoots)
        {
            ct.ThrowIfCancellationRequested();
            entries.AddRange(_scanner.Scan(root));
        }

        return Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }
}
