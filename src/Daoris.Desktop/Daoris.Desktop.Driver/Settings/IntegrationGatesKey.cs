using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// This machine's acceptance of each repository's integration declaration, as <c>driver.json</c> keeps it, <c>integrationGates</c>
/// (INTEGRATE1d; D215 point 2, the integration design §4.1): by repository, git's blob id of the <c>daoris.gates.json</c> the owner
/// accepted for integration, or <c>none</c> where the line held no declaration. Its own key, beside the landed route's acceptance
/// (<c>workflows</c>' <c>landed.gates</c>) and in the shape of <c>heavyGates</c>' map, because integrating needs no verification
/// pipeline; never bound to a chosen workflow. The key's shape and its reading and writing, by the settings' own parser; what an
/// acceptance means, and where it stands against the line's tip, stays with the workplace's integration acceptance.
/// </summary>
public static class IntegrationGatesKey
{
    /// <summary>The file's key.</summary>
    public const string Key = "integrationGates";

    /// <summary>Why a value is no acceptance, in the words every door says.</summary>
    public const string Problem =
        "an integration's acceptance is the blob id git gives the `daoris.gates.json` you accepted, 40 or 64 lower-case hex, or `none` "
        + "where the line held none.";

    /// <summary>Whether a value is an acceptance as kept: a whole blob id, 40 or 64 lower-case hex, or <c>none</c> (the landed route's own rule).</summary>
    public static bool IsAcceptance(string? value) => WorkflowKey.IsGates(value);

    /// <summary>
    /// The map as <c>driver.json</c> holds it: each repository's name trimmed, a blank one and a value no acceptance passed over, a
    /// name written twice in any case read where first readable (CASEMAP1); none where the key is absent or no object.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Map(JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(Key, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || map.ContainsKey(named) || property.Value.ValueKind != JsonValueKind.String) continue;
            if (property.Value.GetString() is { } blob && IsAcceptance(blob)) map[named] = blob;
        }

        return map;
    }

    /// <summary>The map written as <c>driver.json</c> keeps it, only when set, repositories in ordinal order.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(Key);
        foreach (var (repository, blob) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(repository, blob);
        writer.WriteEndObject();
    }
}
