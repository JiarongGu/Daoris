namespace Daoris.Knowledge;

/// <summary>Reads repositories from a directory on this machine — the local-first source.</summary>
/// <remarks>
/// <b>The folder is re-read on every read, not enumerated once.</b> It used to snapshot the directory
/// list when the source was constructed, which quietly made `refresh` — whose whole promise is
/// "re-read every repository from disk" — re-read only the repositories that existed when the process
/// launched. A project born today was invisible until someone restarted the host, and nothing said so:
/// the refresh reported success and a count that looked right. That is the ghost rule's mirror image,
/// and it fails the same way — by looking fine.
/// </remarks>
public sealed class FileSystemKnowledgeSource(Func<IReadOnlyList<string>> repositoryRoots) : IKnowledgeSource
{
    private readonly RepositoryScanner _scanner = new();

    /// <summary>An explicit, fixed set of repository roots.</summary>
    public FileSystemKnowledgeSource(IReadOnlyList<string> repositoryRoots)
        : this(() => repositoryRoots)
    {
    }

    /// <summary>
    /// Every immediate subdirectory of a folder — the usual "all my repositories" case, listed afresh
    /// each time so a repository that appears later appears in the next refresh.
    /// </summary>
    public static FileSystemKnowledgeSource UnderFolder(string folder) =>
        new(() => Directory.Exists(folder)
            ? Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal).ToList()
            : []);

    public string Name { get; init; } = "filesystem";

    public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default)
    {
        var entries = new List<KnowledgeEntry>();
        foreach (var root in repositoryRoots())
        {
            ct.ThrowIfCancellationRequested();
            entries.AddRange(_scanner.Scan(root));
        }

        return Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }
}
