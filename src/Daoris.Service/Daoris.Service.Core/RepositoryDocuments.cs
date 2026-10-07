using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The development documents a repository declares in <c>daoris.json</c>'s <c>documents</c>, as the
/// scanner reads them (DOC5; D122, `docs/2026-10-01-development-documents-design.md` §2.7 and §5).
/// </summary>
/// <remarks>
/// <para><b>A declaration adds a path; it is never required</b> (§5). The scanner still needs no
/// configuration, and a repository that declares nothing is read exactly as before. One that declares its
/// decisions, fixes or archive has that path read before the scanner's candidates, its router read as
/// a document, and its index of where things are read at its headings (ORIENT2e, <see cref="RepositoryScanner"/>).</para>
///
/// <para>🔴 <b>A twin</b> (<c>.claude/knowledge/twins.md</c>, *the development documents*): the CLI's
/// <c>checkDocuments</c> (<c>src/Daoris.Cli/src/documents.ts</c>) reads the same field, and the two share
/// no code. <see cref="Roles"/> is a deliberate copy of its <c>ROLES</c>; <c>RepositoryDocumentsTests</c>
/// holds the rows of its <c>documents-manifest.test.ts</c> in their order.</para>
///
/// <para><b>The one difference is the direction.</b> The CLI is a gate: it refuses the whole manifest
/// for one role it cannot honour (exit 2), so <c>check</c> fails and <c>sync</c> refuses, and the
/// repository hears it from its own tool. This is an indexer, with nobody to tell: a refusal here would
/// drop the repository from search, or fail the refresh for every repository after it (REV3). So a role
/// the CLI refuses is read as undeclared, the roles beside it are read, and a <c>documents</c> that is not
/// a map, or is declared twice, is read as none. Never worse than declaring nothing, never a path the CLI
/// would refuse, and never a guess at which of two declarations was meant.</para>
/// </remarks>
public static class RepositoryDocuments
{
    /// <summary>
    /// The roles the manifest may declare, in the order everything lists them: a copy of the CLI's
    /// <c>ROLES</c> (its <c>role</c> and <c>binding</c>), kept in step by hand and held to it by
    /// <c>RepositoryDocumentsTests</c>, which reads the CLI's file (ORIENT2e). <c>brief</c> and <c>room</c>
    /// take a ceiling and no path. <c>knowledge</c> and <c>skill</c> are roles the CLI refuses here, since
    /// the index lists them from the target, so they are absent, as an unknown role is.
    /// </summary>
    internal static readonly IReadOnlyList<(string Role, bool TakesPath)> Roles =
    [
        ("brief", false),
        ("room", false),
        ("router", true),
        // ORIENT2e (D151 points 4 and 6): the generated index of where things are, after the router as the CLI
        // lists it (ORIENT2b); the scanner reads every file in its folder as entries of its own kind.
        ("index", true),
        ("decisions", true),
        ("backlog", true),
        ("archive", true),
        ("fixes", true),
        ("changelog", true),
        ("glossary", true),
        ("gates", true),
    ];

    /// <summary>One declared role, as the CLI's reading spells it.</summary>
    /// <param name="Role">The role.</param>
    /// <param name="Path">Repository-relative with forward slashes; null for a role that takes only a ceiling.</param>
    /// <param name="Words">Its ceiling in words; null when none is declared.</param>
    public sealed record Declared(string Role, string? Path, long? Words);

    /// <summary>
    /// The declared roles the CLI would accept, in <see cref="Roles"/>' order: each role it refuses is
    /// absent, and the rest are read.
    /// </summary>
    /// <param name="documents">The manifest's field, or null when it has none or holds it twice.</param>
    /// <param name="target">The root the tiers are written to, declared.</param>
    /// <param name="mirrorRoot">Where the layout mirrors a tier, declared; null when it mirrors none.</param>
    internal static IReadOnlyList<Declared> Read(JsonElement? documents, string target, string? mirrorRoot)
    {
        if (documents is not { ValueKind: JsonValueKind.Object } map) return [];

        var members = map.EnumerateObject().ToList();
        var found = new Dictionary<string, Declared>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            // Twice: JSON keeps the last silently, so the CLI refuses to guess, and neither does this.
            if (members.Count(other => other.Name == member.Name) > 1) continue;

            var spec = Roles.FirstOrDefault(row => row.Role == member.Name);
            if (spec.Role is null) continue;
            if (Role(spec.Role, spec.TakesPath, member.Value, target, mirrorRoot) is { } declared)
            {
                found[spec.Role] = declared;
            }
        }
        return Roles.Where(row => found.ContainsKey(row.Role)).Select(row => found[row.Role]).ToList();
    }

    /// <summary>One role's value as the CLI's <c>checkDocuments</c> reads it, or null where it refuses it.</summary>
    private static Declared? Role(string role, bool takesPath, JsonElement value, string target, string? mirrorRoot)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return takesPath && Readable(value.GetString()!, target, mirrorRoot) is { } path
                ? new Declared(role, path, null)
                : null;
        }
        if (value.ValueKind != JsonValueKind.Object) return null;

        JsonElement? declaredPath = null;
        long? words = null;
        foreach (var field in value.EnumerateObject())
        {
            switch (field.Name)
            {
                // A path on a role that takes only a ceiling is refused whatever it holds, null included.
                case "path" when takesPath:
                    declaredPath = field.Value;
                    break;
                case "words":
                    words = Ceiling(field.Value);
                    if (words is null) return null;
                    break;
                default:
                    return null;
            }
        }
        if (!takesPath) return new Declared(role, null, words);
        if (declaredPath is not { ValueKind: JsonValueKind.String } text) return null;
        return Readable(text.GetString()!, target, mirrorRoot) is { } readable ? new Declared(role, readable, words) : null;
    }

    /// <summary>
    /// A ceiling the CLI accepts: a whole number above zero, however JSON spells it (<c>1e3</c> and
    /// <c>2500.0</c> are whole numbers to the CLI's reader too).
    /// </summary>
    private static long? Ceiling(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var words)
        && words > 0
        && words == Math.Floor(words)
        && words < long.MaxValue
            ? (long)words
            : null;

    /// <summary>
    /// A declared path as the scanner reads it, or null where the CLI refuses it: leaving the repository,
    /// the repository's root, inside the target or the mirror root (D18, §2.7).
    /// </summary>
    /// <remarks>
    /// Read-side only: the path is resolved, and both its spelling and where it lands are checked. The CLI
    /// keeps a path's spelling and checks the spelling, so a <c>..</c> inside one (<c>docs/..</c>,
    /// <c>x/../.claude/…</c>) can name the root or a doctrine root it would refuse spelled plainly. A reader
    /// is never looser than its writer: the root is never read as a folder of records, nor a tier as the
    /// repository's own. What it reads is where the path lands, so one file has one spelling in the index.
    /// </remarks>
    private static string? Readable(string raw, string target, string? mirrorRoot)
    {
        var path = RepositoryLayout.Declared(raw);
        if (RepositoryLayout.Escapes(path)) return null;

        // Escapes said no `..` climbs past the root, so the stack never runs dry.
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..") parts.RemoveAt(parts.Count - 1);
            else parts.Add(part);
        }
        var resolved = string.Join('/', parts);

        if (resolved.Length == 0) return null;
        // Both the spelling the CLI checks and the place it names: refused by either, read by neither.
        foreach (var root in new[] { target, mirrorRoot })
        {
            if (root is { Length: > 0 } && (RepositoryLayout.Within(path, root) || RepositoryLayout.Within(resolved, root))) return null;
        }
        return resolved;
    }
}
