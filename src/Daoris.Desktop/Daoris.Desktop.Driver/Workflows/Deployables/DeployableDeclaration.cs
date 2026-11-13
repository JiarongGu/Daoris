using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Reads the deployables a repository declares in its own <c>daoris.json</c>, beside its <c>domain</c>:
/// <c>{"id", "paths", "provides": [{"resource", "mode"}], "consumes": [...]}</c>. Pure, handed the manifest's text as its line holds it.
/// </summary>
/// <remarks>
/// An entry that does not read is left out with one sentence naming it and why, and the others stand, so one slip never hides a
/// repository's other deployables from a plan. The manifest's other fields are the registration's to judge; a manifest that does not read
/// declares none here and says so.
/// </remarks>
public static partial class DeployableDeclaration
{
    /// <summary>The manifest's key.</summary>
    public const string Key = "deployables";

    /// <summary>How a deployable provides a resource: the resource's shape, or its schema's changes.</summary>
    public static readonly string[] Provides = ["owns", "migrates"];

    /// <summary>How a deployable consumes one: maps a schema without migrating it, calls an endpoint, or reads a table, key or flag.</summary>
    public static readonly string[] Consumes = ["mirror", "calls", "reads"];

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex IdShape();

    /// <summary>The deployables <paramref name="manifest"/> declares; none, unsaid, where it names no <c>deployables</c>.</summary>
    /// <param name="blob">The manifest's blob id where it was read from a line, kept on the answer.</param>
    public static DeclaredDeployables Read(string? manifest, string? blob = null)
    {
        if (manifest is null) return new([], []) { Blob = blob };
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(manifest, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            return new([], [$"{DeclarationNames.Manifest} is not valid JSON, so it declares no deployables"]) { Blob = blob };
        }

        if (parsed is not JsonObject root || !root.TryGetPropertyValue(Key, out var listed) || listed is null) return new([], []) { Blob = blob };
        if (listed is not JsonArray entries) return new([], [$"`{Key}` is not a list, so no deployable is declared"]) { Blob = blob };

        var items = new List<Deployable>();
        var problems = new List<string>();
        for (var at = 0; at < entries.Count; at++)
        {
            var (item, problem) = Entry(entries[at], $"{Key}[{at}]");
            if (problem is null && item is not null && items.Any(each => string.Equals(each.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
            {
                problem = $"{Key}[{at}] `{item.Id}` is declared twice; the first stands";
            }

            if (problem is not null) problems.Add(problem);
            else if (item is not null) items.Add(item);
        }

        return new(items, problems) { Blob = blob };
    }

    /// <summary>One entry read, or why it is left out.</summary>
    private static (Deployable? Item, string? Problem) Entry(JsonNode? node, string where)
    {
        if (node is not JsonObject entry) return (null, $"{where} is not an object");
        var id = Text(entry["id"]);
        if (id is null) return (null, $"{where} names no id");
        if (!IdShape().IsMatch(id))
        {
            return (null, $"{where} `{id}` is no id: lower-case letters, digits and hyphens, starting with a letter or digit, at most 64");
        }

        var named = $"{where} `{id}`";
        if (entry["paths"] is not JsonArray pathList || pathList.Count == 0) return (null, $"{named} names no paths that build it");
        var paths = new List<string>();
        foreach (var raw in pathList)
        {
            var path = Text(raw);
            if (path is null || !Relative(path)) return (null, $"{named} names a path that is not repository-relative: `{raw?.ToJsonString() ?? "null"}`");
            paths.Add(path);
        }

        var (provides, provided) = Uses(entry["provides"], Provides, $"{named} provides");
        if (provided is not null) return (null, provided);
        var (consumes, consumed) = Uses(entry["consumes"], Consumes, $"{named} consumes");
        if (consumed is not null) return (null, consumed);
        return (new Deployable(id, paths, provides, consumes), null);
    }

    /// <summary>A <c>provides</c> or <c>consumes</c> list, absent being none, each a resource and one of <paramref name="modes"/>.</summary>
    private static (IReadOnlyList<DeployableUse> Uses, string? Problem) Uses(JsonNode? node, string[] modes, string said)
    {
        if (node is null) return ([], null);
        if (node is not JsonArray list) return ([], $"{said} something that is not a list");
        var uses = new List<DeployableUse>();
        foreach (var each in list)
        {
            if (each is not JsonObject use || Text(use["resource"]) is not { } resource) return ([], $"{said} an entry that names no resource");
            var mode = Text(use["mode"]);
            if (mode is null || !modes.Contains(mode, StringComparer.Ordinal))
            {
                return ([], $"{said} `{resource}` as `{mode ?? "nothing"}`, which is none of {string.Join(" or ", modes)}");
            }

            uses.Add(new DeployableUse(resource, mode));
        }

        return (uses, null);
    }

    /// <summary>A path inside the repository: not rooted, no drive, never climbing out.</summary>
    private static bool Relative(string path) =>
        !path.StartsWith('/') && !path.StartsWith('\\') && !path.Contains(':', StringComparison.Ordinal)
        && !path.Replace('\\', '/').Split('/').Contains("..", StringComparer.Ordinal);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Trim() is { Length: > 0 } text ? text : null;
}
