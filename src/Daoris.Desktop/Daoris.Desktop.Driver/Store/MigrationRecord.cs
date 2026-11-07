using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The home's migration record as read, below every domain so a door may ask whether a step it waits on is done: the migration it is
/// of and the steps it holds done; null where there is none or it does not read.
/// </summary>
public static class MigrationRecord
{
    public static (string Migration, IReadOnlyList<string> Steps)? Read(string record)
    {
        try
        {
            if (!File.Exists(record) || JsonNode.Parse(AtomicFile.ReadText(record)) is not JsonObject root) return null;
            if ((string?)root["migration"] is not { } migration || root["steps"] is not JsonArray steps) return null;
            return (migration, [.. steps.Select(each => each?.GetValue<string>()).OfType<string>()]);
        }
        catch (Exception error) when (error is JsonException or IOException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
