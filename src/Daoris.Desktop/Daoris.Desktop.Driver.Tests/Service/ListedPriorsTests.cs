using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The list's text read as runs that go on read each record, parsed once for each text: every record asked of the same text is a lookup,
/// and another text, even one equal to it, is its own.
/// </summary>
public sealed class ListedPriorsTests
{
    private static string Listed(params string[] ids) => new JsonArray(
    [
        .. ids.Select(id => (JsonNode)new JsonObject
        {
            ["id"] = id, ["quest"] = "q1", ["repository"] = "engine", ["door"] = "claude-code", ["state"] = "completed", ["kind"] = "chat",
            ["created"] = "2026-10-19T08:00:00+00:00", ["updated"] = "2026-10-19T09:00:00+00:00",
        }),
    ]).ToJsonString();

    [Fact]
    public void Every_record_asked_of_one_text_is_read_from_one_parse()
    {
        var json = Listed("a", "b", "teammate/c");

        var first = ServiceClient.ReadRecords(json);

        Assert.Same(first, ServiceClient.ReadRecords(json));
        Assert.Same(first["a"], ServiceClient.ReadRecord(json, "a"));
        Assert.Equal("b", ServiceClient.ReadRecord(json, "b")!.Session);
        Assert.Null(ServiceClient.ReadRecord(json, "teammate/c"));
        Assert.Null(ServiceClient.ReadRecord(json, "A"));
    }

    [Fact]
    public void Another_text_is_parsed_as_its_own()
    {
        var json = Listed("a");
        var first = ServiceClient.ReadRecords(json);

        var again = ServiceClient.ReadRecords(new string(json.AsSpan()));
        var other = ServiceClient.ReadRecords(Listed("b"));

        Assert.NotSame(first, again);
        Assert.Equal(first.Keys, again.Keys);
        Assert.Equal(["b"], other.Keys);
    }
}
