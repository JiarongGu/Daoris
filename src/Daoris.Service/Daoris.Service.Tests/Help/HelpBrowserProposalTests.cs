using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>browser</c> kind's writer (HELP10): Daoris's browser's settings, spelled as <c>daoris browser</c> spells them —
/// which browser, where links open, other software's extensions, a favorite with its address and its title. The driver
/// judges it against the files as the desktop read them; here only the shape is checked.
/// </summary>
public sealed class HelpBrowserProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_browser_setting_is_written_with_its_door_and_value_and_a_favorite_with_its_address_and_title()
    {
        var (links, message) = Box().ProposeBrowser(" Links ", "daoris", null, null, "the person wants tickets in Daoris's browser", "h1", Now);
        var (favorite, _) = Box().ProposeBrowser("favorite", "add", "https://docs.example/guide", " The guide ", "the person keeps it", "h1", Now);

        Assert.Contains($"#{links}", message);
        var set = Written(links!);
        Assert.Equal(("browser", "links", "daoris"),
            (set.GetProperty("kind").GetString(), set.GetProperty("door").GetString(), set.GetProperty("value").GetString()));
        Assert.Equal(JsonValueKind.Null, set.GetProperty("target").ValueKind);
        Assert.Equal(JsonValueKind.Null, set.GetProperty("title").ValueKind);
        var kept = Written(favorite!);
        Assert.Equal(("favorite", "add", "https://docs.example/guide", "The guide"),
            (kept.GetProperty("door").GetString(), kept.GetProperty("value").GetString(), kept.GetProperty("target").GetString(),
                kept.GetProperty("title").GetString()));
    }

    /// <summary>The connector's tool names every door the box takes, so the helper is told of each.</summary>
    [Fact]
    public void The_tool_names_every_door()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeBrowser))!;
        var door = method.GetParameters().Single(parameter => parameter.Name == "door")
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        foreach (var name in new[] { "use", "links", "extensions", "favorite" }) Assert.Contains(name, door);
    }

    [Theory]
    [InlineData("bookmarks", "on", "", "", "is not a setting of Daoris's browser")]
    [InlineData("use", "firefox", "", "", "`use` is `daoris` or `edge`")]
    [InlineData("links", "", "", "", "`links` is `system` or `daoris`")]
    [InlineData("extensions", "maybe", "", "", "`extensions` is `offer` or `refuse`")]
    [InlineData("use", "edge", "https://docs.example/", "", "Only a favorite names an address or a title")]
    [InlineData("favorite", "keep", "https://docs.example/", "", "A favorite is `add` or `remove`")]
    [InlineData("favorite", "add", "", "", "A favorite names its address")]
    [InlineData("favorite", "add", "two words", "", "An address is one word")]
    [InlineData("favorite", "remove", "https://docs.example/", "The guide", "Only a favorite added takes a title")]
    public void A_malformed_browser_setting_is_refused_with_nothing_written(string door, string value, string address, string title, string says)
    {
        var (id, message) = Box().ProposeBrowser(door, Named(value), Named(address), Named(title), "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
