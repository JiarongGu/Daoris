using System.Text.Json;

namespace Daoris.Driver;

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
    /// <summary>The kinds the person sets in this build, by id, in the table's order.</summary>
    public static IReadOnlyList<KeepKind> Settable { get; } = [KeepKinds.SetAside, KeepKinds.Downloads, KeepKinds.Logs, KeepKinds.History];

    /// <summary>One kind's keep rule set, or its default again, through <see cref="Apply"/>.</summary>
    /// <exception cref="DriverException">The rule's refusal (<see cref="ChoiceRefusal"/> out of its bounds), nothing changed.</exception>
    public static DriverConfig WithKeep(this DriverConfig config, KeepSet set) => Apply(config, set);

    /// <summary>The kind <paramref name="id"/> names, among those the person sets.</summary>
    /// <exception cref="DriverException">No kind the person sets has that id.</exception>
    public static KeepKind Kind(string? id) =>
        Settable.FirstOrDefault(kind => string.Equals(kind.Id, id?.Trim(), StringComparison.Ordinal))
        ?? throw new DriverException($"`{id}` is no kind whose rule you set here: name {string.Join(", ", Settable.Select(kind => $"`{kind.Id}`"))} "
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
            ("kind", Floor), ("value", value), ("min", min.ToString(invariant)), ("max", max.ToString(invariant)), ("unit", unit),
            // The page words a floor's bounds by its unit, not as days.
            ("context", unit));
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
