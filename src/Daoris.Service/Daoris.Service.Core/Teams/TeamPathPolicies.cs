using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>Reads a repository's <see cref="TeamPathPolicy"/> from the manifest at its registered root: the records <c>daoris.json</c> declares.</summary>
public static class TeamPathPolicies
{
    private static readonly string[] Files = ["backlog", "archive", "fixes", "changelog"];

    /// <summary>The policy of the repository registered as <paramref name="repository"/>; none where it has no root or no manifest.</summary>
    public static async Task<TeamPathPolicy> OfAsync(KnowledgeService service, string repository, CancellationToken ct)
    {
        var registered = (await service.RegistryAsync(ct: ct).ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase));
        if (registered?.Root is not { Length: > 0 } root) return TeamPathPolicy.None;
        try
        {
            var records = TeamPathPolicy.None;
            var manifest = Path.Combine(root, "daoris.json");
            if (File.Exists(manifest))
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifest, ct).ConfigureAwait(false));
                records = Read(document.RootElement);
            }

            var attributes = Path.Combine(root, ".gitattributes");
            var generated = File.Exists(attributes) ? GeneratedIn(await File.ReadAllTextAsync(attributes, ct).ConfigureAwait(false)) : [];
            return records with { Generated = generated };
        }
        catch (Exception unreadable) when (unreadable is JsonException or IOException or UnauthorizedAccessException)
        {
            return TeamPathPolicy.None;
        }
    }

    /// <summary>The paths a <c>.gitattributes</c> marks <c>linguist-generated</c>; a wildcard with no slash, which git matches at any depth, names no path and is read as nothing.</summary>
    internal static IReadOnlyList<string> GeneratedIn(string attributes)
    {
        var paths = new List<string>();
        foreach (var raw in attributes.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            var marked = fields.Skip(1).LastOrDefault(field => field.TrimStart('-').StartsWith(Generated, StringComparison.Ordinal)
                && (field.Length == Generated.Length || field.StartsWith(Generated + "=", StringComparison.Ordinal) || field[0] == '-'));
            if (marked is not { } mark || mark[0] == '-' || mark.EndsWith("=false", StringComparison.Ordinal)) continue;
            var path = fields[0].TrimStart('/');
            if (path.IndexOfAny(['*', '?', '[']) >= 0 && !fields[0].Contains('/')) continue;
            if (QuestPaths.Fits(path)) paths.Add(path);
        }

        return paths;
    }

    private const string Generated = "linguist-generated";

    /// <summary>The records a manifest's <c>documents</c> declare: each file's path, and the areas folder as everything under it.</summary>
    internal static TeamPathPolicy Read(JsonElement manifest)
    {
        if (manifest.ValueKind != JsonValueKind.Object || !manifest.TryGetProperty("documents", out var documents)
            || documents.ValueKind != JsonValueKind.Object)
        {
            return TeamPathPolicy.None;
        }

        var records = new List<string>();
        foreach (var role in Files)
        {
            if (PathOf(documents, role) is { } path) records.Add(path);
        }

        if (PathOf(documents, "areas") is { } areas) records.Add(areas.TrimEnd('/') + "/**");
        return new(records, []);
    }

    private static string? PathOf(JsonElement documents, string role)
    {
        if (!documents.TryGetProperty(role, out var value)) return null;
        var path = value.ValueKind == JsonValueKind.String ? value.GetString()
            : value.ValueKind == JsonValueKind.Object && value.TryGetProperty("path", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString()
            : null;
        return path is { Length: > 0 } && QuestPaths.Fits(path) ? path : null;
    }
}
