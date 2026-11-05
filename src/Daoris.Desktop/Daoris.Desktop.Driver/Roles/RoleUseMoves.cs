using System.Text.Json;

namespace Daoris.Driver;

/// <summary>How many of a role's lines left a choice for another because of one condition (<c>limit</c>, <c>transient</c>, <c>failed</c>, <c>quiet</c>, ...).</summary>
public sealed record RoleUseMoved(string Condition, int Moves);

/// <summary>
/// A role's moves between choices in the week (STAFFPOLICY1f), summed from the machine log's <c>actor.moved</c> lines, which a part the course
/// moved to another choice writes with the role and the condition that moved it. Names and counts only.
/// </summary>
public static class RoleUseMoves
{
    /// <summary>The moves of each role in <paramref name="lines"/>, by condition, the most first and ties by name; a role that moved nowhere has no entry.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<RoleUseMoved>> Of(IEnumerable<LogLine> lines) =>
        lines
            .Where(line => line.Event == "actor.moved" && line.Data.ValueKind == JsonValueKind.Object)
            .Select(line => (Role: Text(line.Data, "role"), Condition: Text(line.Data, "condition")))
            .Where(each => each.Role is not null && each.Condition is not null)
            .GroupBy(each => each.Role!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RoleUseMoved>)[.. group.GroupBy(each => each.Condition!, StringComparer.Ordinal)
                    .Select(moved => new RoleUseMoved(moved.Key, moved.Count()))
                    .OrderByDescending(moved => moved.Moves).ThenBy(moved => moved.Condition, StringComparer.Ordinal)],
                StringComparer.Ordinal);

    private static string? Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}
