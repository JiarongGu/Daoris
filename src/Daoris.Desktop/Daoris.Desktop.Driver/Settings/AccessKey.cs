using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One pause a repository's sessions may stop for (AUTONOMY1a, D184 point 3): its name as the driver's file keeps it, its values in
/// the order a person is offered them, the value that is today's behaviour (<see cref="Default"/>) and the one full access takes
/// (<see cref="Open"/>).
/// </summary>
public sealed record AccessPause(string Name, IReadOnlyList<string> Values, string Default, string Open);

/// <summary>
/// One level's access setup as the driver's file keeps it (AUTONOMY1a, D184 points 1-3): full access, or the pauses it names, each
/// with its value. A pause it does not name is the next level's. Never both: a pause set over full access writes every pause.
/// </summary>
public sealed record AccessSetting
{
    /// <summary>Every pause off (D184 point 2): what the file's <c>"full"</c> says.</summary>
    public static AccessSetting FullAccess { get; } = new() { Full = true };

    /// <summary>Whether this level is full access: every pause at its <see cref="AccessPause.Open"/> value.</summary>
    public bool Full { get; init; }

    /// <summary>The pauses this level names, by <see cref="AccessPause.Name"/>, each with a value of its own; none where <see cref="Full"/>.</summary>
    public IReadOnlyDictionary<string, string> Pauses { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// The access setup as <c>driver.json</c> keeps it, <c>access</c>, <c>workspaceAccess</c> and <c>machineAccess</c> (AUTONOMY1a, D184
/// points 1-3; FOUNDATION1 mod-iii): the pauses and their values, and the key's reading and writing by the settings' own parser. How a
/// level resolves, is said and is edited stays with the rule (<see cref="AccessRules"/>), which answers through this for what it shares.
/// </summary>
/// <remarks>
/// <b>Read as a stored shape</b>: a pause this build does not know is passed over, and a value it does not know reads as the pause's
/// default at that level, never as the next level's, which may be looser. Both stay in the file as written, since an edit changes
/// only the key paths it changed. A level of another shape is not read.
/// </remarks>
public static class AccessKey
{
    /// <summary>The file's word for full access at a level, and a door's.</summary>
    public const string FullWord = "full";

    public const string Permissions = "permissions";
    public const string Routing = "routing";
    public const string Landing = "landing";
    public const string GoAheads = "goAheads";
    public const string Questions = "questions";

    /// <summary>Permission prompts: the harness's asking mode.</summary>
    public const string Asking = "asking";

    /// <summary>Permission prompts: the harness's auto mode, today's (D81).</summary>
    public const string Auto = "auto";

    /// <summary>Permission prompts: the harness's unrestricted mode (D183's full postures).</summary>
    public const string Unrestricted = "unrestricted";

    /// <summary>Routing and landing: the person's press, today's.</summary>
    public const string Person = "person";

    /// <summary>Routing: the ask goes where the intake chose.</summary>
    public const string Intake = "intake";

    /// <summary>Landing: the repository's landing rule lands it.</summary>
    public const string Rule = "rule";

    /// <summary>Go-aheads: asked before the acts the repository names, today's.</summary>
    public const string Named = "named";

    /// <summary>Go-aheads: never asked, save what the ask itself demands (D184 point 5).</summary>
    public const string Never = "never";

    /// <summary>Questions: as Claude Code's console asks them (D184 point 4), today's behaviour until AUTONOMY1e.</summary>
    public const string ClaudeCode = "claude-code";

    /// <summary>The pauses, in the order every door and the record say them.</summary>
    public static IReadOnlyList<AccessPause> Pauses { get; } =
    [
        new(Permissions, [Asking, Auto, Unrestricted], Auto, Unrestricted),
        new(Routing, [Person, Intake], Person, Intake),
        new(Landing, [Person, Rule], Person, Rule),
        new(GoAheads, [Named, Never], Named, Never),
        new(Questions, [ClaudeCode], ClaudeCode, ClaudeCode),
    ];

    /// <summary>The pause a spelling names, trimmed and in any case, or null for none.</summary>
    public static AccessPause? PauseNamed(string? spelled) =>
        Pauses.FirstOrDefault(pause => string.Equals(pause.Name, spelled?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The value of <paramref name="pause"/> a spelling names, trimmed and in any case, or null for none.</summary>
    public static string? ValueNamed(AccessPause pause, string? spelled) =>
        pause.Values.FirstOrDefault(value => string.Equals(value, spelled?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A level as the file keeps it, read as a stored shape: <c>"full"</c> in any case, or an object of pauses. A pause this build does
    /// not know is passed over; a value it does not know, or one that is no word, reads as the pause's default; a null value is
    /// unset. Null where the level is of another shape.
    /// </summary>
    internal static AccessSetting? Kept(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return string.Equals(value.GetString()?.Trim(), FullWord, StringComparison.OrdinalIgnoreCase) ? AccessSetting.FullAccess : null;
        }

        if (value.ValueKind != JsonValueKind.Object) return null;
        var pauses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (PauseNamed(property.Name) is not { } pause || pauses.ContainsKey(pause.Name) || property.Value.ValueKind == JsonValueKind.Null) continue;
            pauses[pause.Name] = property.Value.ValueKind == JsonValueKind.String && ValueNamed(pause, property.Value.GetString()) is { } named
                ? named
                : pause.Default;
        }

        return new AccessSetting { Pauses = pauses };
    }

    /// <summary>A map of levels as <c>driver.json</c> holds it: each name trimmed, a blank one and a level not read passed over, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, AccessSetting> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || Kept(property.Value) is not { } setting || map.ContainsKey(named)) continue;
            map[named] = setting;
        }

        return map;
    }

    /// <summary>This machine's level as <c>driver.json</c> holds it, <c>machineAccess</c>: null where it is absent or not read.</summary>
    internal static AccessSetting? Machine(JsonElement root) =>
        root.TryGetProperty("machineAccess", out var element) ? Kept(element) : null;

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, AccessSetting> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, setting) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            Write(writer, setting);
        }

        writer.WriteEndObject();
    }

    /// <summary>This machine's level written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMachine(Utf8JsonWriter writer, AccessSetting? setting)
    {
        if (setting is null) return;
        writer.WritePropertyName("machineAccess");
        Write(writer, setting);
    }

    /// <summary>One level as kept: <c>"full"</c>, or its pauses in <see cref="Pauses"/>' order.</summary>
    public static void Write(Utf8JsonWriter writer, AccessSetting setting)
    {
        if (setting.Full)
        {
            writer.WriteStringValue(FullWord);
            return;
        }

        writer.WriteStartObject();
        foreach (var pause in Pauses)
        {
            if (setting.Pauses.TryGetValue(pause.Name, out var value)) writer.WriteString(pause.Name, value);
        }

        writer.WriteEndObject();
    }
}
