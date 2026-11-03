using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The person's said words as a record carries them, in the shape the service answers them: a stand-in for a park they answered.</summary>
internal static class PersonWords
{
    /// <summary>One person's word of <paramref name="text"/>, under the id the service gives a record's old answer column.</summary>
    public static IReadOnlyList<SaidWordView> Of(string text) => [new SaidWordView("answer", text, DateTimeOffset.MinValue, [], false)];

    /// <summary>The same word as the record's wire <c>said</c> list, for a stand-in host to answer.</summary>
    public static JsonNode? Wire(string? text) => text is null
        ? null
        : new JsonArray(new JsonObject { ["id"] = "answer", ["text"] = text, ["at"] = "2026-10-02T10:05:00Z", ["files"] = new JsonArray() });
}
