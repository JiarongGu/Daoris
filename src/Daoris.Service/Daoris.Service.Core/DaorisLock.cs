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

    private DaorisLock(HashSet<string> canonicalPaths, IReadOnlyDictionary<string, IReadOnlyList<Span>>? spans = null)
    {
        _canonicalPaths = canonicalPaths;
        Spans = spans ?? new Dictionary<string, IReadOnlyList<Span>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One canonical document that lives as a SPAN inside a file rather than as a file (D59).
    /// </summary>
    /// <param name="Source">
    /// <c>pack/source</c>, exactly as the provenance line inside the region spells it — which is how
    /// the two halves of this project, sharing no code, agree on which rule is which.
    /// </param>
    /// <param name="Target">The canonical identity, e.g. <c>rules/sensitive-info.md</c>.</param>
    public readonly record struct Span(string Source, string Target);

    /// <summary>
    /// Which spans live in which file, repository-relative. Empty for a repository whose doctrine is
    /// all files — which is every repository that has not synced since the tier moved.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Span>> Spans { get; }

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
            var spans = new Dictionary<string, List<Span>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("target", out var value)) continue;
                if (value.GetString() is not { Length: > 0 } relative) continue;
                paths.Add(Normalize($"{target}/{relative}"));

                // `in` says this entry is a span inside a file rather than a file of its own (D59),
                // and the file is repository-relative — NOT relative to the target directory, because
                // the file belongs to the repository rather than to daoris.
                if (!entry.TryGetProperty("in", out var within)) continue;
                if (within.GetString() is not { Length: > 0 } host) continue;

                var pack = entry.TryGetProperty("pack", out var p) ? p.GetString() : null;
                var source = entry.TryGetProperty("source", out var s) ? s.GetString() : null;
                if (pack is not { Length: > 0 } || source is not { Length: > 0 }) continue;

                if (!spans.TryGetValue(host, out var held)) spans[host] = held = [];
                held.Add(new Span($"{pack}/{source}", relative));
            }

            return new DaorisLock(
                paths,
                spans.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<Span>)pair.Value,
                    StringComparer.OrdinalIgnoreCase));
        }
        // Wrong shape as well as wrong syntax (REV3): an element read on a number or an array throws
        // InvalidOperationException, and a lock like that failed the whole refresh.
        catch (Exception error) when (error is JsonException or InvalidOperationException)
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
        // Wrong shape as well as wrong syntax (REV3): an element read on a number or an array throws
        // InvalidOperationException, and a lock like that failed the whole refresh.
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return ".claude";
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');
}
