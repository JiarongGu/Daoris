using System.Text.Json;

namespace Daoris.Driver;

/// <summary>How one scope's list is used, each setting's value as a start reads it (D130 §16.6; D165 point 2).</summary>
/// <param name="Use"><i>Use accounts</i>: one of <see cref="Modes"/> — <c>goal</c>, §16.3's walk (<see cref="AccountRotation.Order"/>), or
/// <c>order</c>, D125's walk.</param>
/// <param name="Keep">The account kept for conversations, or none.</param>
/// <param name="Switch">When to switch: one of <see cref="Switches"/> — <c>smart</c>, Daoris's curve; <c>curve</c>, the
/// person's own; <c>off</c>, no switch while a session runs.</param>
/// <param name="Curve">The person's curve, <see cref="UseCurve.None"/> for none; kept whatever <paramref name="Switch"/>
/// says, and read only under <c>curve</c>.</param>
public sealed record RotationUse(string Use, string? Keep, string Switch, UseCurve Curve)
{
    /// <summary>
    /// The settings as the old names spelled them (D130 §16.6), read on as a file's are (D165 §6): <c>early</c> false is
    /// <c>off</c>, and <c>near</c> other than 90 the session window flat at it, under <c>curve</c> unless off.
    /// </summary>
    public RotationUse(string use, string? keep, bool early, int near)
        : this(
            use,
            keep,
            !early ? "off" : near != SmartSession ? "curve" : "smart",
            near != SmartSession ? UseCurve.Flat("session", near) : UseCurve.None)
    {
    }

    /// <summary>Today's defaults (D130 §16.6, D165 point 2), and the one place they live: what absence means for each setting.</summary>
    public static readonly RotationUse Default = new("goal", Keep: null, Switch: "smart", Curve: UseCurve.None);

    /// <summary><i>Use accounts</i>' choices (§16.6): make the most of them, one by one in order. A new choice is a row here.</summary>
    public static readonly IReadOnlyList<string> Modes = ["goal", "order"];

    /// <summary>When to switch (D165 point 2): Daoris's curve, the person's own, or no switch while a session runs.</summary>
    public static readonly IReadOnlyList<string> Switches = ["smart", "curve", "off"];

    /// <summary>
    /// The smart curve's five-hour window, flat (D165 point 2; D130 §6's 90): what <i>near</i> meant where none was set, and
    /// the old name's value that reads as no curve. The CLI's <c>SMART_SESSION</c>.
    /// </summary>
    public const int SmartSession = 90;

    /// <summary><i>Near</i>'s range (§14), which the old name still takes at a door: a whole percent from the one to the other.</summary>
    public const int NearLowest = 50;

    /// <inheritdoc cref="NearLowest"/>
    public const int NearHighest = 99;

    /// <summary>The settings this build knows, in the order they are written.</summary>
    public static readonly IReadOnlyList<string> Fields = ["use", "keep", "switch", "curve"];

    /// <summary>The old names (D130 §16.6), which D165 §6 replaced: read on, and never written.</summary>
    public static readonly IReadOnlyList<string> OldNames = ["early", "near"];

    /// <summary>
    /// The settings §16.6 retired, which nothing released wrote: read as nothing and said as nothing, and gone at the next write, which
    /// is measured from the entry as read (KEEPALL1b).
    /// </summary>
    public static readonly IReadOnlyList<string> Retired = ["prefer", "parallel"];

    /// <summary>
    /// <i>Switch before the limit</i> in the old name's terms (D165 §6): the switch is not <c>off</c>. The walk reads the curve
    /// since SWITCH1b-ii; this stays for the doors that still speak the old names (the modules' answer, Ask Daoris).
    /// </summary>
    public bool Early => Switch != "off";

    /// <summary>
    /// <i>Near</i> in the old name's terms (D165 §6): the session window's one point under <c>curve</c>, else the smart curve's
    /// 90. The walk reads the curve since SWITCH1b-ii; this stays for the doors that still speak the old names.
    /// </summary>
    public int Near => Switch == "curve" && Curve["session"] is [var only] ? only.Used : SmartSession;

    /// <summary>
    /// An entry's settings, its old names read on: today's default where it says nothing or holds what this build does not
    /// know, and the names of what it does not know — the known settings first, in their order, then an old name whose
    /// value does not read, then the ones it has no name for.
    /// </summary>
    public static (RotationUse Use, IReadOnlyList<string> Unknown) Read(UseEntry? entry)
    {
        var use = Default;
        var unknown = new List<string>();
        if (entry is null) return (use, unknown);

        var folded = entry.Folded();
        foreach (var field in Fields)
        {
            if (folded[field] is not { } raw) continue;
            switch (field)
            {
                case "use" when ModeOf(raw) is { } mode: use = use with { Use = mode }; break;
                case "keep" when KeepOf(raw) is { } keep: use = use with { Keep = keep }; break;
                case "switch" when SwitchOf(raw) is { } switched: use = use with { Switch = switched }; break;
                case "curve" when UseCurve.Read(raw) is { } curve: use = use with { Curve = curve }; break;
                default: unknown.Add(field); break;
            }
        }

        if (entry["early"] is { } early && FlagOf(early) is null) unknown.Add("early");
        if (entry["near"] is { } near && NearOf(near) is null) unknown.Add("near");
        unknown.AddRange(entry.Fields.Select(field => field.Key)
            .Where(name => !Fields.Contains(name, StringComparer.Ordinal) && !OldNames.Contains(name, StringComparer.Ordinal)
                && !Retired.Contains(name, StringComparer.Ordinal)));
        return (use, unknown);
    }

    internal static string? ModeOf(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.String && Modes.Contains(raw.GetString()!, StringComparer.Ordinal) ? raw.GetString() : null;

    internal static string? SwitchOf(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.String && Switches.Contains(raw.GetString()!, StringComparer.Ordinal) ? raw.GetString() : null;

    internal static bool? FlagOf(JsonElement raw) => raw.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    internal static string? KeepOf(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.String && raw.GetString()!.Trim() is { Length: > 0 } keep ? keep : null;

    /// <summary>A whole percent in range, however the number is spelled (<c>85.0</c> is 85).</summary>
    internal static int? NearOf(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Number && raw.TryGetDouble(out var near) && near == Math.Floor(near)
            && near >= NearLowest && near <= NearHighest ? (int)near : null;
}

/// <summary>
/// One scope's settings as the file holds them (TOOL6a): by name, in the order read, each a value this build knows or not.
/// An editor keeps what it has no field for, so a setting a newer build writes outlives this one's save.
/// </summary>
public sealed record UseEntry(IReadOnlyList<KeyValuePair<string, JsonElement>> Fields)
{
    public static readonly UseEntry Empty = new([]);

    public bool IsEmpty => Fields.Count == 0;

    /// <summary>The value written for <paramref name="name"/>, or null where none is.</summary>
    public JsonElement? this[string name]
    {
        get
        {
            foreach (var (key, value) in Fields)
            {
                if (key == name) return value;
            }

            return null;
        }
    }

    /// <summary>The entry with <paramref name="name"/> set where it stood, or added last; a null value takes it out.</summary>
    public UseEntry With(string name, JsonElement? value)
    {
        var fields = new List<KeyValuePair<string, JsonElement>>();
        var placed = false;
        foreach (var field in Fields)
        {
            if (field.Key != name)
            {
                fields.Add(field);
            }
            else if (value is { } set && !placed)
            {
                fields.Add(KeyValuePair.Create(name, set));
                placed = true;
            }
        }

        if (value is { } added && !placed) fields.Add(KeyValuePair.Create(name, added));
        return new UseEntry(fields);
    }

    /// <summary>A change made over what the entry means: its old names read on first, then the change applied.</summary>
    internal UseEntry Changed(UseChange change) => Folded().Applied(change);

    /// <summary>
    /// The old names read on (D165 §6): <c>early</c> and <c>near</c> taken out, and what reads of them applied as a change
    /// applies them, over the entry's own switch and curve — as the CLI's <c>foldedUse</c> does.
    /// </summary>
    internal UseEntry Folded()
    {
        if (this["early"] is null && this["near"] is null) return this;
        var early = this["early"] is { } flag ? RotationUse.FlagOf(flag) : null;
        var near = this["near"] is { } percent ? RotationUse.NearOf(percent) : null;
        return With("early", null).With("near", null).Applied(new UseChange(Early: early, Near: near));
    }

    /// <summary>
    /// The entry as written: its old names read on, the retired names and a curve that reads but names no window taken out — as the
    /// CLI's <c>writtenUse</c> leaves it. An entry written as nothing is not written.
    /// </summary>
    internal UseEntry Written()
    {
        var folded = RotationUse.Retired.Aggregate(Folded(), (entry, retired) => entry.With(retired, null));
        return folded["curve"] is { } raw && UseCurve.Read(raw) is { IsEmpty: true } ? folded.With("curve", null) : folded;
    }

    /// <summary>A change applied to an entry whose old names are read on already: the old names first, then the new.</summary>
    private UseEntry Applied(UseChange change)
    {
        var entry = this;
        if (change.Use is { } mode) entry = entry.With("use", Element(writer => writer.WriteStringValue(mode)));
        if (change.NoKeep) entry = entry.With("keep", null);
        else if (change.Keep?.Trim() is { Length: > 0 } keep) entry = entry.With("keep", Element(writer => writer.WriteStringValue(keep)));

        if (change.Early == false) entry = entry.Switched("off");
        else if (change.Early == true && entry.IsOff) entry = entry.Switched("smart");
        if (change.Near is { } near && near != RotationUse.SmartSession)
        {
            entry = entry.WithWindow("session", [new CurvePoint(0, near)]);
            if (!entry.IsOff) entry = entry.Switched("curve");
        }
        else if (change.Near == RotationUse.SmartSession)
        {
            entry = entry.WithWindow("session", null);
        }

        if (change.Switch is { } switched) entry = entry.Switched(switched);
        if (change.NoCurve) entry = entry.With("curve", null);
        else if (change.Curve is { } curve)
        {
            foreach (var window in UseCurve.Windows)
            {
                if (curve[window] is { } points) entry = entry.WithWindow(window, points);
            }
        }

        return entry;
    }

    private bool IsOff => this["switch"] is { } raw && RotationUse.SwitchOf(raw) == "off";

    private UseEntry Switched(string to) => With("switch", Element(writer => writer.WriteStringValue(to)));

    /// <summary>One window of the curve set, or taken out where <paramref name="points"/> is null; a curve left naming none goes.</summary>
    private UseEntry WithWindow(string window, IReadOnlyList<CurvePoint>? points)
    {
        var windows = this["curve"] is { ValueKind: JsonValueKind.Object } curve
            ? new UseEntry([.. curve.EnumerateObject().Select(each => KeyValuePair.Create(each.Name, each.Value.Clone()))])
            : Empty;
        windows = windows.With(window, points is null ? null : Element(writer => UseCurve.WritePoints(writer, points)));
        return windows.IsEmpty ? With("curve", null) : With("curve", Element(windows.WriteFields));
    }

    /// <summary>
    /// The entry as written: the settings this build knows in their order, a value it knows normalised, then the rest as
    /// read — as the CLI's <c>writtenUse</c> writes it. Called on <see cref="Written"/>'s entry, so no old name is left.
    /// </summary>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        foreach (var field in RotationUse.Fields)
        {
            if (this[field] is not { } raw) continue;
            writer.WritePropertyName(field);
            switch (field)
            {
                case "use" when RotationUse.ModeOf(raw) is { } mode: writer.WriteStringValue(mode); break;
                case "keep" when RotationUse.KeepOf(raw) is { } keep: writer.WriteStringValue(keep); break;
                case "switch" when RotationUse.SwitchOf(raw) is { } switched: writer.WriteStringValue(switched); break;
                case "curve" when UseCurve.Read(raw) is { } curve: curve.WriteTo(writer); break;
                default: raw.WriteTo(writer); break;
            }
        }

        foreach (var (name, value) in Fields.Where(field => !RotationUse.Fields.Contains(field.Key, StringComparer.Ordinal)
            && !RotationUse.OldNames.Contains(field.Key, StringComparer.Ordinal)
            && !RotationUse.Retired.Contains(field.Key, StringComparer.Ordinal)))
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }
    }

    /// <summary>
    /// The entry as the file held it, each value as read, for the text a save's edit is measured from (KEEPALL1b): the settings this
    /// build knows in their written order, then every other field in the order read, the old and retired names among them, so an
    /// entry the writer would write alike measures as no edit, and its old and retired names as ones the edit takes out.
    /// </summary>
    internal void WriteAsRead(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (name, value) in RotationUse.Fields.Where(field => this[field] is not null).Select(field => KeyValuePair.Create(field, this[field]!.Value))
            .Concat(Fields.Where(field => !RotationUse.Fields.Contains(field.Key, StringComparer.Ordinal))))
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>The fields as one object, each as held.</summary>
    private void WriteFields(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (name, value) in Fields)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    internal static JsonElement Element(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}

/// <summary>
/// A point of a curve (D165 point 2): how much of the window's time has passed, and the most of it an account should have
/// used by then, each a whole percent.
/// </summary>
public readonly record struct CurvePoint(int Elapsed, int Used);

/// <summary>
/// The person's curve (D165 point 2; the switch design §2, §6): per window, the most of it an account should have used by
/// each point of its time, the points joined by straight lines, flat before the first and after the last. A window left
/// out takes the smart curve's. A twin of the CLI's <c>Curve</c>, <c>readCurve</c> and <c>curveProblem</c>: the shared
/// table's <c>refusal</c> rows hold the sentences, word for word.
/// </summary>
public sealed record UseCurve
{
    /// <summary>A curve's windows, as <c>windows.json</c> names them (the switch design §5), in the order they are written.</summary>
    public static readonly IReadOnlyList<string> Windows = ["session", "weekly"];

    /// <summary>A window's curve has one point to this many (the switch design §6).</summary>
    public const int MostPoints = 12;

    /// <summary>No curve of the person's own: every window the smart curve's.</summary>
    public static readonly UseCurve None = new(new Dictionary<string, IReadOnlyList<CurvePoint>>());

    public UseCurve(IReadOnlyDictionary<string, IReadOnlyList<CurvePoint>> points) => Points = points;

    /// <summary>Each window the curve names, by name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<CurvePoint>> Points { get; }

    /// <summary>The window's points, or null where the curve leaves it to the smart curve.</summary>
    public IReadOnlyList<CurvePoint>? this[string window] => Points.GetValueOrDefault(window);

    public bool IsEmpty => Points.Count == 0;

    /// <summary>One window flat at <paramref name="used"/>: a single point.</summary>
    public static UseCurve Flat(string window, int used) =>
        new(new Dictionary<string, IReadOnlyList<CurvePoint>> { [window] = [new CurvePoint(0, used)] });

    public bool Equals(UseCurve? other) =>
        other is not null && Points.Count == other.Points.Count
        && Points.All(window => other[window.Key] is { } theirs && theirs.SequenceEqual(window.Value));

    public override int GetHashCode() => Points.Count;

    /// <summary>A curve as read: each window it names, or null where it breaks a rule (<see cref="Problem"/>).</summary>
    public static UseCurve? Read(JsonElement raw)
    {
        if (Problem(raw) is not null) return null;
        var points = new Dictionary<string, IReadOnlyList<CurvePoint>>(StringComparer.Ordinal);
        foreach (var window in raw.EnumerateObject())
        {
            points[window.Name] = [.. window.Value.EnumerateArray().Select(point => new CurvePoint((int)point[0].GetDouble(), (int)point[1].GetDouble()))];
        }

        return new UseCurve(points);
    }

    /// <summary>
    /// The first problem with a curve a door is handed, in the sentence both twins say, or null. Window by window in the
    /// order written, then point by point: its shape, elapsed from 0 to 100, used from 0 to 100, elapsed rising, used never
    /// falling — the CLI's <c>curveProblem</c>.
    /// </summary>
    public static string? Problem(JsonElement raw) => ProblemOf(raw)?.Sentence;

    /// <summary>
    /// The first problem with a curve a door is handed, as <see cref="Problem"/> finds it, with the rule it breaks and the
    /// window, point and numbers it names (SWITCH1d), so a door that refuses it in a code says it in the reader's language.
    /// </summary>
    public static CurveProblem? ProblemOf(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return new CurveProblem("shape", "A curve is an object of windows, `session` or `weekly`, each a list of rows [elapsed, used].");
        }

        foreach (var window in raw.EnumerateObject())
        {
            var name = window.Name;
            if (!Windows.Contains(name, StringComparer.Ordinal))
            {
                return new CurveProblem("window", $"`{name}` is not a window — a curve's windows are `session` and `weekly`.", name);
            }

            if (window.Value.ValueKind != JsonValueKind.Array)
            {
                return new CurveProblem("list", $"`{name}` is not a list of rows [elapsed, used].", name);
            }

            var count = window.Value.GetArrayLength();
            if (count is < 1 or > MostPoints)
            {
                return new CurveProblem("count", $"`{name}` has {count} rows — a window's curve has one to twelve.", name, Count: count);
            }

            (double Elapsed, double Used)? before = null;
            var at = 0;
            foreach (var point in window.Value.EnumerateArray())
            {
                at++;
                if (PointOf(point) is not { } read)
                {
                    return new CurveProblem("point", $"`{name}`'s row {at} is not two whole numbers [elapsed, used].", name, at);
                }

                var (elapsed, used) = read;
                if (elapsed is < 0 or > 100)
                {
                    return new CurveProblem(
                        "elapsed", $"`{name}`'s row {at} is at {Said(elapsed)}% of the window's time — elapsed is from 0 to 100.",
                        name, at, Said(elapsed));
                }

                if (used is < 0 or > 100)
                {
                    return new CurveProblem(
                        "used", $"`{name}`'s row {at} has used {Said(used)}% — used is from 0 to 100.", name, at, Said(used));
                }

                if (before is { } earlier && elapsed <= earlier.Elapsed)
                {
                    return new CurveProblem(
                        "rising",
                        $"`{name}`'s row {at} is at {Said(elapsed)}% of the window's time, not after row {at - 1}'s "
                        + $"{Said(earlier.Elapsed)}% — elapsed rises from row to row.",
                        name, at, Said(elapsed), Said(earlier.Elapsed));
                }

                if (before is { } had && used < had.Used)
                {
                    return new CurveProblem(
                        "falling",
                        $"`{name}`'s row {at} has used {Said(used)}%, less than row {at - 1}'s {Said(had.Used)}% — used never falls.",
                        name, at, Said(used), Said(had.Used));
                }

                before = (elapsed, used);
            }
        }

        return null;
    }

    /// <summary>The curve as written: its windows in <see cref="Windows"/>' order, each point <c>[elapsed, used]</c>.</summary>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var window in Windows)
        {
            if (this[window] is not { } points) continue;
            writer.WritePropertyName(window);
            WritePoints(writer, points);
        }

        writer.WriteEndObject();
    }

    internal static void WritePoints(Utf8JsonWriter writer, IReadOnlyList<CurvePoint> points)
    {
        writer.WriteStartArray();
        foreach (var point in points)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(point.Elapsed);
            writer.WriteNumberValue(point.Used);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }

    /// <summary>A point that is two whole numbers, however each is spelled (<c>30.0</c> is 30); null for any other.</summary>
    private static (double Elapsed, double Used)? PointOf(JsonElement point) =>
        point.ValueKind == JsonValueKind.Array && point.GetArrayLength() == 2 && Whole(point[0]) is { } elapsed && Whole(point[1]) is { } used
            ? (elapsed, used)
            : null;

    private static double? Whole(JsonElement number) =>
        number.ValueKind == JsonValueKind.Number && number.TryGetDouble(out var value) && double.IsFinite(value) && value == Math.Floor(value)
            ? value
            : null;

    /// <summary>A whole number as both twins say it.</summary>
    private static string Said(double value) => value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// What is wrong with a curve a door is handed (SWITCH1d): the rule it breaks, the sentence both twins say, and the window,
/// point and numbers it names, so a door that refuses it in a code says it in the reader's language (the modules'
/// <c>ACCOUNT_USE_CURVE</c>, its rule the catalogue's form).
/// </summary>
/// <param name="Kind">The rule: <c>shape</c> (not an object of windows), <c>window</c> (a name that is no window), <c>list</c>
/// (a window that is not a list), <c>count</c> (not one to twelve points), <c>point</c> (not two whole numbers), <c>elapsed</c>
/// or <c>used</c> (out of 0 to 100), <c>rising</c> (elapsed not after the point before) or <c>falling</c> (used less than it).</param>
/// <param name="Point">The point, from 1, where the rule is one point's.</param>
/// <param name="Value">The point's number the rule weighs, as both twins say it.</param>
/// <param name="Before">The point before's number it is weighed against, for <c>rising</c> and <c>falling</c>.</param>
/// <param name="Count">How many points the window has, for <c>count</c>.</param>
public sealed record CurveProblem(
    string Kind, string Sentence, string? Window = null, int? Point = null, string? Value = null, string? Before = null, int? Count = null);

/// <summary>
/// A change to one scope's settings: a field null is left as it was; <paramref name="NoKeep"/> keeps none;
/// <paramref name="Curve"/> sets each window it names, keeping the others, and <paramref name="NoCurve"/> takes the curve
/// out. A door validates each value first; <see cref="HarnessSettings.WithUse"/> writes it as given.
/// </summary>
/// <remarks>
/// <paramref name="Early"/> and <paramref name="Near"/> are the old names (D165 §6), which the modules' <c>ACCOUNT_USE</c>
/// and Ask Daoris's <c>use</c> door still read beside <c>switch</c> and <c>curve</c> since SWITCH1d, for a page or a proposal
/// written before it: <c>Early</c> false is <c>switch: off</c>, and true is
/// <c>smart</c> where it was off; <c>Near</c> other than 90 is the session window flat at it, and <c>switch: curve</c> unless
/// it is off, and 90 takes the session window back to the smart curve's. They are applied before <paramref name="Switch"/>
/// and <paramref name="Curve"/>, so a new name beside an old one is the one written — as the CLI's <c>UseChange</c>.
/// </remarks>
public sealed record UseChange(
    string? Use = null, string? Keep = null, bool NoKeep = false, bool? Early = null, int? Near = null,
    string? Switch = null, UseCurve? Curve = null, bool NoCurve = false);

/// <summary>The scope a start reads (D130 §2 rule 1).</summary>
/// <param name="From">Whose scope: <see cref="ChoiceFrom.Workspace"/> or <see cref="ChoiceFrom.Machine"/>.</param>
/// <param name="Default">The scope's own default, trimmed, or none.</param>
/// <param name="List">The scope's list, empty for none.</param>
/// <param name="Begins">Its default where its list holds it, else its list's first; with no list, its default alone.</param>
/// <param name="Use">How it is used; today's defaults with no list.</param>
/// <param name="Unknown">The settings it holds that this build does not know, by name.</param>
public sealed record RotationScope(
    ChoiceFrom From, string? Default, IReadOnlyList<string> List, string? Begins, RotationUse Use, IReadOnlyList<string> Unknown);

/// <summary>What binds a scope (D130 §3.1, §4.6).</summary>
public enum ScopeProblemKind
{
    /// <summary>Its default is not in its list.</summary>
    Default,

    /// <summary>Its kept account is not in its list.</summary>
    Keep,

    /// <summary>Its kept account is the one account its list holds, so driven work would have none.</summary>
    Alone,
}

/// <summary>
/// Why a scope cannot be written (D130 §3.1, §4.6): its default or its kept account outside its list, or a kept account that
/// leaves driven work none. Both doors refuse it; the CLI's <c>scopeProblem</c> is the twin. No list is refused for its
/// length: a list of one with no kept account, and a list of many, are both fine.
/// </summary>
public sealed record ScopeProblem(ScopeProblemKind Kind, string Account)
{
    /// <summary>
    /// The first problem with a scope's default, list and kept account, or null. Names compare exactly, as a door compares
    /// a name with its directory; a scope with no list is its default alone, so only a kept account is refused there.
    /// </summary>
    public static ScopeProblem? Of(string? scopeDefault, IReadOnlyList<string> list, string? keep)
    {
        var own = scopeDefault?.Trim();
        if (!string.IsNullOrEmpty(own) && list.Count > 0 && !list.Contains(own, StringComparer.Ordinal))
        {
            return new ScopeProblem(ScopeProblemKind.Default, own);
        }

        var kept = keep?.Trim();
        if (string.IsNullOrEmpty(kept)) return null;
        if (!list.Contains(kept, StringComparer.Ordinal)) return new ScopeProblem(ScopeProblemKind.Keep, kept);
        return list.Any(name => name != kept) ? null : new ScopeProblem(ScopeProblemKind.Alone, kept);
    }
}
