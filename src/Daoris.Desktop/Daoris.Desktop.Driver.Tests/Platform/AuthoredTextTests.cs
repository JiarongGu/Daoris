using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>I18NOPEN1 (D222): words a person writes are one string or a map of language tags to text, shown in the viewer's language.</summary>
public sealed class AuthoredTextTests
{
    private static AuthoredText? Read(string json, int most = 0, bool oneLine = false)
    {
        using var document = JsonDocument.Parse(json);
        return AuthoredText.Read(document.RootElement, most, oneLine);
    }

    [Fact]
    public void A_string_is_one_text_in_any_language()
    {
        var text = Read("\"Reviewer\"")!;
        Assert.Equal("Reviewer", text.In("en"));
        Assert.Equal("Reviewer", text.In("fr"));
        Assert.Equal("\"Reviewer\"", text.Json());
    }

    [Fact]
    public void A_map_shows_the_viewers_language_then_its_base_language_then_the_first_entry()
    {
        var text = Read("""{ "fr": "Relecteur", "zh": "审阅", "en": "Reviewer" }""")!;
        Assert.Equal("Relecteur", text.In("fr"));
        Assert.Equal("审阅", text.In("zh-TW"));
        Assert.Equal("Relecteur", text.In("de"));
        Assert.Equal("Relecteur", text.AsWritten);
        Assert.Equal("""{"fr":"Relecteur","zh":"审阅","en":"Reviewer"}""", text.Json());
    }

    [Fact]
    public void An_old_pair_of_en_and_zh_reads_and_writes_as_it_was()
    {
        var text = Read("""{ "en": "Build", "zh": "开发" }""")!;
        Assert.Equal("开发", text.In("zh"));
        Assert.Equal("Build", text.In("en"));
        Assert.Equal("""{"en":"Build","zh":"开发"}""", text.Json());
    }

    [Theory]
    [InlineData("\"  \"")]
    [InlineData("{}")]
    [InlineData("""{ "en": "" }""")]
    [InlineData("""{ "en": 3 }""")]
    [InlineData("""{ "not a tag": "x" }""")]
    [InlineData("3")]
    [InlineData("null")]
    public void A_value_that_is_no_text_does_not_read(string json) => Assert.Null(Read(json));

    [Fact]
    public void A_bound_holds_each_text_and_a_one_line_text_has_no_break()
    {
        Assert.Null(Read("\"abcdef\"", most: 5));
        Assert.Null(Read("""{ "en": "abc", "fr": "abcdef" }""", most: 5));
        Assert.NotNull(Read("""{ "en": "abc", "fr": "abcde" }""", most: 5));
        Assert.Null(Read("\"a\\nb\"", oneLine: true));
    }
}
