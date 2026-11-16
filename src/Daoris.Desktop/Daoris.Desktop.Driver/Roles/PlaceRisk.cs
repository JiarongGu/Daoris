using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A place's risk, which decides what an appointed role may hold there: low, medium or high.</summary>
public static class PlaceRisks
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    public static IReadOnlyList<string> All { get; } = [Low, Medium, High];

    public static bool Is(string? risk) => risk is Low or Medium or High;

    /// <summary>Whether an appointed role may hold an act at <paramref name="risk"/>: at low and medium, never at high.</summary>
    public static bool RoleMayHold(string risk) => risk is Low or Medium;
}

/// <summary>
/// The owner's marks on a place: what stays the owner role's whatever its risk. A destination's mark (<see cref="ProductionReleases"/>) covers
/// the place and every place under it; a line's mark (<see cref="Line"/>, <c>line.&lt;kind&gt;</c>) is the owner holding that place's level of
/// the reporting line alone, the one way the person holds a level.
/// </summary>
public static class PlaceMarks
{
    /// <summary>The place's production releases: their approval stays the owner role's.</summary>
    public const string ProductionReleases = "release.production";

    /// <summary>The owner role holds this place's supervisor level for every kind; <see cref="LineOf"/> for one kind.</summary>
    public const string Line = "line";

    public static string LineOf(string kind) => $"{Line}.{kind}";

    /// <summary>Whether <paramref name="mark"/> is one of the vocabulary.</summary>
    public static bool Is(string? mark) => mark is ProductionReleases or Line
        || (mark is { } named && named.StartsWith(Line + ".", StringComparison.Ordinal) && SupervisorKinds.Known(named[(Line.Length + 1)..]));

    /// <summary>Whether <paramref name="mark"/> is a destination's, which covers every place under the one it is set on.</summary>
    public static bool Destination(string mark) => mark == ProductionReleases;
}

/// <summary>One place's record: its risk and the owner's marks on it.</summary>
public sealed record PlaceRisk(string Risk, IReadOnlyList<string> Marks)
{
    public static PlaceRisk Default { get; } = new(PlaceRisks.Low, []);

    public bool Marked(string mark) => Marks.Contains(mark, StringComparer.Ordinal);
}

/// <summary>
/// What stands at a place: its risk and where it is set (the place's own, a wider place's, or the default), and the destination marks
/// covering it, its own and every wider place's together.
/// </summary>
public sealed record PlaceRiskNow(string Risk, string Source, IReadOnlyList<string> Marks)
{
    public bool Marked(string mark) => Marks.Contains(mark, StringComparer.Ordinal);

    /// <summary>Whether an appointed role may hold an act whose destination is <paramref name="destination"/> here: not at high risk, nor where the owner marked it.</summary>
    public bool RoleMayHold(string? destination = null) =>
        PlaceRisks.RoleMayHold(Risk) && (destination is null || !Marked(destination));
}

/// <summary>One change to a place's record, as every door makes it (the window's row, <c>daoris settings risk</c>, Ask Daoris's carded press).</summary>
public sealed record PlaceRiskEdit
{
    /// <summary>The repository the record is for; with none named, the workspace's, and with neither, the machine's.</summary>
    public string? Repository { get; init; }

    public string? Workspace { get; init; }

    /// <summary>The risk set; none leaves it as it stands.</summary>
    public string? Risk { get; init; }

    public IReadOnlyList<string> Mark { get; init; } = [];

    public IReadOnlyList<string> Unmark { get; init; } = [];
}

/// <summary>
/// Every place's risk and the owner's marks, one record (<c>placeRisk</c> in <c>driver.json</c>), keyed by place: <c>machine</c>,
/// <c>workspace/&lt;name&gt;</c>, <c>repository/&lt;name&gt;</c>. A place it does not name is low and unmarked. Set once by the owner through one
/// press and its twins; read by the roles' admission, the reporting line and the go-ahead.
/// </summary>
/// <remarks>
/// <see cref="Written"/> is false where <c>driver.json</c> holds no record at all (a home the migration has not reached, or one an older build
/// rewrote, which keeps no key it does not know): the reporting line then reads a level held by the person's rules or by nobody as the person's,
/// as it did before the record. That older reading ends when no home on a supported update path lacks the record.
/// </remarks>
public sealed record PlaceRiskBook(IReadOnlyDictionary<string, PlaceRisk> Places, bool Written = true)
{
    public const string Key = "placeRisk";

    public const string MachineKey = "machine";

    /// <summary>A home whose <c>driver.json</c> holds no record.</summary>
    public static PlaceRiskBook Unwritten { get; } = new(new Dictionary<string, PlaceRisk>(StringComparer.OrdinalIgnoreCase), Written: false);

    public const string OwnSource = "own";
    public const string WorkspaceSource = "workspace";
    public const string MachineSource = "machine";
    public const string DefaultSource = "default";

    /// <summary>The key of the place a scope names.</summary>
    public static string KeyOf(RoleScope scope) => scope.Kind switch
    {
        RoleScope.Repository => $"repository/{scope.Id}",
        RoleScope.Workspace => $"workspace/{scope.Id}",
        _ => MachineKey,
    };

    /// <summary>The key of the place an edit names: the repository's, else the workspace's, else the machine's.</summary>
    public static string KeyOf(string? workspace, string? repository) =>
        Trimmed(repository) is { } r ? $"repository/{r}" : Trimmed(workspace) is { } w ? $"workspace/{w}" : MachineKey;

    /// <summary>The record set on exactly the place <paramref name="scope"/> names; none where nothing is.</summary>
    public PlaceRisk? Own(RoleScope scope) => Places.GetValueOrDefault(KeyOf(scope));

    /// <summary>What stands at <paramref name="place"/>: the narrowest risk set, and every destination mark from the machine down.</summary>
    public PlaceRiskNow At(RolePlace place)
    {
        var chain = new List<(string Source, PlaceRisk? Risk)>
        {
            (OwnSource, Trimmed(place.Repository) is { } r ? Places.GetValueOrDefault($"repository/{r}") : null),
            (place.Repository is null ? OwnSource : WorkspaceSource, Trimmed(place.Workspace) is { } w ? Places.GetValueOrDefault($"workspace/{w}") : null),
            (place is { Repository: null, Workspace: null } ? OwnSource : MachineSource, Places.GetValueOrDefault(MachineKey)),
        };
        var set = chain.FirstOrDefault(each => each.Risk is not null);
        IReadOnlyList<string> marks = [.. chain.SelectMany(each => each.Risk?.Marks ?? []).Where(PlaceMarks.Destination).Distinct(StringComparer.Ordinal)];
        return set.Risk is { } risk ? new(risk.Risk, set.Source, marks) : new(PlaceRisks.Low, DefaultSource, marks);
    }

    /// <summary>Whether the owner holds the level of the reporting line at <paramref name="scope"/> for <paramref name="kind"/>, by a line's mark.</summary>
    public bool OwnerHoldsLine(RoleScope scope, string kind) =>
        Own(scope) is { } own && (own.Marked(PlaceMarks.Line) || own.Marked(PlaceMarks.LineOf(kind)));

    /// <summary>
    /// One change made, or its refusal thrown in the record's words with nothing changed: the risk set where one is named, the marks added
    /// and taken away; a place left low and unmarked loses its entry, the absent default.
    /// </summary>
    /// <exception cref="DriverException">A risk that is none of the three, or a mark that is none of the vocabulary.</exception>
    public PlaceRiskBook Apply(PlaceRiskEdit edit)
    {
        if (edit.Risk is { } named && !PlaceRisks.Is(named)) throw new DriverException(RiskWords);
        if (edit.Mark.Concat(edit.Unmark).FirstOrDefault(mark => !PlaceMarks.Is(mark)) is { } unknown) throw new DriverException($"`{unknown}` is no mark: {MarkWords}");
        var key = KeyOf(edit.Workspace, edit.Repository);
        var spelled = Places.Keys.FirstOrDefault(each => string.Equals(each, key, StringComparison.OrdinalIgnoreCase)) ?? key;
        var before = Places.GetValueOrDefault(spelled) ?? PlaceRisk.Default;
        IReadOnlyList<string> marks = [.. before.Marks.Concat(edit.Mark).Except(edit.Unmark, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        var after = new PlaceRisk(edit.Risk ?? before.Risk, marks);
        var changed = new Dictionary<string, PlaceRisk>(Places, StringComparer.OrdinalIgnoreCase);
        if (after.Risk == PlaceRisks.Low && after.Marks.Count == 0) changed.Remove(spelled);
        else changed[spelled] = after;
        return new(changed);
    }

    public const string RiskWords = "a place's risk is `low`, `medium` or `high`.";

    public const string MarkWords = "a mark is `release.production` (the place's production releases stay yours), `line` or `line.<kind>` (you hold the place's supervisor level).";

    /// <summary>The record as <c>driver.json</c> holds it: an entry with a problem is not read, a risk that is none of the three read as high.</summary>
    public static PlaceRiskBook Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(Key, out var element) || element.ValueKind != JsonValueKind.Object) return Unwritten;
        var places = new Dictionary<string, PlaceRisk>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object || places.ContainsKey(property.Name)) continue;
            var risk = Json.Text(property.Value, "risk");
            // A risk this build does not know is read on the safe side: no role holds an act there.
            var read = PlaceRisks.Is(risk) ? risk! : PlaceRisks.High;
            IReadOnlyList<string> marks = property.Value.TryGetProperty("marks", out var listed) && listed.ValueKind == JsonValueKind.Array
                ? [.. listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).Distinct(StringComparer.Ordinal)]
                : [];
            places[property.Name.Trim()] = new(read, marks);
        }

        return new(places);
    }

    /// <summary>The record written as <c>driver.json</c> keeps it, the empty record included, so a migrated home says it is written.</summary>
    public void Write(Utf8JsonWriter writer)
    {
        if (!Written) return;
        writer.WriteStartObject(Key);
        foreach (var (key, place) in Places.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(key);
            writer.WriteString("risk", place.Risk);
            writer.WriteStartArray("marks");
            foreach (var mark in place.Marks) writer.WriteStringValue(mark);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>An edit as the route and the verb spell it: <c>repository</c>, <c>workspace</c>, <c>risk</c>, <c>mark</c> and <c>unmark</c>.</summary>
    public static PlaceRiskEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new PlaceRiskEdit();
        IReadOnlyList<string> List(string name) => root.TryGetProperty(name, out var listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString() ?? "" : "")]
            : [];
        return new PlaceRiskEdit
        {
            Repository = Json.Text(root, "repository"),
            Workspace = Json.Text(root, "workspace"),
            Risk = root.TryGetProperty("risk", out var risk) ? risk.ValueKind == JsonValueKind.String ? risk.GetString() : "" : null,
            Mark = List("mark"),
            Unmark = List("unmark"),
        };
    }

    /// <summary>The rows <c>STATE</c> answers, one per place set, as the window's Settings read them.</summary>
    public IReadOnlyList<object> Rows() =>
    [
        .. Places.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (object)new { Place = pair.Key, pair.Value.Risk, pair.Value.Marks }),
    ];

    private static string? Trimmed(string? name) => name?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
