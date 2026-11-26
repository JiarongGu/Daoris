using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What each agent door hands its agent, declared once and read from the door: a provider's <c>grants.json</c> (beside its
/// <c>provider.json</c>) holds one row per door, and <see cref="Observe"/> builds the same row from what the door's own spawn and
/// wire do, so a census can say where they differ. A row is: <c>servers</c> (how the connectors reach the agent, by what flag, which
/// names), <c>approval</c> (the route a call the mode would ask about takes to the person), <c>note</c> (how a word reaches a running
/// turn), <c>access</c> (the arguments, variables and wire mode of the asking, the full and the driven start), and <c>proof</c> (the test
/// that reads the tools the agent lists on a stand-in model, or why none is written yet and the open row that writes it).
/// </summary>
internal static class DoorGrants
{
    public const string FileName = "grants.json";

    /// <summary>The routes a connector takes to the agent.</summary>
    public const string ConfigFile = "config-file", SessionNew = "session-new", NoRoute = "none";

    /// <summary>The routes a request to ask the person takes.</summary>
    public const string PreToolUseHook = "pre-tool-use-hook", PermissionRequest = "permission-request";

    /// <summary>The fields of a row: the grant's, then the proof.</summary>
    private static readonly string[] Fields = ["id", "servers", "approval", "note", "access", "proof"];

    private static string ProvidersFolder => Path.Combine(SourceFiles.Project(SourceFiles.DriverProject), "Agents", "Providers");

    /// <summary>The file a provider's rows are in, beside its <c>provider.json</c>; null for a provider with no folder.</summary>
    public static string? FileOf(string provider) =>
        ConformancePending.Folders().TryGetValue(provider, out var folder) ? Path.Combine(folder, FileName) : null;

    /// <summary>A provider's declared rows by door id, and what breaks the layout; a provider with no file has none.</summary>
    public static (IReadOnlyDictionary<string, JsonObject> Rows, IReadOnlyList<string> Problems) Declared(string provider)
    {
        var rows = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        var file = FileOf(provider);
        if (file is null || !File.Exists(file))
        {
            problems.Add($"{provider}: no {FileName} beside its provider.json: each provider declares what each of its doors hands its agent");
            return (rows, problems);
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(file));
        }
        catch (JsonException error)
        {
            return (rows, [$"{ConformancePending.Relative(file)}: not JSON ({error.Message})"]);
        }

        if (root is not JsonObject { } holder || holder["doors"] is not JsonArray doors)
        {
            return (rows, [$"{ConformancePending.Relative(file)}: the file is an object whose `doors` is an array of rows"]);
        }

        var at = 0;
        foreach (var node in doors)
        {
            at++;
            var where = $"{ConformancePending.Relative(file)}, row {at}";
            if (node is not JsonObject row || (string?)row["id"] is not { Length: > 0 } id)
            {
                problems.Add($"{where}: a row is an object with an `id`");
                continue;
            }

            problems.AddRange(row.Select(pair => pair.Key).Where(name => !Fields.Contains(name)).Select(name => $"{where}: `{name}` is no field of a row ({string.Join(", ", Fields)})"));
            problems.AddRange(Fields.Where(name => row[name] is null).Select(name => $"{where} ({id}): `{name}` is missing"));
            if (!rows.TryAdd(id, row)) problems.Add($"{where}: door `{id}` is declared twice");
        }

        return (rows, problems);
    }

    /// <summary>A row's grant: everything but its id and its proof, which is what a door's spawn is held to.</summary>
    public static JsonObject Grant(JsonObject row)
    {
        var grant = new JsonObject();
        foreach (var (name, value) in row)
        {
            if (name is not ("id" or "proof")) grant[name] = value?.DeepClone();
        }

        return grant;
    }

    /// <summary>
    /// The grant read from the door: what its spawn hands <paramref name="plan"/>'s servers, how a call to be asked about reaches the person,
    /// how a word reaches a running turn, and the arguments of the start in each mode it states.
    /// </summary>
    public static JsonObject Observe(Provider provider, AgentDoor door, ConnectorPlan plan, string home, string root)
    {
        var spawn = door.Spawned();
        return new JsonObject
        {
            ["servers"] = Servers(spawn, door, plan, home),
            ["approval"] = Approval(provider, door, home),
            ["note"] = Note(door),
            ["access"] = new JsonObject
            {
                ["asking"] = Mode(spawn, door, door.Postures.Asking, root),
                ["full"] = Mode(spawn, door, door.Postures.Full, root),
                ["driven"] = Mode(spawn, door, null, root),
            },
        };
    }

    private static JsonObject Servers(ISpawn spawn, AgentDoor door, ConnectorPlan plan, string home)
    {
        if (spawn is IServerHanding handing)
        {
            var info = new ProcessStartInfo("program");
            var file = handing.Hand(info, home, plan);
            var arguments = info.ArgumentList.ToList();
            var at = file is null ? -1 : arguments.IndexOf(file);
            if (file is null || at < 1) return Route(NoRoute, null, []);
            var names = JsonNode.Parse(File.ReadAllText(file))?["mcpServers"] is JsonObject served ? served.Select(pair => pair.Key) : [];
            return Route(ConfigFile, arguments[at - 1], names);
        }

        if (door.Transport != Transport.Protocol) return Route(NoRoute, null, []);
        var offered = JsonSerializer.SerializeToNode(AcpProtocol.Offer(plan, spawn.OwnServers(OwnTools.Machine(home, new ProcessStartInfo("program"), connectorOnly: true)))) as JsonArray ?? [];
        return Route(SessionNew, null, offered.Select(server => (string?)server?["name"] ?? ""));
    }

    private static JsonObject Route(string route, string? flag, IEnumerable<string> names) =>
        new() { ["route"] = route, ["flag"] = flag, ["names"] = new JsonArray([.. names.Order(StringComparer.Ordinal).Select(name => (JsonNode?)name)]) };

    /// <summary>
    /// A native door's call to ask about reaches the person by the hook its rule language writes into the session's settings; a protocol door's
    /// by its wire's request, which the driver holds.
    /// </summary>
    private static string Approval(Provider provider, AgentDoor door, string home)
    {
        if (door.Transport == Transport.Protocol) return PermissionRequest;
        if (!provider.Rules.TryGet(out var language)) return NoRoute;
        var file = language.Write(home, "grants-" + door.Id.Value, new HandedRules(RuleLists.Empty, [], null, null) { Asked = true });
        return File.ReadAllText(file).Contains("PreToolUse", StringComparison.Ordinal) ? PreToolUseHook : NoRoute;
    }

    /// <summary>How a word the person sends reaches a running turn, from what the door states of its wire.</summary>
    private static string Note(AgentDoor door)
    {
        if (door.Transport == Transport.Native)
        {
            return door.Dialect.TryGet(out var dialect) && dialect is INativeDialect native && native.FrameSteer("note", "id") is not null
                ? "stdin-line"
                : "after-turn";
        }

        if (AbilityReading.Does(door, AbilityName.Steering)) return "steer-request";
        return AbilityReading.Does(door, AbilityName.WordsAtNextStep) ? "next-step" : "after-turn";
    }

    private static JsonObject Mode(ISpawn spawn, AgentDoor door, string? mode, string root)
    {
        var request = new SpawnRequest("repository", root, "http://127.0.0.1:1") { Command = ["program"], Mode = mode };
        var recipe = spawn.Start(request);
        return new JsonObject
        {
            ["arguments"] = new JsonArray([.. recipe.Arguments.Select(argument => (JsonNode?)argument)]),
            ["variables"] = new JsonObject(recipe.Variables.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => KeyValuePair.Create<string, JsonNode?>(pair.Key, pair.Value))),
            ["wire"] = door.Transport == Transport.Protocol ? mode : null,
        };
    }

    /// <summary>Each place a declared grant and an observed one differ, by its path in the row.</summary>
    public static IReadOnlyList<string> Differences(JsonNode? declared, JsonNode? observed, string path = "")
    {
        var found = new List<string>();
        Compare(declared, observed, path, found);
        return found;
    }

    private static void Compare(JsonNode? declared, JsonNode? observed, string path, List<string> found)
    {
        switch (declared, observed)
        {
            case (JsonObject left, JsonObject right):
                foreach (var name in left.Select(pair => pair.Key).Union(right.Select(pair => pair.Key)))
                {
                    Compare(left[name], right[name], path.Length == 0 ? name : path + "." + name, found);
                }

                break;
            case (JsonArray left, JsonArray right):
                if (!left.Select(item => item?.ToJsonString()).SequenceEqual(right.Select(item => item?.ToJsonString())))
                {
                    found.Add($"{path}: declared {left.ToJsonString()}, the door hands {right.ToJsonString()}");
                }

                break;
            default:
                if (declared?.ToJsonString() != observed?.ToJsonString())
                {
                    found.Add($"{path}: declared {declared?.ToJsonString() ?? "nothing"}, the door hands {observed?.ToJsonString() ?? "nothing"}");
                }

                break;
        }
    }
}
