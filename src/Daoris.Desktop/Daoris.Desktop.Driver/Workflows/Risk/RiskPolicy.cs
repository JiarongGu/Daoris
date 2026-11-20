using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One row of the risk policy at a place: an act kind (and qualifier) to a class, with an optional answering role and access mode.</summary>
/// <param name="Class">The class as written: a class this build does not know reads <c>owner</c> (<see cref="RiskGate"/>).</param>
/// <param name="Mode">The access mode the row applies under (<see cref="RiskPlace.FullMode"/>, <see cref="RiskPlace.RestrictedMode"/>); null under either.</param>
public sealed record RiskRow(string Kind, string? Qualifier, string Class, string? By, string? Mode);

/// <summary>
/// The risk policy as the home's <c>driver.json</c> keeps it: <c>risk</c>, rows keyed by place (<c>machine</c>,
/// <c>workspace/&lt;name&gt;</c>, <c>repository/&lt;name&gt;</c>), each place a list of <c>{ "kind": "kind[/qualifier]", "class": "none|role|owner",
/// "by"?: role, "mode"?: "full|restricted" }</c>; and today's <c>placeRisk</c>, read through (<see cref="PlaceRisk"/>). Read only here: no door
/// writes a row yet, so <see cref="DriverConfig"/> keeps the key as written.
/// </summary>
/// <remarks>
/// An absent record reads as the seed. A record that does not read (<see cref="Fault"/>) reads every act at the owner, since a broken
/// governing record is the owner's to mend. A row naming no kind of the census is not read and is named (<see cref="Unknown"/>): it judges no
/// act this build knows, so leaving it loosens nothing. The older reader of <c>placeRisk</c> ends when no home on a supported update path
/// holds one (<c>compatible-builds</c>).
/// </remarks>
public sealed record RiskPolicy(IReadOnlyDictionary<string, IReadOnlyList<RiskRow>> Rows, PlaceRiskBook PlaceRisk, string Revision)
{
    public const string Key = "risk";

    /// <summary>The revision of a home that writes neither record: the seed alone.</summary>
    public const string SeedRevision = "seed";

    /// <summary>Why the record does not read, or null where it does.</summary>
    public string? Fault { get; init; }

    /// <summary>Each row naming a kind outside the census, as <c>&lt;place&gt; &lt;kind&gt;</c>.</summary>
    public IReadOnlyList<string> Unknown { get; init; } = [];

    /// <summary>The policy <paramref name="config"/>'s file holds, read from its source text.</summary>
    public static RiskPolicy Of(DriverConfig config)
    {
        using var document = JsonDocument.Parse(config.Source ?? config.ToJson());
        return Read(document.RootElement);
    }

    /// <summary>The policy in <c>driver.json</c>'s <paramref name="root"/>.</summary>
    public static RiskPolicy Read(JsonElement root)
    {
        var placeRisk = PlaceRiskBook.Read(root);
        var written = root.ValueKind == JsonValueKind.Object && root.TryGetProperty(Key, out var risk) ? risk : (JsonElement?)null;
        var carried = root.ValueKind == JsonValueKind.Object && root.TryGetProperty(PlaceRiskBook.Key, out var level) ? level.GetRawText() : null;
        var revision = written is null && carried is null ? SeedRevision : Digest($"{written?.GetRawText()}\n{carried}");
        var rows = new Dictionary<string, IReadOnlyList<RiskRow>>(StringComparer.OrdinalIgnoreCase);
        if (written is not { } record) return new(rows, placeRisk, revision);
        var unknown = new List<string>();
        var fault = ReadRows(record, rows, unknown);
        return new(fault is null ? rows : new Dictionary<string, IReadOnlyList<RiskRow>>(), placeRisk, revision) { Fault = fault, Unknown = unknown };
    }

    /// <summary>Whether <paramref name="key"/> names a place: the machine, a workspace or a repository.</summary>
    public static bool IsPlace(string key) =>
        key == PlaceRiskBook.MachineKey
        || (key.StartsWith("workspace/", StringComparison.Ordinal) && key.Length > "workspace/".Length)
        || (key.StartsWith("repository/", StringComparison.Ordinal) && key.Length > "repository/".Length);

    private static string? ReadRows(JsonElement record, Dictionary<string, IReadOnlyList<RiskRow>> rows, List<string> unknown)
    {
        if (record.ValueKind != JsonValueKind.Object) return "`risk` is not an object of places";
        foreach (var place in record.EnumerateObject())
        {
            if (!IsPlace(place.Name)) return $"`{place.Name}` is no place (`machine`, `workspace/<name>` or `repository/<name>`)";
            if (place.Value.ValueKind != JsonValueKind.Array) return $"`{place.Name}` holds no list of rows";
            var read = new List<RiskRow>();
            foreach (var row in place.Value.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || Text(row, "kind") is not { } spelled || Text(row, "class") is not { } named)
                {
                    return $"a row of `{place.Name}` names no kind and class";
                }

                var by = row.TryGetProperty("by", out var role) ? role.ValueKind == JsonValueKind.String ? role.GetString() : "" : null;
                var mode = row.TryGetProperty("mode", out var access) ? access.ValueKind == JsonValueKind.String ? access.GetString() : "" : null;
                if (by is { Length: 0 }) return $"a row of `{place.Name}` names its `by` role as no role";
                if (mode is not (null or RiskPlace.FullMode or RiskPlace.RestrictedMode)) return $"a row of `{place.Name}` names the mode `{mode}`, not `full` or `restricted`";
                var at = spelled.IndexOf('/', StringComparison.Ordinal);
                var kind = at < 0 ? spelled : spelled[..at];
                if (!RiskActs.Is(kind))
                {
                    unknown.Add($"{place.Name} {spelled}");
                    continue;
                }

                read.Add(new RiskRow(kind, at < 0 || at == spelled.Length - 1 ? null : spelled[(at + 1)..], named, by, mode));
            }

            rows.TryAdd(place.Name, read);
        }

        return null;
    }

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];
}
