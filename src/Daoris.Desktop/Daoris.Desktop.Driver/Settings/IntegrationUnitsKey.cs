using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The digest of each unit of the declaration a repository's owner accepted (<see cref="DeclarationUnits"/>): the acceptance by unit that
/// replaces the whole file's blob. <paramref name="Blob"/> is the acceptance the digests were computed from, so a record ends where that
/// acceptance moves; <paramref name="Units"/> are the units by key, each with its digest.
/// </summary>
public sealed record AcceptedUnits(string Blob, IReadOnlyDictionary<string, string> Units);

/// <summary>
/// The accepted units of each repository, as <c>driver.json</c> keeps them, <c>integrationUnits</c>: by repository, an object holding the
/// <c>blob</c> they were computed from and the <c>units</c> by key. Beside <c>integrationGates</c>, whose blob still names the text a check
/// runs under; read as a stored shape, written only where set. What a unit being accepted means stays with the declaration's own rules.
/// </summary>
public static class IntegrationUnitsKey
{
    /// <summary>The file's key.</summary>
    public const string Key = "integrationUnits";

    /// <summary>
    /// The map as <c>driver.json</c> holds it: a repository's name trimmed, a blank one, a record with no blob that is an acceptance (and
    /// not <c>none</c>) or with a unit that is no string passed over, a name written twice in any case read where first readable.
    /// </summary>
    internal static IReadOnlyDictionary<string, AcceptedUnits> Map(JsonElement root)
    {
        var map = new Dictionary<string, AcceptedUnits>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(Key, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || map.ContainsKey(named) || Record(property.Value) is not { } record) continue;
            map[named] = record;
        }

        return map;
    }

    private static AcceptedUnits? Record(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("blob", out var blob) || blob.ValueKind != JsonValueKind.String || blob.GetString() is not { } id
            || !IntegrationGatesKey.IsAcceptance(id) || id == IntegrationAcceptance.None
            || !value.TryGetProperty("units", out var units) || units.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var unit in units.EnumerateObject())
        {
            if (unit.Value.ValueKind != JsonValueKind.String || unit.Value.GetString() is not { } digest) return null;
            found[unit.Name] = digest;
        }

        return new AcceptedUnits(id, found);
    }

    /// <summary>
    /// <paramref name="config"/> with the accepted units of <paramref name="repository"/> set, or cleared with null. A record is the units of
    /// the blob the acceptance names, so a setting for another blob or none is refused, and moving or clearing the acceptance ends it.
    /// </summary>
    /// <exception cref="DriverException">No acceptance, or one of another blob, for the repository.</exception>
    public static DriverConfig WithIntegrationUnits(this DriverConfig config, string repository, AcceptedUnits? record) =>
        DriverConfigEdits.IntegrationUnits(config, repository, record);

    /// <summary>The map written as <c>driver.json</c> keeps it, only when set, repositories and units in ordinal order.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, AcceptedUnits> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(Key);
        foreach (var (repository, record) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(repository);
            writer.WriteString("blob", record.Blob);
            writer.WriteStartObject("units");
            foreach (var (unit, digest) in record.Units.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(unit, digest);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
