using System.Text.Json;

namespace Daoris.Driver;

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

    /// <summary>The kinds the person sets in this build, by id, in the table's order.</summary>
    public static IReadOnlyList<KeepKind> Settable { get; } = [KeepKinds.SetAside, KeepKinds.Downloads, KeepKinds.Logs, KeepKinds.History];

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
                SetAsideDays = Days(setAside, KeepKinds.SetAside),
                SetAsideWhole = setAside.TryGetProperty("whole", out var whole) && whole.ValueKind == JsonValueKind.True,
            };
        }

        if (Kind(keep, "downloads") is { } downloads && downloads.TryGetProperty("days", out var days))
        {
            choices = days.ValueKind == JsonValueKind.Null
                ? choices with { DownloadsNever = true }
                : choices with { DownloadsDays = Days(downloads, KeepKinds.Downloads) };
        }

        if (Kind(keep, "logs") is { } logs) choices = choices with { LogsDays = Days(logs, KeepKinds.Logs) };
        // `history.days` written null is `never`, its default, so it reads as none.
        if (Kind(keep, "history") is { } history) choices = choices with { HistoryDays = Days(history, KeepKinds.History) };
        // KEEP1f: one of `off`, a size or a share; a value out of its bounds is not read, so the floor keeps its default.
        if (Kind(keep, "floor") is { } floor)
        {
            if (floor.TryGetProperty("off", out var off) && off.ValueKind == JsonValueKind.True) choices = choices with { FloorOff = true };
            else if (Whole(floor, "gb", FloorPolicy.MinGb, FloorPolicy.MaxGb) is { } gb) choices = choices with { FloorGb = gb };
            else if (Whole(floor, "percent", FloorPolicy.MinPercent, FloorPolicy.MaxPercent) is { } share) choices = choices with { FloorPercent = share };
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
    private static int? Days(JsonElement kind, KeepKind bounds) => Whole(kind, "days", bounds.MinDays, bounds.MaxDays);

    /// <summary>A whole number under <paramref name="key"/> within its bounds; else none.</summary>
    private static int? Whole(JsonElement kind, string key, int min, int max) =>
        kind.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count)
        && count >= min && count <= max
            ? count
            : null;
}

/// <summary>
/// One kind's rule as the person sets it (KEEP1d): its days, <c>never</c>, <c>whole</c> or not, or back to its default. What
/// <c>SET_KEEP</c> carries and <c>daoris driver keep</c> reads from its words; <see cref="KeepRules.Apply"/> judges it.
/// </summary>
/// <param name="Kind">The kind's id: <c>set-aside</c>, <c>downloads</c>, <c>logs</c> or <c>history</c>.</param>
/// <param name="Days">Its days, where given.</param>
/// <param name="Never">The downloads' or history's <c>never</c>.</param>
/// <param name="Whole">The set-aside folders kept whole (true) or slimmed (false), where given.</param>
/// <param name="Default">Its default again: every other field is then none.</param>
/// <param name="Gb">The floor's size in GB (KEEP1f, the kind <c>floor</c>).</param>
/// <param name="Percent">The floor's share of each volume, in percent (KEEP1f).</param>
/// <param name="Off">No floor (KEEP1f).</param>
public sealed record KeepSet(
    string Kind, int? Days = null, bool Never = false, bool? Whole = null, bool Default = false, int? Gb = null, int? Percent = null, bool Off = false)
{
    /// <summary>
    /// The size of the Daoris folder's volume in bytes, where the door read it (KEEP1f): a floor's size is judged against half of it. Null
    /// where it was not read; each volume's floor is then still at most half of that volume (<see cref="FloorPolicy.Of"/>).
    /// </summary>
    public long? Volume { get; init; }
}

/// <summary>
/// The person's keep rules set (KEEP1d, D195 point 2; the cleanup design §2.2): the one judgement the window's <c>SET_KEEP</c>, the
/// terminal's <c>daoris driver keep</c> and Ask Daoris's <c>keep</c> door make (D194 point 4), refusing a value out of its kind's bounds
/// with <see cref="ChoiceCodes.KeepOutOfRange"/>.
/// </summary>
public static class KeepRules
{
    /// <summary>The kind <paramref name="id"/> names, among those the person sets.</summary>
    /// <exception cref="DriverException">No kind the person sets has that id.</exception>
    public static KeepKind Kind(string? id) =>
        KeepChoices.Settable.FirstOrDefault(kind => string.Equals(kind.Id, id?.Trim(), StringComparison.Ordinal))
        ?? throw new DriverException($"`{id}` is no kind whose rule you set here: name {string.Join(", ", KeepChoices.Settable.Select(kind => $"`{kind.Id}`"))} "
            + $"or `{Floor}`.");

    /// <summary>The free-space floor's word (KEEP1f): set beside the kinds' rules, though it keeps no kind of its own.</summary>
    public const string Floor = "floor";

    /// <summary>Whether <paramref name="id"/> names the floor rather than a kind.</summary>
    public static bool IsFloor(string? id) => string.Equals(id?.Trim(), Floor, StringComparison.Ordinal);

    /// <summary>The rule <paramref name="set"/> names, judged against its kind's bounds and written into the choices; nothing else changes.</summary>
    /// <exception cref="ChoiceRefusal">A value out of the kind's bounds, or one the kind does not take (<c>never</c> but for the downloads,
    /// <c>whole</c> but for the set-aside folders): nothing is written.</exception>
    /// <exception cref="DriverException">No kind the person sets, or nothing to set.</exception>
    public static DriverConfig Apply(DriverConfig config, KeepSet set)
    {
        if (IsFloor(set.Kind)) return config with { Keep = ApplyFloor(config.Keep, set) };
        var kind = Kind(set.Kind);
        if (set.Gb is not null || set.Percent is not null || set.Off) throw OutOfRange(kind, "a size, a share or off, which only the floor takes");
        var keep = config.Keep;
        if (set.Default)
        {
            if (set.Days is not null || set.Never || set.Whole is not null) throw OutOfRange(kind, "default and a value at once");
            return config with { Keep = Cleared(keep, kind.Id) };
        }

        if (set.Days is null && !set.Never && set.Whole is null) throw new DriverException($"name `{kind.Id}`'s days, {Choices(kind)}.");
        if (set.Days is { } days && (days < kind.MinDays || days > kind.MaxDays)) throw OutOfRange(kind, $"{days} days");
        if (set.Never && !kind.MaySetNever) throw OutOfRange(kind, "never");
        if (set.Never && set.Days is not null) throw OutOfRange(kind, "never and a number of days at once");
        if (set.Whole is not null && !kind.MaySetWhole) throw OutOfRange(kind, set.Whole.Value ? "whole" : "slim");

        keep = kind.Id switch
        {
            "set-aside" => keep with { SetAsideDays = set.Days ?? keep.SetAsideDays, SetAsideWhole = set.Whole ?? keep.SetAsideWhole },
            "downloads" => set.Never
                ? keep with { DownloadsNever = true, DownloadsDays = null }
                : keep with { DownloadsNever = false, DownloadsDays = set.Days },
            // History's `never` is its default: no age, written as nothing (KEEP1g).
            "history" => keep with { HistoryDays = set.Never ? null : set.Days },
            _ => keep with { LogsDays = set.Days },
        };
        return config with { Keep = keep };
    }

    /// <summary>
    /// The rule a kind's word and its value name, as <c>daoris driver keep &lt;kind&gt; &lt;rule&gt;</c> and Ask Daoris's <c>keep</c> door
    /// read them: a whole number of days, <c>never</c>, <c>whole</c>, <c>slim</c> or <c>default</c>. Its bounds are
    /// <see cref="Apply"/>'s to judge.
    /// </summary>
    /// <exception cref="DriverException">No kind the person sets, or a value that is none of those.</exception>
    public static KeepSet Asked(string kindWord, string value)
    {
        if (IsFloor(kindWord)) return FloorAsked(value);
        var kind = Kind(kindWord);
        var word = value.Trim();
        return word switch
        {
            "default" => new KeepSet(kind.Id, Default: true),
            "never" => new KeepSet(kind.Id, Never: true),
            "whole" => new KeepSet(kind.Id, Whole: true),
            "slim" => new KeepSet(kind.Id, Whole: false),
            _ when int.TryParse(word, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var days)
                => new KeepSet(kind.Id, Days: days),
            _ => throw new DriverException($"`{word}` is no rule of `{kind.Id}`: it keeps {Choices(kind)}."),
        };
    }

    /// <summary>One kind's rule, as the person's <paramref name="keep"/> makes it, in a clause: the terminal's and Ask Daoris's card's words.</summary>
    public static string Said(string kind, KeepChoices keep)
    {
        var policy = KeepPolicy.From(keep);
        return kind switch
        {
            Floor => policy.Floor.Said,
            "set-aside" => policy.SetAside.Whole
                ? $"a tree's folder moved aside is kept {policy.SetAside.Days.TotalDays:0} days, whole"
                : $"a tree's folder moved aside is kept {policy.SetAside.Days.TotalDays:0} days; its package folders go an hour after the move",
            "downloads" => policy.Downloads.Days is { } days
                ? $"a downloaded version nothing uses goes {days.TotalDays:0} days into its unused stretch"
                : "a downloaded version nothing uses stays until you remove it",
            "history" => policy.History.Days is { } age
                ? $"finished work's records go {age.TotalDays:0} days after their last move, through the clear, each refusal holding"
                : "finished work's records stay until you clear them",
            _ => $"each day's file of the machine log is kept {policy.Logs.Days.TotalDays:0} days",
        };
    }

    /// <summary>What <paramref name="kind"/> takes, in the words its refusal and its usage say.</summary>
    public static string Choices(KeepKind kind)
    {
        List<string> choices = [$"{kind.MinDays} to {kind.MaxDays} days"];
        if (kind.MaySetWhole) choices.AddRange(["`whole`", "`slim`"]);
        if (kind.MaySetNever) choices.Add("`never`");
        return $"{string.Join(", ", choices)} or `default`";
    }

    /// <summary>What the floor takes, in the words its refusal and its usage say (KEEP1f).</summary>
    public static string FloorChoices(long? volume) =>
        $"a size from {FloorPolicy.MinGb} GB to {(volume is { } total ? $"{FloorPolicy.HalfGb(total)} GB, half its volume" : "half its volume")}, "
        + $"a share from {FloorPolicy.MinPercent}% to {FloorPolicy.MaxPercent}%, `off` or `default`";

    /// <summary>
    /// The floor a word names (KEEP1f): <c>&lt;n&gt;GB</c>, <c>&lt;n&gt;%</c>, <c>off</c> or <c>default</c>, as
    /// <c>daoris driver keep floor</c> and Ask Daoris's door read it; its bounds are <see cref="Apply"/>'s to judge.
    /// </summary>
    private static KeepSet FloorAsked(string value)
    {
        var word = value.Trim();
        var number = System.Globalization.NumberStyles.AllowLeadingSign;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        return word switch
        {
            "default" => new KeepSet(Floor, Default: true),
            "off" => new KeepSet(Floor, Off: true),
            _ when word.EndsWith("GB", StringComparison.OrdinalIgnoreCase) && int.TryParse(word[..^2].TrimEnd(), number, invariant, out var gb)
                => new KeepSet(Floor, Gb: gb),
            _ when word.EndsWith('%') && int.TryParse(word[..^1].TrimEnd(), number, invariant, out var share)
                => new KeepSet(Floor, Percent: share),
            _ => throw new DriverException($"`{word}` is no floor: it takes {FloorChoices(null)}, e.g. `20GB` or `10%`."),
        };
    }

    /// <summary>The floor set (KEEP1f), judged against its bounds: a size from 1 GB to half the Daoris folder's volume, a share from 1% to 50%.</summary>
    private static KeepChoices ApplyFloor(KeepChoices keep, KeepSet set)
    {
        var given = (set.Gb is not null ? 1 : 0) + (set.Percent is not null ? 1 : 0) + (set.Off ? 1 : 0);
        if (set.Days is not null || set.Never || set.Whole is not null) throw FloorOutOfRange(set, "days, never or whole, which the floor does not take");
        if (set.Default)
        {
            if (given > 0) throw FloorOutOfRange(set, "default and a value at once");
            return keep with { FloorGb = null, FloorPercent = null, FloorOff = false };
        }

        if (given == 0) throw new DriverException($"name the floor: {FloorChoices(set.Volume)}.");
        if (given > 1) throw FloorOutOfRange(set, "more than one of a size, a share and off at once");
        if (set.Off) return keep with { FloorGb = null, FloorPercent = null, FloorOff = true };
        if (set.Gb is { } gb)
        {
            var most = set.Volume is { } total ? Math.Min(FloorPolicy.HalfGb(total), FloorPolicy.MaxGb) : FloorPolicy.MaxGb;
            if (gb < FloorPolicy.MinGb || gb > most) throw FloorOutOfRange(set, $"{gb} GB");
            return keep with { FloorGb = gb, FloorPercent = null, FloorOff = false };
        }

        var share = set.Percent!.Value;
        if (share < FloorPolicy.MinPercent || share > FloorPolicy.MaxPercent) throw FloorOutOfRange(set, $"{share}%");
        return keep with { FloorGb = null, FloorPercent = share, FloorOff = false };
    }

    private static ChoiceRefusal FloorOutOfRange(KeepSet set, string value)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var (min, max, unit) = set.Percent is not null
            ? (FloorPolicy.MinPercent, FloorPolicy.MaxPercent, "percent")
            : (FloorPolicy.MinGb, set.Volume is { } total ? Math.Min(FloorPolicy.HalfGb(total), FloorPolicy.MaxGb) : FloorPolicy.MaxGb, "gb");
        return new(ChoiceRefusal.KeepOutOfRange, $"the floor takes {FloorChoices(set.Volume)}; {value} is out of its bounds, so nothing was written.",
            ("kind", Floor), ("value", value), ("min", min.ToString(invariant)), ("max", max.ToString(invariant)), ("unit", unit));
    }

    private static KeepChoices Cleared(KeepChoices keep, string kind) => kind switch
    {
        "set-aside" => keep with { SetAsideDays = null, SetAsideWhole = false },
        "downloads" => keep with { DownloadsDays = null, DownloadsNever = false },
        "history" => keep with { HistoryDays = null },
        _ => keep with { LogsDays = null },
    };

    private static ChoiceRefusal OutOfRange(KeepKind kind, string value) =>
        new(ChoiceRefusal.KeepOutOfRange, $"`{kind.Id}` keeps {Choices(kind)}; {value} is out of its bounds, so nothing was written.",
            ("kind", kind.Id), ("value", value), ("min", kind.MinDays.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("max", kind.MaxDays.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
