using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The bounds <c>driver.json</c>'s <c>keep</c> is read within (KEEP1d, KEEP1f, KEEP1g; FOUNDATION1 mod-iii): the days each kind's rule may
/// hold and the floor's size and share, which a value out of is not read. The kinds' table (<see cref="KeepKinds"/>) and the floor's
/// policy (<see cref="FloorPolicy"/>) answer through them, so a bound is said once.
/// </summary>
public static class KeepKey
{
    /// <summary>The fewest days the set-aside folders' rule may hold.</summary>
    public const int SetAsideMinDays = 1;

    /// <summary>The most days the set-aside folders' rule may hold.</summary>
    public const int SetAsideMaxDays = 90;

    /// <summary>The fewest days the downloads' rule may hold.</summary>
    public const int DownloadsMinDays = 0;

    /// <summary>The most days the downloads' rule may hold.</summary>
    public const int DownloadsMaxDays = 365;

    /// <summary>The fewest days of the machine log's files kept.</summary>
    public const int LogsMinDays = 7;

    /// <summary>The most days of the machine log's files kept: each process's own thirty stays the ceiling.</summary>
    public const int LogsMaxDays = MachineLog.KeptDays;

    /// <summary>The youngest age finished history may be cleared at.</summary>
    public const int HistoryMinDays = 30;

    /// <summary>The oldest age finished history may be cleared at.</summary>
    public const int HistoryMaxDays = 3650;

    /// <summary>The fewest GB the person may set the free-space floor to.</summary>
    public const int FloorMinGb = 1;

    /// <summary>The most GB a stored floor may hold, whatever the volume; a door judges against half the volume it reads.</summary>
    public const int FloorMaxGb = 1 << 20;

    /// <summary>The smallest share the person may set the floor to.</summary>
    public const int FloorMinPercent = 1;

    /// <summary>The largest share the person may set the floor to: half the volume.</summary>
    public const int FloorMaxPercent = 50;
}

/// <summary>
/// The person's keep rules (KEEP1d, D195 point 2; the cleanup design §2.2): <c>driver.json</c>'s <c>keep</c>, a standing choice in one
/// object, each key absent meaning its kind's default in <see cref="KeepKinds"/>. This row's kinds: the set-aside folders' days and
/// <c>whole</c>, the downloads' days or <c>never</c>, the logs' days, history's age (KEEP1g), none by default, and the free-space floor
/// (KEEP1f): a size in GB, a share of the volume, or <c>off</c>. A key this build does not know stays where it stands
/// (<see cref="JsonKeeping"/>).
/// </summary>
/// <remarks>
/// <para><b>Read as a stored shape</b>: a value out of its kind's bounds, or of a kind it is not, is not read, so its kind keeps its
/// default; <c>downloads.days</c> written <c>null</c> is <c>never</c>. Written only where set, in the table's order, the floor last.</para>
/// </remarks>
public sealed record KeepChoices
{
    /// <summary>The file's key.</summary>
    public const string Key = "keep";

    /// <summary>The set-aside folders' days, or null for the default.</summary>
    public int? SetAsideDays { get; init; }

    /// <summary>Whether every set-aside folder is kept whole, its package folders included (no slim).</summary>
    public bool SetAsideWhole { get; init; }

    /// <summary>The downloads' days into a version's unused stretch, or null for the default (or <see cref="DownloadsNever"/>).</summary>
    public int? DownloadsDays { get; init; }

    /// <summary>The downloads' <c>never</c>: none goes by its days, the press alone.</summary>
    public bool DownloadsNever { get; init; }

    /// <summary>The machine log's days, or null for the default thirty.</summary>
    public int? LogsDays { get; init; }

    /// <summary>Finished history's age in days (KEEP1g), or null for the default: <c>never</c>, the person's clear alone.</summary>
    public int? HistoryDays { get; init; }

    /// <summary>The floor as a size, in GB (KEEP1f), or null; at most half of each volume it is read on (<see cref="FloorPolicy.Of"/>).</summary>
    public int? FloorGb { get; init; }

    /// <summary>The floor as a share of each volume, in percent (KEEP1f), or null.</summary>
    public int? FloorPercent { get; init; }

    /// <summary>No floor (KEEP1f): Daoris frees nothing early, and says nothing of the free space in <i>What needs you</i>.</summary>
    public bool FloorOff { get; init; }

    /// <summary>No choice made: every kind at its default.</summary>
    public static KeepChoices None { get; } = new();

    /// <summary>Whether <paramref name="kind"/>'s rule is its default: nothing of it chosen.</summary>
    public bool IsDefault(string kind) => kind switch
    {
        "set-aside" => SetAsideDays is null && !SetAsideWhole,
        "downloads" => DownloadsDays is null && !DownloadsNever,
        "logs" => LogsDays is null,
        "history" => HistoryDays is null,
        "floor" => FloorGb is null && FloorPercent is null && !FloorOff,
        _ => true,
    };

    /// <summary>The choices as <c>driver.json</c> holds them; none where the key is absent or no object.</summary>
    internal static KeepChoices Read(JsonElement root)
    {
        if (!root.TryGetProperty(Key, out var keep) || keep.ValueKind != JsonValueKind.Object) return None;
        var choices = None;
        if (Kind(keep, "setAside") is { } setAside)
        {
            choices = choices with
            {
                SetAsideDays = Days(setAside, KeepKey.SetAsideMinDays, KeepKey.SetAsideMaxDays),
                SetAsideWhole = setAside.TryGetProperty("whole", out var whole) && whole.ValueKind == JsonValueKind.True,
            };
        }

        if (Kind(keep, "downloads") is { } downloads && downloads.TryGetProperty("days", out var days))
        {
            choices = days.ValueKind == JsonValueKind.Null
                ? choices with { DownloadsNever = true }
                : choices with { DownloadsDays = Days(downloads, KeepKey.DownloadsMinDays, KeepKey.DownloadsMaxDays) };
        }

        if (Kind(keep, "logs") is { } logs) choices = choices with { LogsDays = Days(logs, KeepKey.LogsMinDays, KeepKey.LogsMaxDays) };
        // `history.days` written null is `never`, its default, so it reads as none.
        if (Kind(keep, "history") is { } history) choices = choices with { HistoryDays = Days(history, KeepKey.HistoryMinDays, KeepKey.HistoryMaxDays) };
        // KEEP1f: one of `off`, a size or a share; a value out of its bounds is not read, so the floor keeps its default.
        if (Kind(keep, "floor") is { } floor)
        {
            if (floor.TryGetProperty("off", out var off) && off.ValueKind == JsonValueKind.True) choices = choices with { FloorOff = true };
            else if (Whole(floor, "gb", KeepKey.FloorMinGb, KeepKey.FloorMaxGb) is { } gb) choices = choices with { FloorGb = gb };
            else if (Whole(floor, "percent", KeepKey.FloorMinPercent, KeepKey.FloorMaxPercent) is { } share) choices = choices with { FloorPercent = share };
        }

        return choices;
    }

    /// <summary>The choices written as <c>driver.json</c> keeps them: only what is set, the kinds in the table's order.</summary>
    internal void Write(Utf8JsonWriter writer)
    {
        if (this == None) return;
        writer.WriteStartObject(Key);
        if (!IsDefault("set-aside"))
        {
            writer.WriteStartObject("setAside");
            if (SetAsideDays is { } setAside) writer.WriteNumber("days", setAside);
            if (SetAsideWhole) writer.WriteBoolean("whole", true);
            writer.WriteEndObject();
        }

        if (!IsDefault("downloads"))
        {
            writer.WriteStartObject("downloads");
            if (DownloadsNever) writer.WriteNull("days");
            else writer.WriteNumber("days", DownloadsDays!.Value);
            writer.WriteEndObject();
        }

        if (LogsDays is { } logs)
        {
            writer.WriteStartObject("logs");
            writer.WriteNumber("days", logs);
            writer.WriteEndObject();
        }

        if (HistoryDays is { } history)
        {
            writer.WriteStartObject("history");
            writer.WriteNumber("days", history);
            writer.WriteEndObject();
        }

        if (!IsDefault("floor"))
        {
            writer.WriteStartObject("floor");
            if (FloorOff) writer.WriteBoolean("off", true);
            else if (FloorGb is { } gb) writer.WriteNumber("gb", gb);
            else writer.WriteNumber("percent", FloorPercent!.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static JsonElement? Kind(JsonElement keep, string name) =>
        keep.TryGetProperty(name, out var kind) && kind.ValueKind == JsonValueKind.Object ? kind : null;

    /// <summary>A kind's days where they are a whole number within its bounds; else none, so the kind keeps its default.</summary>
    private static int? Days(JsonElement kind, int min, int max) => Whole(kind, "days", min, max);

    /// <summary>A whole number under <paramref name="key"/> within its bounds; else none.</summary>
    private static int? Whole(JsonElement kind, string key, int min, int max) =>
        kind.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count)
        && count >= min && count <= max
            ? count
            : null;
}
