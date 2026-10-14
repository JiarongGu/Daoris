using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one field reader the driver's modules share (DEDUP1a): every reader answers absent for a value of the wrong
/// kind, and for an element that is not an object at all, where the private copies it replaced threw on one.
/// </summary>
public sealed class JsonTests
{
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void Text_answers_the_string_and_null_for_missing_or_not_a_string()
    {
        var element = Parse("""{"a":"x","e":"","n":3,"o":{},"z":null}""");

        Assert.Equal("x", Json.Text(element, "a"));
        Assert.Equal("", Json.Text(element, "e"));
        Assert.Null(Json.Text(element, "n"));
        Assert.Null(Json.Text(element, "o"));
        Assert.Null(Json.Text(element, "z"));
        Assert.Null(Json.Text(element, "missing"));
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("\"text\"")]
    [InlineData("7")]
    [InlineData("null")]
    [InlineData("true")]
    public void Every_reader_answers_absent_for_an_element_that_is_not_an_object(string json)
    {
        var element = Parse(json);

        Assert.Null(Json.Text(element, "a"));
        Assert.Null(Json.NonEmpty(element, "a"));
        Assert.Null(Json.Int(element, "a"));
        Assert.Null(Json.Long(element, "a"));
        Assert.Null(Json.Number(element, "a"));
        Assert.False(Json.Flag(element, "a"));
        Assert.Empty(Json.Strings(element, "a"));
        Assert.Empty(Json.Items(element, "a"));
    }

    [Fact]
    public void NonEmpty_treats_an_empty_string_as_missing()
    {
        var element = Parse("""{"a":"x","e":"","n":1}""");

        Assert.Equal("x", Json.NonEmpty(element, "a"));
        Assert.Null(Json.NonEmpty(element, "e"));
        Assert.Null(Json.NonEmpty(element, "n"));
        Assert.Null(Json.NonEmpty(element, "missing"));
    }

    [Fact]
    public void Numbers_are_null_when_missing_not_a_number_or_out_of_range()
    {
        var element = Parse("""{"i":5,"big":9999999999,"huge":1e999,"frac":1.5,"s":"5"}""");

        Assert.Equal(5, Json.Int(element, "i"));
        Assert.Null(Json.Int(element, "big"));
        Assert.Null(Json.Int(element, "frac"));
        Assert.Null(Json.Int(element, "s"));
        Assert.Null(Json.Int(element, "missing"));

        Assert.Equal(9999999999L, Json.Long(element, "big"));
        Assert.Null(Json.Long(element, "frac"));
        Assert.Null(Json.Long(element, "s"));

        Assert.Equal(1.5, Json.Number(element, "frac"));
        Assert.Equal(5.0, Json.Number(element, "i"));
        Assert.Null(Json.Number(element, "huge"));
        Assert.Null(Json.Number(element, "s"));
        Assert.Null(Json.Number(element, "missing"));
    }

    [Fact]
    public void Flag_is_true_only_for_a_true_boolean()
    {
        var element = Parse("""{"t":true,"f":false,"s":"true","n":1}""");

        Assert.True(Json.Flag(element, "t"));
        Assert.False(Json.Flag(element, "f"));
        Assert.False(Json.Flag(element, "s"));
        Assert.False(Json.Flag(element, "n"));
        Assert.False(Json.Flag(element, "missing"));
    }

    [Fact]
    public void Strings_drops_non_strings_and_empties_unless_asked_to_keep_them()
    {
        var element = Parse("""{"a":["x","",3,null,"y"],"o":{}}""");

        Assert.Equal(["x", "y"], Json.Strings(element, "a"));
        Assert.Equal(["x", "", "y"], Json.Strings(element, "a", keepEmpty: true));
        Assert.Empty(Json.Strings(element, "o"));
        Assert.Empty(Json.Strings(element, "missing"));
    }

    [Fact]
    public void Items_are_the_objects_of_an_array_read_now()
    {
        IReadOnlyList<JsonElement> items;
        using (var document = JsonDocument.Parse("""{"a":[{"k":1},3,"s",{"k":2}]}"""))
            items = Json.Items(document.RootElement, "a");

        Assert.Equal(2, items.Count);
        Assert.Equal(1, Json.Int(items[0], "k"));
        Assert.Equal(2, Json.Int(items[1], "k"));
    }

    [Fact]
    public void Items_is_empty_when_the_field_is_not_an_array()
    {
        var element = Parse("""{"a":{"k":1},"s":"x"}""");

        Assert.Empty(Json.Items(element, "a"));
        Assert.Empty(Json.Items(element, "s"));
        Assert.Empty(Json.Items(element, "missing"));
    }

    [Fact]
    public void Node_readers_answer_the_same_on_objects_and_values()
    {
        var entry = JsonNode.Parse("""{"a":"x","e":"","n":3,"t":true,"f":"true"}""")!.AsObject();

        Assert.Equal("x", Json.Text(entry, "a"));
        Assert.Equal("", Json.Text(entry, "e"));
        Assert.Null(Json.Text(entry, "n"));
        Assert.Equal("x", Json.NonEmpty(entry, "a"));
        Assert.Null(Json.NonEmpty(entry, "e"));
        Assert.True(Json.Flag(entry, "t"));
        Assert.False(Json.Flag(entry, "f"));
        Assert.False(Json.Flag(entry, "missing"));

        Assert.Equal("x", Json.Text(entry["a"]));
        Assert.Null(Json.Text(entry["n"]));
        Assert.Null(Json.Text((JsonNode?)null));
    }

    [Fact]
    public void Trimmed_is_the_text_trimmed_and_null_when_blank_or_not_text()
    {
        var entry = JsonNode.Parse("""{"a":"  x ","b":"   ","n":3}""")!.AsObject();

        Assert.Equal("x", Json.Trimmed(entry["a"]));
        Assert.Null(Json.Trimmed(entry["b"]));
        Assert.Null(Json.Trimmed(entry["n"]));
        Assert.Null(Json.Trimmed(null));
    }

    [Fact]
    public void TrimmedStrings_keeps_the_non_blank_strings_of_an_array_trimmed()
    {
        var entry = JsonNode.Parse("""{"a":[" x ","",3,"  ","y"],"o":{}}""")!.AsObject();

        Assert.Equal(["x", "y"], Json.TrimmedStrings(entry["a"]));
        Assert.Empty(Json.TrimmedStrings(entry["o"]));
        Assert.Empty(Json.TrimmedStrings(null));
    }
}
