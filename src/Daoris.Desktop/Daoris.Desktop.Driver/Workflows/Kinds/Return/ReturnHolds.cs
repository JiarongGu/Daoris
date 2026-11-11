using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a return waiting on the person holds on its status: each path changed on both sides with the versions it showed, each path refused
/// with its code (<see cref="ReturnRefusal"/>), and how many files it already brought back. Its wait is read from it, and a choice kept on a
/// path is bound to the versions it showed (<see cref="ReturnNode.ChoiceKey"/>), so a choice never lands on a version the person did not see.
/// </summary>
public sealed record ReturnHolds(IReadOnlyDictionary<string, ReturnShown> Waiting, IReadOnlyDictionary<string, string> Refused, int Returned)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ReturnHolds Of(FolderReturned returned) => new(returned.Shown, returned.Refused, returned.Written.Count);

    public string Write() => JsonSerializer.Serialize(this, Json);

    /// <summary>The holds an active return's status keeps; one kept before they were written whole is its lines, a path each, a refused one with its code after a tab.</summary>
    public static ReturnHolds Read(string? holds)
    {
        if (holds is { Length: > 0 } && holds[0] == '{')
        {
            try
            {
                if (JsonSerializer.Deserialize<ReturnHolds>(holds, Json) is { } read) return read;
            }
            catch (JsonException)
            {
            }
        }

        var lines = (holds ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return new(
            lines.Where(line => !line.Contains('\t')).Distinct(StringComparer.Ordinal).ToDictionary(line => line, _ => new ReturnShown(null, null), StringComparer.Ordinal),
            lines.Where(line => line.Contains('\t')).Select(line => line.Split('\t', 2)).DistinctBy(parts => parts[0]).ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal),
            0);
    }
}
