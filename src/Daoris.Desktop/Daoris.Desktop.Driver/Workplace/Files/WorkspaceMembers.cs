using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A package manager's workspace members as the manifest at the commit names them: npm's and yarn's <c>workspaces</c> in
/// <c>package.json</c> (a list, or an object holding <c>packages</c>) and pnpm's <c>packages</c> list in <c>pnpm-workspace.yaml</c>,
/// each a folder or a one-level pattern with its <c>./</c> and trailing <c>/</c> dropped. A negated entry names a folder no member
/// owns, so it adds none. Read from text git's objects gave, never a working file.
/// </summary>
internal static class WorkspaceMembers
{
    /// <summary>The members <paramref name="manifest"/>, a <c>package.json</c> object, names.</summary>
    internal static IReadOnlyList<string> OfManifest(JsonElement manifest)
    {
        if (!manifest.TryGetProperty("workspaces", out var listed)) return [];
        return Clean(listed.ValueKind == JsonValueKind.Object ? Json.Strings(listed, "packages") : Json.Strings(manifest, "workspaces"));
    }

    /// <summary>
    /// The members the <c>packages</c> key of a <c>pnpm-workspace.yaml</c> names, as a block list or a flow list. Only that key is read:
    /// the file's other keys are settings, and a file naming no <c>packages</c> has no members beyond its own folder.
    /// </summary>
    internal static IReadOnlyList<string> OfPnpm(string yaml)
    {
        var lines = yaml.ReplaceLineEndings("\n").Split('\n');
        var items = new List<string>();
        for (var at = 0; at < lines.Length; at++)
        {
            var line = lines[at];
            if (!line.StartsWith("packages:", StringComparison.Ordinal)) continue;
            var rest = Uncommented(line["packages:".Length..]).Trim();
            if (rest.StartsWith('['))
            {
                items.AddRange(rest.TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Unquoted));
                break;
            }

            for (var next = at + 1; next < lines.Length; next++)
            {
                var entry = Uncommented(lines[next]);
                if (entry.Trim().Length == 0) continue;
                if (!char.IsWhiteSpace(entry[0])) break;
                var trimmed = entry.Trim();
                if (trimmed.StartsWith('-')) items.Add(Unquoted(trimmed[1..].Trim()));
            }

            break;
        }

        return Clean(items);
    }

    /// <summary>The <c>node_modules/</c> path of each member, repository-relative below <paramref name="folder"/> (empty or ending in <c>/</c>).</summary>
    internal static IEnumerable<string> Modules(string folder, IEnumerable<string> members) => members.Select(member => $"{folder}{member}/node_modules/");

    private static List<string> Clean(IEnumerable<string> entries) =>
    [
        .. entries.Select(entry => entry.Trim()).Where(entry => entry.Length > 0 && entry[0] != '!')
            .Select(entry => entry.StartsWith("./", StringComparison.Ordinal) ? entry[2..] : entry).Select(entry => entry.Trim('/'))
            .Where(entry => entry.Length > 0 && entry != ".").Distinct(StringComparer.Ordinal),
    ];

    private static string Uncommented(string line)
    {
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            if (quote == '\0' && line[i] is '"' or '\'') quote = line[i];
            else if (line[i] == quote) quote = '\0';
            else if (quote == '\0' && line[i] == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1]))) return line[..i];
        }

        return line;
    }

    private static string Unquoted(string text) =>
        text.Length >= 2 && text[0] == text[^1] && text[0] is '"' or '\'' ? text[1..^1] : text;
}
