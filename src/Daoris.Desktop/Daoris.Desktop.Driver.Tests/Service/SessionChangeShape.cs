using System.Text.Json.Nodes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The lists' change doors' paths and the contract's shape of a first read, held once for the wrappers over a stand-in's HTTP or doors.</summary>
internal static class SessionChangeShape
{
    public const string ChangesPath = "/api/sessions/changes";
    public const string ListDoor = "/api/sessions?includeClosed=true";
    public const string QuestChangesPath = "/api/quests/changes";
    public const string QuestListDoor = "/api/quests?includeClosed=true";

    public static string? ListDoorOf(string path) => path switch
    {
        ChangesPath => ListDoor,
        QuestChangesPath => QuestListDoor,
        _ => null,
    };

    /// <summary>The change that carries every record of <paramref name="sessions"/>, a JSON array, as a first read; an answer that is no array is passed as it came.</summary>
    public static string Whole(string sessions)
    {
        if (JsonNode.Parse(sessions) is not JsonArray records) return sessions;
        var order = new JsonArray();
        foreach (var record in records)
        {
            if (record?["id"] is JsonValue id && id.TryGetValue<string>(out var named)) order.Add(named);
        }

        return new JsonObject { ["mark"] = "m1", ["order"] = order, ["changed"] = records.DeepClone(), ["whole"] = true }.ToJsonString();
    }
}
