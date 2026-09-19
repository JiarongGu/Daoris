using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's standing choices, machine-local by nature (D46 §6/§7): which repositories THIS machine
/// may drive, what is held, how wide the loop runs, and which adapter spawns.
/// </summary>
/// <param name="Drivable">Repositories the person opted in. Empty means the driver drives nothing.</param>
/// <param name="Holds">Repositories the person paused — drivable, but not now.</param>
/// <param name="Cap">Concurrent sessions across all repositories.</param>
/// <param name="Adapter">Which adapter spawns sessions. The supported harness by default (D23).</param>
/// <param name="TimeoutMinutes">How long a session may run before the driver concludes it failed.</param>
/// <param name="PollSeconds">How often the watch loop ticks.</param>
/// <param name="Commands">Per-adapter command configuration — what the stub runs.</param>
public sealed record DriverConfig(
    IReadOnlyList<string> Drivable,
    IReadOnlyList<string> Holds,
    int Cap,
    string Adapter,
    int TimeoutMinutes,
    int PollSeconds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Commands)
{
    /// <summary>Drives nothing, holds nothing — the safe shape silence takes.</summary>
    public static DriverConfig Empty { get; } = new(
        Drivable: [], Holds: [], Cap: 2, Adapter: "claude-code",
        TimeoutMinutes: 30, PollSeconds: 15,
        Commands: new Dictionary<string, IReadOnlyList<string>>());

    public const string PathVariable = "DAORIS_DRIVER_CONFIG";

    /// <summary>The conventional home, beside the store the driver watches.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "driver.json");

    /// <summary>A missing file is a machine that has opted nothing in — the empty config, not an error.</summary>
    public static DriverConfig Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;

    // Read by hand for the same reason the registration store writes by hand: nothing here may
    // quietly stop working under AOT, and the shape is small enough to be explicit about.
    public static DriverConfig Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new DriverConfig(
            Drivable: Strings(root, "drivable"),
            Holds: Strings(root, "holds"),
            Cap: Math.Max(1, Int(root, "cap") ?? Empty.Cap),
            Adapter: String(root, "adapter") ?? Empty.Adapter,
            TimeoutMinutes: Math.Max(1, Int(root, "timeoutMinutes") ?? Empty.TimeoutMinutes),
            PollSeconds: Math.Max(1, Int(root, "pollSeconds") ?? Empty.PollSeconds),
            Commands: CommandMap(root));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CommandMap(JsonElement root)
    {
        var commands = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("commands", out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var items = new List<string>();
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text)
                        {
                            items.Add(text);
                        }
                    }
                }

                commands[property.Name] = items;
            }
        }

        return commands;
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() is { Length: > 0 } text ? text : null
            : null;

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }
}
