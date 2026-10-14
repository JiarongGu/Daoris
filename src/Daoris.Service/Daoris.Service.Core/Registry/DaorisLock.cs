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
///
/// 🔴 <b>The lock, not the manifest, says where the files are</b> (D117 §5.1, LAYOUT4). Since the agents
/// layout the lock carries the descriptor and root its entries were written under, and the mirrors it
/// wrote. A twin of the CLI's <c>lockLayout</c> (<c>src/Daoris.Cli/src/layout.ts</c>), which shares no
/// code with this; <see cref="RepositoryLayout"/> names the rest of the twin and its test table.
/// </remarks>
public sealed class DaorisLock
{
    private readonly HashSet<string> _canonicalPaths;
    private readonly HashSet<string> _mirrors;

    private DaorisLock(
        HashSet<string> canonicalPaths,
        IReadOnlyDictionary<string, IReadOnlyList<Span>>? spans = null,
        string? harness = null,
        string? target = null,
        HashSet<string>? mirrors = null)
    {
        _canonicalPaths = canonicalPaths;
        Spans = spans ?? new Dictionary<string, IReadOnlyList<Span>>(StringComparer.OrdinalIgnoreCase);
        Harness = harness;
        Target = target;
        _mirrors = mirrors ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The descriptor the lock's entries were written under: <c>claude-code</c> when the lock names
    /// none, the only layout written before D117. Null when there is no lock, or it is read as none.
    /// </summary>
    public string? Harness { get; }

    /// <summary>
    /// The root the lock's entries were written under, repository-relative with forward slashes: the
    /// lock's own <c>target</c>, else the manifest's for a lock on the older layout, else the
    /// descriptor's. Null when there is no lock, or it is read as none, and then nothing says where
    /// daoris wrote anything.
    /// </summary>
    public string? Target { get; }

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

            // Lock targets are relative to the root the lock was written under. A lock on the older
            // layout does not name it, so the manifest has to be read for it (the CLI's lockLayout).
            if (Layout(document.RootElement, repositoryRoot) is not { } layout) return Empty;
            var (harness, target) = layout;

            var mirrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.TryGetProperty("mirrors", out var written) && written.ValueKind == JsonValueKind.Array)
            {
                foreach (var mirror in written.EnumerateArray())
                {
                    // `path` is repository-relative: a mirror is written outside the target, for the one
                    // agent that does not read there (D117 §3.2).
                    if (mirror.TryGetProperty("path", out var path) && path.GetString() is { Length: > 0 } copy)
                    {
                        mirrors.Add(Normalize(copy));
                    }
                }
            }

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
                    StringComparer.OrdinalIgnoreCase),
                harness,
                target,
                mirrors);
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

    /// <summary>
    /// Whether a repository-relative path is a mirror the lock records: a copy of a skill for the one
    /// agent that reads only its own folder (D117 §3.2). Never indexed, so a search finds each skill
    /// once per repository, at its source.
    /// </summary>
    public bool IsMirror(string relativePath) => _mirrors.Contains(Normalize(relativePath));

    /// <summary>
    /// The descriptor and root a lock was written under, the CLI's <c>lockLayout</c> row for row; null
    /// for a lock this reads as none.
    /// </summary>
    /// <remarks>
    /// The CLI refuses a lock whose target leaves the repository, or whose descriptor it does not know.
    /// Read here as no lock at all, which is the safe direction <see cref="Read"/> already takes: every
    /// document is then the repository's own, and none is read outside it.
    /// </remarks>
    private static (string Harness, string Target)? Layout(JsonElement lockRoot, string repositoryRoot)
    {
        // A field of the wrong shape throws InvalidOperationException here, which Read turns into Empty.
        var named = lockRoot.TryGetProperty("harness", out var h) ? h.GetString() : null;
        var harness = named ?? RepositoryLayout.Older;
        if (!RepositoryLayout.Descriptors.TryGetValue(harness, out var descriptor)) return null;

        string target;
        if (lockRoot.TryGetProperty("target", out var t))
        {
            target = t.GetString() ?? descriptor.DefaultTarget;
        }
        else
        {
            // Neither field: an older build wrote it, at the manifest's own target while the manifest is
            // still on that layout, and at `.claude` once the manifest has been flipped past it.
            var manifest = RepositoryLayout.Manifest.Read(repositoryRoot);
            target = named is null && manifest.OnOlderLayout
                ? manifest.Target ?? descriptor.DefaultTarget
                : descriptor.DefaultTarget;
        }

        var normal = RepositoryLayout.Declared(target);
        return RepositoryLayout.Escapes(normal) ? null : (harness, normal);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');
}
