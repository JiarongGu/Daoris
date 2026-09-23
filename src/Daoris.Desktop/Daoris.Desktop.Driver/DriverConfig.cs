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
/// <param name="Trees">
/// Repositories whose sessions open their OWN worktree instead of the registered root (D51). Empty —
/// the default — is today's behaviour byte for byte: an additive feature, like the profiles (D49 §4).
/// </param>
/// <param name="Notify">
/// Whether this machine interrupts the person when a session parks or ends unasked (SURF5b).
/// <b>On by default</b>: the whole point of a driver is that nobody has to watch it, and a driver
/// that ran silently by default would be one whose parked sessions sit until somebody thinks to look.
/// Off in one click, and machine-local like every other choice in this file.
/// </param>
public sealed record DriverConfig(
    IReadOnlyList<string> Drivable,
    IReadOnlyList<string> Holds,
    int Cap,
    string Adapter,
    int TimeoutMinutes,
    int PollSeconds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Commands,
    IReadOnlyList<string> Trees,
    bool Notify = true,
    int Strikes = 3,
    IReadOnlyDictionary<string, int>? Forgiven = null)
{
    /// <summary>
    /// How many failed sessions park a quest (DRV6). <c>0</c> never parks — today's behaviour, for a
    /// person who wants the loop to keep trying.
    /// </summary>
    public int Strikes { get; init; } = Math.Max(0, Strikes);

    /// <summary>
    /// Quests the person has restarted, and the failure count they were restarted at. A mark rather
    /// than an erasure: the records still say what happened, and the next three failures park it
    /// again. Machine-local, like every other choice in this file.
    /// </summary>
    public IReadOnlyDictionary<string, int> Forgiven { get; init; } =
        Forgiven ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Drives nothing, holds nothing — the safe shape silence takes.</summary>
    public static DriverConfig Empty { get; } = new(
        Drivable: [], Holds: [], Cap: 2, Adapter: "claude-code",
        TimeoutMinutes: 30, PollSeconds: 15,
        Commands: new Dictionary<string, IReadOnlyList<string>>(),
        Trees: []);

    /// <summary>The failure count this quest was last restarted at — zero when it never was.</summary>
    public int ForgivenAt(string questId) =>
        Forgiven.TryGetValue(questId, out var mark) ? mark : 0;

    /// <summary>Let this quest run again, counting from where it stands now.</summary>
    public DriverConfig WithForgiven(string questId, int at)
    {
        var next = new Dictionary<string, int>(Forgiven, StringComparer.OrdinalIgnoreCase)
        {
            [questId] = Math.Max(0, at),
        };
        return this with { Forgiven = next };
    }

    /// <summary>How many failures park a quest on this machine.</summary>
    public DriverConfig WithStrikes(int strikes) => this with { Strikes = Math.Max(0, strikes) };

    /// <summary>
    /// The harness an ask's INTAKE session runs on (D65 §1b) — or null, and no intake runs.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Absent means OFF</b>, the reading `notify` does not take and for the opposite reason.
    /// An intake spends a real login on every ask a person makes; a machine whose file predates this
    /// field has been answering asks by declarations only (INT4a), and silently starting to spend
    /// accounts on them after an upgrade is not a default anyone chose.</para>
    ///
    /// <para><b>Named, never "the same as the adapter"</b>: which harness answers asks and which does
    /// the work are two choices, and the second changing must not quietly move the first.</para>
    /// </remarks>
    public string? IntakeAdapter { get; init; }

    /// <summary>Which harness answers asks here, or null for none — the declarations tier alone.</summary>
    public DriverConfig WithIntake(string? adapter) =>
        this with { IntakeAdapter = string.IsNullOrWhiteSpace(adapter) ? null : adapter.Trim() };

    public const string PathVariable = "DAORIS_DRIVER_CONFIG";

    /// <summary>
    /// The conventional home, beside the store the driver watches — under the Daoris home (D63). A
    /// machine with no home and no override is refused, naming what to set, rather than written to.
    /// </summary>
    public static string DefaultPath => DaorisHome.Require("driver.json");

    /// <summary>The file every door reads and writes — the override, or the conventional home.</summary>
    public static string ResolvePath() =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath;

    /// <summary>A missing file is a machine that has opted nothing in — the empty config, not an error.</summary>
    public static DriverConfig Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;

    /// <summary>
    /// Write the choices back — atomically, beside-then-rename, like every write in this family: the
    /// watch loop re-reads this file every tick, and a torn read must never be what it finds.
    /// </summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var beside = path + ".writing";
        File.WriteAllText(beside, ToJson());
        File.Move(beside, path, overwrite: true);
    }

    /// <summary>The file's shape, written by hand for the same AOT reason it is read by hand.</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("drivable");
            foreach (var name in Drivable) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteStartArray("holds");
            foreach (var name in Holds) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteStartArray("trees");
            foreach (var name in Trees) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteNumber("cap", Cap);
            writer.WriteString("adapter", Adapter);
            // Written only when named: absent IS off, and the CLI twin writes it the same way.
            if (IntakeAdapter is not null) writer.WriteString("intakeAdapter", IntakeAdapter);
            writer.WriteBoolean("notify", Notify);
            writer.WriteNumber("timeoutMinutes", TimeoutMinutes);
            writer.WriteNumber("pollSeconds", PollSeconds);
            writer.WriteNumber("strikes", Strikes);
            writer.WriteStartObject("forgiven");
            foreach (var (quest, mark) in Forgiven.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                writer.WriteNumber(quest, mark);
            }

            writer.WriteEndObject();
            writer.WriteStartObject("commands");
            foreach (var (name, command) in Commands.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                writer.WriteStartArray(name);
                foreach (var part in command) writer.WriteStringValue(part);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>This machine's answer to "may I drive that repository, now" — one flag flipped at a time.</summary>
    public DriverConfig WithDrivable(string repository, bool drivable) => this with
    {
        Drivable = Toggle(Drivable, repository, drivable),
    };

    public DriverConfig WithHold(string repository, bool held) => this with
    {
        Holds = Toggle(Holds, repository, held),
    };

    /// <summary>Whether this repository's sessions open their own worktree (D51).</summary>
    public DriverConfig WithTrees(string repository, bool ownTree) => this with
    {
        Trees = Toggle(Trees, repository, ownTree),
    };

    /// <summary>The read side of <see cref="WithTrees"/> — one comparison rule for both.</summary>
    public bool OpensOwnTree(string repository) =>
        Trees.Contains(repository, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Toggle(IReadOnlyList<string> names, string repository, bool present)
    {
        var kept = names.Where(n => !string.Equals(n, repository, StringComparison.OrdinalIgnoreCase));
        return present ? [.. kept, repository] : [.. kept];
    }

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
            Commands: CommandMap(root),
            Trees: Strings(root, "trees"),
            // Absent means ON (SURF5b): every machine that already has a driver.json predates this
            // field, and reading silence as "off" would ship the feature switched off everywhere it
            // matters most — a machine that has been driving for a while.
            Notify: Bool(root, "notify") ?? Empty.Notify,
            // 🔴 Absent means the DEFAULT, not off — the opposite reading from `notify` and for the
            // opposite reason. A machine that predates this field is exactly the one that has been
            // driving unattended longest, and reading silence as "never park" would leave it doing
            // the thing this field exists to stop.
            Strikes: Int(root, "strikes") ?? Empty.Strikes,
            Forgiven: ForgivenMap(root))
        {
            // 🔴 Absent means OFF — see IntakeAdapter for why this is the opposite of `notify`.
            IntakeAdapter = String(root, "intakeAdapter")?.Trim() is { Length: > 0 } intake ? intake : null,
        };
    }

    private static IReadOnlyDictionary<string, int> ForgivenMap(JsonElement root)
    {
        var forgiven = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("forgiven", out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var mark) && mark > 0)
                {
                    forgiven[property.Name] = mark;
                }
            }
        }

        return forgiven;
    }

    /// <summary>Whether this machine says so when a session parks or ends unasked (SURF5b).</summary>
    public DriverConfig WithNotify(bool notify) => this with { Notify = notify };

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

    private static bool? Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
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
