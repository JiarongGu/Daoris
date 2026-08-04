using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The set of paths daoris materialized into a repository, read from its <c>daoris.lock</c>.
/// </summary>
/// <remarks>
/// The lock is the authority on provenance, which is the same invariant the CLI is built on: anything
/// absent from it is the repository's own. Re-deriving that by comparing content against a canon
/// would be a second answer to a question already answered, and the two would disagree.
///
/// A repository that has not adopted daoris has no lock, and everything in it is local — which is
/// correct rather than a special case, and is why an absent lock is not an error.
/// </remarks>
public sealed class DaorisLock
{
    private readonly HashSet<string> _canonicalPaths;

    private DaorisLock(HashSet<string> canonicalPaths) => _canonicalPaths = canonicalPaths;

    /// <summary>A lock claiming nothing — for a repository that has not adopted daoris.</summary>
    public static DaorisLock Empty { get; } = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>How many paths the lock claims.</summary>
    public int Count => _canonicalPaths.Count;

    /// <summary>
    /// Read the lock beside a repository root. Returns <see cref="Empty"/> when there is none, or
    /// when it cannot be parsed — provenance is an optimisation for the index, and failing an entire
    /// ingest because one repository's lock is malformed would trade the whole corpus for a detail.
    /// Treating it as "everything is local" is the safe direction: it over-reports local content
    /// rather than silently hiding it.
    /// </summary>
    public static DaorisLock Read(string repositoryRoot)
    {
        var file = Path.Combine(repositoryRoot, "daoris.lock");
        if (!File.Exists(file)) return Empty;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("entries", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                return Empty;
            }

            // Lock targets are relative to the MANIFEST's target directory, and the lock does not
            // repeat it — so the manifest has to be read for it. Defaulting to `.claude` matches the
            // CLI's own default for a manifest that omits it.
            var target = ReadTargetDirectory(repositoryRoot);

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("target", out var value)) continue;
                if (value.GetString() is not { Length: > 0 } relative) continue;
                paths.Add(Normalize($"{target}/{relative}"));
            }

            return new DaorisLock(paths);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>Whether a repository-relative path was materialized by daoris.</summary>
    public Provenance ProvenanceOf(string relativePath) =>
        _canonicalPaths.Contains(Normalize(relativePath)) ? Provenance.Canonical : Provenance.Local;

    private static string ReadTargetDirectory(string repositoryRoot)
    {
        var manifest = Path.Combine(repositoryRoot, "daoris.json");
        if (!File.Exists(manifest)) return ".claude";

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            return document.RootElement.TryGetProperty("target", out var value)
                   && value.GetString() is { Length: > 0 } target
                ? target
                : ".claude";
        }
        catch (JsonException)
        {
            return ".claude";
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');
}
