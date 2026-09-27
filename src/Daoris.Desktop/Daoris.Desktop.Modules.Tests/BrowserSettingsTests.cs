using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// CHR7: Daoris's browser's settings, kept in <c>&lt;home&gt;/browser/settings.json</c> and read by
/// <c>daoris-browser</c> each time it starts. 🔴 A TWIN file: the CLI's <c>browser.ts</c> reads and edits
/// it too, and its <c>browser.test.ts</c> carries the same table. A case changed here is changed there,
/// in the same commit.
/// </summary>
public sealed class BrowserSettingsTests : Bridge
{
    private string File => BrowserSettings.FilePath(Home);

    private void Write(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, json);
    }

    [Fact]
    public void No_settings_file_offers_other_softwares_extensions_as_the_engine_does() =>
        Assert.Equal(new BrowserSettingsRead(ExtensionsSetting.Offer, null), BrowserSettings.Read(Home));

    /// <summary>The extensions setting — the CLI's twin table holds these cases, answer for answer.</summary>
    [Theory]
    [InlineData("\"offer\"", "offer")]
    [InlineData("\"refuse\"", "refuse")]
    [InlineData("\"REFUSE\"", "offer")]
    [InlineData("\"block\"", "offer")]
    [InlineData("true", "offer")]
    [InlineData("null", "offer")]
    public void The_extensions_setting_is_offer_or_refuse_and_anything_else_is_the_default(string value, string expected)
    {
        Write($$"""{ "extensions": {{value}} }""");

        Assert.Equal(new BrowserSettingsRead(expected, null), BrowserSettings.Read(Home));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1]")]
    public void A_settings_file_that_cannot_be_read_shows_the_default_and_is_not_written_over(string content)
    {
        Write(content);

        var read = BrowserSettings.Read(Home);
        Assert.Equal(ExtensionsSetting.Offer, read.Extensions);
        Assert.NotNull(read.Problem);
        Assert.Throws<InvalidOperationException>(() => BrowserSettings.SetExtensions(Home, ExtensionsSetting.Refuse));
        Assert.Equal(content, System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void Setting_the_extensions_keeps_what_it_has_no_field_for()
    {
        Write("""{ "version": 2, "extensions": "offer", "theirs": { "kept": true } }""");

        BrowserSettings.SetExtensions(Home, ExtensionsSetting.Refuse);

        var file = JsonNode.Parse(System.IO.File.ReadAllText(File))!.AsObject();
        Assert.Equal(2, (int)file["version"]!);
        Assert.True((bool)file["theirs"]!["kept"]!);
        Assert.Equal(ExtensionsSetting.Refuse, BrowserSettings.Read(Home).Extensions);
    }

    [Fact]
    public void A_value_that_is_neither_is_refused_and_writes_nothing()
    {
        Assert.Throws<InvalidOperationException>(() => BrowserSettings.SetExtensions(Home, "maybe"));
        Assert.False(System.IO.File.Exists(File));
    }
}
