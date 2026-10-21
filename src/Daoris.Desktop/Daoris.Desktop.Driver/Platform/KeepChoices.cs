using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's keep rules (KEEP1d, D195 point 2; the cleanup design §2.2): <c>driver.json</c>'s <c>keep</c>, a standing choice in one
/// object, each key absent meaning its kind's default in <see cref="KeepKinds"/>. This row's kinds: the set-aside folders' days and
/// <c>whole</c>, the downloads' days or <c>never</c>, and the logs' days. The floor (KEEP1f) and history's age (KEEP1g) arrive with the
/// acts they govern; a key this build does not know stays where it stands (<see cref="JsonKeeping"/>).
/// </summary>
/// <remarks>
/// <para><b>Read as a stored shape</b>: a value out of its kind's bounds, or of a kind it is not, is not read, so its kind keeps its
/// default; <c>downloads.days</c> written <c>null</c> is <c>never</c>. Written only where set, in the table's order.</para>
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

    /// <summary>No choice made: every kind at its default.</summary>
    public static KeepChoices None { get; } = new();

    /// <summary>The kinds the person sets in this build, by id, in the table's order.</summary>
    public static IReadOnlyList<KeepKind> Settable { get; } = [KeepKinds.SetAside, KeepKinds.Downloads, KeepKinds.Logs];

    /// <summary>Whether <paramref name="kind"/>'s rule is its default: nothing of it chosen.</summary>
    public bool IsDefault(string kind) => kind switch
    {
        "set-aside" => SetAsideDays is null && !SetAsideWhole,
        "downloads" => DownloadsDays is null && !DownloadsNever,
        "logs" => LogsDays is null,
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

        writer.WriteEndObject();
    }

    private static JsonElement? Kind(JsonElement keep, string name) =>
        keep.TryGetProperty(name, out var kind) && kind.ValueKind == JsonValueKind.Object ? kind : null;

    /// <summary>A kind's days where they are a whole number within its bounds; else none, so the kind keeps its default.</summary>
    private static int? Days(JsonElement kind, KeepKind bounds) =>
        kind.TryGetProperty("days", out var days) && days.ValueKind == JsonValueKind.Number && days.TryGetInt32(out var count)
        && count >= bounds.MinDays && count <= bounds.MaxDays
            ? count
            : null;
}

/// <summary>
/// One kind's rule as the person sets it (KEEP1d): its days, <c>never</c>, <c>whole</c> or not, or back to its default. What
/// <c>SET_KEEP</c> carries and <c>daoris driver keep</c> reads from its words; <see cref="KeepRules.Apply"/> judges it.
/// </summary>
/// <param name="Kind">The kind's id: <c>set-aside</c>, <c>downloads</c> or <c>logs</c>.</param>
/// <param name="Days">Its days, where given.</param>
/// <param name="Never">The downloads' <c>never</c>.</param>
/// <param name="Whole">The set-aside folders kept whole (true) or slimmed (false), where given.</param>
/// <param name="Default">Its default again: every other field is then none.</param>
public sealed record KeepSet(string Kind, int? Days = null, bool Never = false, bool? Whole = null, bool Default = false);

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
        ?? throw new DriverException($"`{id}` is no kind whose rule you set here: name {string.Join(", ", KeepChoices.Settable.Select(kind => $"`{kind.Id}`"))}.");

    /// <summary>The rule <paramref name="set"/> names, judged against its kind's bounds and written into the choices; nothing else changes.</summary>
    /// <exception cref="ChoiceRefusal">A value out of the kind's bounds, or one the kind does not take (<c>never</c> but for the downloads,
    /// <c>whole</c> but for the set-aside folders): nothing is written.</exception>
    /// <exception cref="DriverException">No kind the person sets, or nothing to set.</exception>
    public static DriverConfig Apply(DriverConfig config, KeepSet set)
    {
        var kind = Kind(set.Kind);
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
            "set-aside" => policy.SetAside.Whole
                ? $"a tree's folder moved aside is kept {policy.SetAside.Days.TotalDays:0} days, whole"
                : $"a tree's folder moved aside is kept {policy.SetAside.Days.TotalDays:0} days; its package folders go an hour after the move",
            "downloads" => policy.Downloads.Days is { } days
                ? $"a downloaded version nothing uses goes {days.TotalDays:0} days into its unused stretch"
                : "a downloaded version nothing uses stays until you remove it",
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

    private static KeepChoices Cleared(KeepChoices keep, string kind) => kind switch
    {
        "set-aside" => keep with { SetAsideDays = null, SetAsideWhole = false },
        "downloads" => keep with { DownloadsDays = null, DownloadsNever = false },
        _ => keep with { LogsDays = null },
    };

    private static ChoiceRefusal OutOfRange(KeepKind kind, string value) =>
        new(ChoiceRefusal.KeepOutOfRange, $"`{kind.Id}` keeps {Choices(kind)}; {value} is out of its bounds, so nothing was written.",
            ("kind", kind.Id), ("value", value), ("min", kind.MinDays.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("max", kind.MaxDays.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
