using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One running environment a repository declares in its own <c>daoris.json</c> under <c>environments</c>: a game server, a
/// docker stack, a database. Its version is the content of its <paramref name="Inputs"/> (<see cref="EnvironmentVersion"/>), never a number.
/// </summary>
/// <param name="Id">Unique in the repository: lower-case letters, digits and hyphens.</param>
/// <param name="Inputs">The repository-relative paths whose content makes the environment what it is: change one and the version moves.</param>
/// <param name="Start">The command that runs it from a tree of its version; the repository's own, Daoris supplies no way to run one.</param>
/// <param name="Ports">The names of the ports it takes; Daoris gives each instance its own number for each name.</param>
/// <param name="Grace">How long an instance no lease holds is kept before it is stopped.</param>
public sealed record EnvironmentSpec(string Id, IReadOnlyList<string> Inputs, string Start, IReadOnlyList<string> Ports, TimeSpan Grace);

/// <summary>A repository's environments as read from its manifest: each entry that reads, and why each other one does not.</summary>
public sealed record DeclaredEnvironments(IReadOnlyList<EnvironmentSpec> Items, IReadOnlyList<string> Problems)
{
    /// <summary>The manifest's blob id on its line, as <see cref="DeclaredDeployables.Blob"/>.</summary>
    public string? Blob { get; init; }
}

/// <summary>
/// Reads the environments a repository declares in its own <c>daoris.json</c>, beside <c>deployables</c>:
/// <c>{"id", "inputs", "start", "ports", "graceSeconds"}</c>. Pure, handed the manifest's text as its line holds it; an entry that does not
/// read is left out with one sentence naming it and why, and the others stand (as <see cref="DeployableDeclaration"/> does).
/// </summary>
public static partial class EnvironmentDeclaration
{
    /// <summary>The manifest's key.</summary>
    public const string Key = "environments";

    /// <summary>The grace an environment keeps an unheld instance where it declares none.</summary>
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromMinutes(10);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex NameShape();

    /// <summary>The environments <paramref name="manifest"/> declares; none, unsaid, where it names no <c>environments</c>.</summary>
    public static DeclaredEnvironments Read(string? manifest, string? blob = null)
    {
        if (manifest is null) return new([], []) { Blob = blob };
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(manifest, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            return new([], [$"{DeclarationNames.Manifest} is not valid JSON, so it declares no environments"]) { Blob = blob };
        }

        if (parsed is not JsonObject root || !root.TryGetPropertyValue(Key, out var listed) || listed is null) return new([], []) { Blob = blob };
        if (listed is not JsonArray entries) return new([], [$"`{Key}` is not a list, so no environment is declared"]) { Blob = blob };

        var items = new List<EnvironmentSpec>();
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

    private static (EnvironmentSpec? Item, string? Problem) Entry(JsonNode? node, string where)
    {
        if (node is not JsonObject entry) return (null, $"{where} is not an object");
        var id = Text(entry["id"]);
        if (id is null) return (null, $"{where} names no id");
        if (!NameShape().IsMatch(id)) return (null, $"{where} `{id}` is no id: lower-case letters, digits and hyphens, starting with a letter or digit, at most 64");

        var named = $"{where} `{id}`";
        if (entry["inputs"] is not JsonArray inputList || inputList.Count == 0) return (null, $"{named} names no inputs whose content is its version");
        var inputs = new List<string>();
        foreach (var raw in inputList)
        {
            var path = Text(raw);
            if (path is null || !Relative(path)) return (null, $"{named} names an input that is not repository-relative: `{raw?.ToJsonString() ?? "null"}`");
            inputs.Add(path);
        }

        var start = Text(entry["start"]);
        if (start is null) return (null, $"{named} names no start command");

        var ports = new List<string>();
        if (entry["ports"] is { } portNode)
        {
            if (portNode is not JsonArray portList) return (null, $"{named} names ports as something that is not a list");
            foreach (var raw in portList)
            {
                var port = Text(raw);
                if (port is null || !NameShape().IsMatch(port)) return (null, $"{named} names a port name that is not lower-case letters, digits and hyphens: `{raw?.ToJsonString() ?? "null"}`");
                ports.Add(port);
            }
        }

        var grace = DefaultGrace;
        if (entry["graceSeconds"] is { } graceNode)
        {
            if (graceNode is not JsonValue value || !value.TryGetValue<int>(out var seconds) || seconds < 0)
            {
                return (null, $"{named} names a graceSeconds that is not a whole number of seconds, 0 or more");
            }

            grace = TimeSpan.FromSeconds(seconds);
        }

        return (new EnvironmentSpec(id, inputs, start, ports, grace), null);
    }

    /// <summary>A path inside the repository: not rooted, no drive, never climbing out.</summary>
    private static bool Relative(string path) =>
        !path.StartsWith('/') && !path.StartsWith('\\') && !path.Contains(':', StringComparison.Ordinal)
        && !path.Replace('\\', '/').Split('/').Contains("..", StringComparer.Ordinal);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Trim() is { Length: > 0 } text ? text : null;
}
