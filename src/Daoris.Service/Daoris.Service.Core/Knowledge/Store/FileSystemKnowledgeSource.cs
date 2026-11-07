namespace Daoris.Knowledge;

/// <summary>Reads repositories from a directory on this machine — the local-first source.</summary>
/// <remarks>
/// <b>The folder is re-read on every read, not enumerated once.</b> A snapshot of the directory
/// list taken when the source is constructed would quietly make `refresh` — whose whole promise is
/// "re-read every repository from disk" — re-read only the repositories that existed when the process
/// launched. A project born today would be invisible until someone restarted the host, and nothing would say so:
/// the refresh would report success and a count that looks right. That is the ghost rule's mirror image,
/// and it fails the same way — by looking fine.
/// </remarks>
/// <param name="repositoryRoots">The checkouts to read, listed afresh on every read.</param>
/// <param name="scanner">
/// How each is read: the scanner a deployment configured, with the documents and the index it names,
/// or the default, which reads neither.
/// </param>
public sealed class FileSystemKnowledgeSource(
    Func<IReadOnlyList<string>> repositoryRoots, RepositoryScanner? scanner = null) : IRootReadingSource
{
    private readonly RepositoryScanner _scanner = scanner ?? new();

    /// <summary>An explicit, fixed set of repository roots.</summary>
    public FileSystemKnowledgeSource(IReadOnlyList<string> repositoryRoots, RepositoryScanner? scanner = null)
        : this(() => repositoryRoots, scanner)
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

    public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) => ReadRootsAsync(repositoryRoots(), ct);

    public Task<IReadOnlyList<KnowledgeEntry>> ReadRootsAsync(IReadOnlyList<string> roots, CancellationToken ct = default)
    {
        var entries = new List<KnowledgeEntry>();
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            entries.AddRange(_scanner.Scan(root));
        }

        return Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }
}
