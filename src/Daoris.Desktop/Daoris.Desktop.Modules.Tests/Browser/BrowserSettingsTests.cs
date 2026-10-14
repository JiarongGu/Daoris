using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// CHR7: Daoris's browser's settings, kept in <c>&lt;home&gt;/browser/settings.json</c> and read by
/// <c>daoris-browser</c> each time it starts — and its <c>links</c> by the page at each click (BRW7).
/// 🔴 A TWIN file: the CLI's <c>browser.ts</c> reads and edits
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
    public void No_settings_file_offers_other_softwares_extensions_as_the_engine_does_uses_Daoris_browser_and_opens_links_in_the_systems() =>
        Assert.Equal(
            new BrowserSettingsRead(ExtensionsSetting.Offer, BrowserChoice.Daoris, LinksSetting.System, null),
            BrowserSettings.Read(Home));

    /// <summary>Which browser (BRW12) — the CLI's twin table holds these cases, answer for answer.</summary>
    [Theory]
    [InlineData("\"daoris\"", "daoris")]
    [InlineData("\"edge\"", "edge")]
    [InlineData("\"Edge\"", "daoris")]
    [InlineData("\"chrome\"", "daoris")]
    [InlineData("1", "daoris")]
    public void The_browser_is_daoris_or_edge_and_anything_else_is_Daoris_own(string value, string expected)
    {
        Write($$"""{ "browser": {{value}}, "extensions": "refuse" }""");

        Assert.Equal(new BrowserSettingsRead(ExtensionsSetting.Refuse, expected, LinksSetting.System, null), BrowserSettings.Read(Home));
    }

    [Fact]
    public void Choosing_the_browser_keeps_the_rest_and_refuses_what_is_neither()
    {
        Write("""{ "extensions": "refuse" }""");

        BrowserSettings.SetBrowser(Home, BrowserChoice.Edge);

        Assert.Equal(new BrowserSettingsRead(ExtensionsSetting.Refuse, BrowserChoice.Edge, LinksSetting.System, null), BrowserSettings.Read(Home));
        Assert.Throws<InvalidOperationException>(() => BrowserSettings.SetBrowser(Home, "firefox"));
    }

    /// <summary>
    /// Where the page's links open (BRW7) — the CLI's twin table holds these cases, answer for answer.
    /// `daoris` is whichever browser the file's `browser` chooses.
    /// </summary>
    [Theory]
    [InlineData("\"system\"", "system")]
    [InlineData("\"daoris\"", "daoris")]
    [InlineData("\"Daoris\"", "system")]
    [InlineData("\"edge\"", "system")]
    [InlineData("true", "system")]
    public void Links_open_in_the_systems_browser_or_Daoris_and_anything_else_is_the_systems(string value, string expected)
    {
        Write($$"""{ "links": {{value}}, "browser": "edge" }""");

        Assert.Equal(new BrowserSettingsRead(ExtensionsSetting.Offer, BrowserChoice.Edge, expected, null), BrowserSettings.Read(Home));
    }

    [Fact]
    public void Setting_the_links_keeps_what_it_has_no_field_for_and_refuses_what_is_neither_or_a_file_it_could_not_read()
    {
        Write("""{ "extensions": "refuse", "theirs": { "kept": true } }""");

        BrowserSettings.SetLinks(Home, LinksSetting.Daoris);

        var file = JsonNode.Parse(System.IO.File.ReadAllText(File))!.AsObject();
        Assert.True((bool)file["theirs"]!["kept"]!);
        Assert.Equal(
            new BrowserSettingsRead(ExtensionsSetting.Refuse, BrowserChoice.Daoris, LinksSetting.Daoris, null),
            BrowserSettings.Read(Home));
        Assert.Throws<InvalidOperationException>(() => BrowserSettings.SetLinks(Home, "chrome"));

        Write("not json");
        Assert.Throws<InvalidOperationException>(() => BrowserSettings.SetLinks(Home, LinksSetting.Daoris));
        Assert.Equal("not json", System.IO.File.ReadAllText(File));
    }

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

        Assert.Equal(new BrowserSettingsRead(expected, BrowserChoice.Daoris, LinksSetting.System, null), BrowserSettings.Read(Home));
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
