using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The string a field or a node holds, which <see cref="Moments"/> reads a moment through and <see cref="Json.Text(JsonElement, string)"/> answers
/// (FOUNDATION1 mod-ii): absent for a value of the wrong kind and for an element that is no object, empty kept, and the moment read as before.
/// </summary>
public sealed class JsonTextTests
{
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void A_field_is_the_string_it_holds_and_absent_for_anything_else()
    {
        var element = Parse("""{"a":"x","e":"","n":3,"o":{},"z":null}""");

        Assert.Equal("x", JsonText.Of(element, "a"));
        Assert.Equal("", JsonText.Of(element, "e"));
        Assert.Null(JsonText.Of(element, "n"));
        Assert.Null(JsonText.Of(element, "o"));
        Assert.Null(JsonText.Of(element, "z"));
        Assert.Null(JsonText.Of(element, "missing"));
        Assert.Null(JsonText.Of(Parse("[1]"), "a"));
        Assert.Null(JsonText.Of(Parse("\"a\""), "a"));
    }

    [Fact]
    public void A_node_is_the_string_it_holds_and_absent_for_anything_else()
    {
        Assert.Equal("x", JsonText.Of(JsonValue.Create("x")));
        Assert.Equal("", JsonText.Of(JsonValue.Create("")));
        Assert.Null(JsonText.Of(JsonValue.Create(3)));
        Assert.Null(JsonText.Of(new JsonObject()));
        Assert.Null(JsonText.Of(null));
    }

    [Fact]
    public void Json_Text_and_the_moments_that_read_through_it_answer_as_they_did()
    {
        var element = Parse("""{"at":"2026-10-25T09:00:00.0000000+00:00","bad":"later","n":1}""");
        var node = JsonNode.Parse("""{"at":"2026-10-25T09:00:00.0000000+00:00","n":1}""")!;

        Assert.Equal(JsonText.Of(element, "at"), Json.Text(element, "at"));
        Assert.Equal(JsonText.Of(node["at"]), Json.Text(node["at"]));
        var moment = new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
        Assert.Equal(moment, Moments.Read(element, "at"));
        Assert.Equal(moment, Moments.Read(node["at"]));
        Assert.Null(Moments.Read(element, "bad"));
        Assert.Null(Moments.Read(element, "n"));
        Assert.Null(Moments.Read(node["n"]));
    }
}
