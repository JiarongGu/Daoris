using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>browser</c> proposal (HELP10): Daoris's browser's settings, as Settings → Browser and
/// <c>daoris browser</c> set them — which browser, where the page's links open, other software's extensions, a favorite.
/// </summary>
public sealed class HelpBrowserProposalsTests : HelpProposalsFixture
{
    /// <summary>The two files as the Browser screen reads them; an address is a page where it starts `https://`, as a stand-in for the route's reader.</summary>
    private static readonly HelpBrowserFacts Files = new("daoris", "system", "offer", ["https://site.example/board"])
    {
        EdgeFound = true,
        Page = typed => typed.StartsWith("https://", StringComparison.Ordinal) ? typed
            : typed.Contains('.', StringComparison.Ordinal) && !typed.Contains(':', StringComparison.Ordinal) ? $"https://{typed}" : null,
    };

    private static readonly HelpMachineFacts WithFiles = Facts with { Browser = Files };

    private static HelpProposal Browser(string door, string? value, string? address = null, string? title = null) =>
        new HelpProposal("p9", "browser", door, address, null, value, null, "the person asked", "h1", "proposed") { Title = title };

    [Theory]
    [InlineData("use", "edge", null, null, "daoris browser use edge", "your Edge, on a profile of Daoris's")]
    [InlineData("use", "daoris", null, null, "daoris browser use daoris", "Daoris's own browser")]
    [InlineData("links", "daoris", null, null, "daoris browser links daoris", "Links on the page open in Daoris's browser, from the next click.")]
    [InlineData("links", "system", null, null, "daoris browser links system", "Links on the page open in the system's browser, from the next click.")]
    [InlineData("extensions", "refuse", null, null, "daoris browser extensions refuse", "refused from the browser's next start")]
    [InlineData("favorite", "add", "docs.example/guide", null, "daoris browser favorite add https://docs.example/guide", "Keep https://docs.example/guide")]
    [InlineData("favorite", "add", "https://docs.example/guide", "The guide", "daoris browser favorite add https://docs.example/guide --title \"The guide\"", "as “The guide”")]
    [InlineData("favorite", "remove", "site.example/board", null, "daoris browser favorite remove https://site.example/board", "Stop keeping https://site.example/board")]
    public void A_browser_setting_is_planned_as_what_it_changes_and_the_command_that_does_the_same(
        string door, string value, string? address, string? title, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Browser(door, value, address, title), DriverConfig.Empty, WithFiles);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    /// <summary>What the Browser screen's routes refuse, refused here in their words, and what a helper can invent.</summary>
    [Theory]
    [InlineData("use", "firefox", null, "The browser is `daoris` or `edge`, not `firefox`.")]
    [InlineData("links", "tab", null, "Links open in `system` or `daoris`, not `tab`.")]
    [InlineData("extensions", "maybe", null, "The extensions setting is `offer` or `refuse`, not `maybe`.")]
    [InlineData("favorite", "add", "javascript:alert(1)", "`javascript:alert(1)` is not a web page, so it cannot be a favorite.")]
    [InlineData("favorite", "remove", "https://elsewhere.example/", "`https://elsewhere.example/` is not a favorite")]
    [InlineData("favorite", "keep", "https://docs.example/", "a favorite is `add` or `remove`")]
    [InlineData("favorite", "add", null, "a favorite names its address")]
    [InlineData("bookmarks", "on", null, "`bookmarks` is not a setting of Daoris's browser")]
    public void What_the_browser_screens_route_would_refuse_is_refused_in_its_words(string door, string value, string? address, string says)
    {
        var plan = HelpProposals.Plan(Browser(door, value, address), DriverConfig.Empty, WithFiles);

        Assert.Contains(says, plan.Refusal);
    }

    /// <summary>A file the screen could not read is never written over, and the card is refused in the screen's words.</summary>
    [Fact]
    public void A_file_the_browser_screen_could_not_read_refuses_every_change_to_it()
    {
        var unreadable = Facts with { Browser = Files with { Problem = "settings.json is not readable JSON", FavoritesProblem = "favorites.json is not a JSON array" } };

        Assert.Contains("settings.json is not readable JSON. Fix it, or delete it to start from nothing",
            HelpProposals.Plan(Browser("links", "daoris"), DriverConfig.Empty, unreadable).Refusal);
        Assert.Contains("favorites.json is not a JSON array. Fix it",
            HelpProposals.Plan(Browser("favorite", "add", "https://docs.example/"), DriverConfig.Empty, unreadable).Refusal);
    }

    [Fact]
    public void Edge_where_none_is_installed_says_so_before_Apply()
    {
        var plan = HelpProposals.Plan(Browser("use", "edge"), DriverConfig.Empty, Facts with { Browser = Files with { EdgeFound = false } });

        Assert.Null(plan.Refusal);
        Assert.Contains("No Edge is installed on this machine, so choosing it would open nothing.", plan.Describe);
    }

    [Fact]
    public void With_no_reading_of_the_browsers_files_nothing_is_proposed()
    {
        Assert.Contains("Settings → Browser", HelpProposals.Plan(Browser("links", "daoris"), DriverConfig.Empty, Facts).Refusal);
    }

    /// <summary>Every door the kind names is one it plans, so the coverage's doors are ones a proposal can take.</summary>
    [Fact]
    public void Every_door_the_kind_names_is_one_it_plans()
    {
        (string Door, string Value, string? Address)[] samples =
            [("use", "edge", null), ("links", "daoris", null), ("extensions", "offer", null), ("favorite", "add", "https://docs.example/")];

        Assert.Equal(new HelpBrowserProposals().Doors, samples.Select(sample => sample.Door));
        foreach (var (door, value, address) in samples)
        {
            Assert.Null(HelpProposals.Plan(Browser(door, value, address), DriverConfig.Empty, WithFiles).Refusal);
        }
    }

    [Fact]
    public async Task A_browser_setting_is_applied_through_the_browser_screens_own_edit()
    {
        var (applied, doors, later) = await ApplyAsync(Browser("favorite", "add", "docs.example/guide", "The guide"), facts: WithFiles);

        Assert.True(applied.Applied);
        // As proposed: the route reads the address with the same reader the card's page came from.
        Assert.Equal(["BROWSER favorite add docs.example/guide The guide"], doors.Calls);
        Assert.Equal([applied.Told], later);
        Assert.Equal("applied", HelpProposals.Find(_home, "p9")!.State);
    }

    [Fact]
    public async Task A_browser_setting_the_door_refuses_is_settled_refused_in_its_words()
    {
        var (applied, _, _) = await ApplyAsync(Browser("links", "daoris"), new HelpStandInDoors { BrowserRefusal = "settings.json could not be read." }, WithFiles);

        Assert.False(applied.Applied);
        Assert.Contains("settings.json could not be read.", applied.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p9")!.State);
    }

    /// <summary>A favorite's title is read from the file the service's box writes; a file with none reads as none.</summary>
    [Fact]
    public void A_favorites_title_is_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "p9", ["proposed"] = "2026-09-30T10:00:00Z", ["by"] = new JsonObject { ["session"] = "h1" }, ["kind"] = "browser",
            ["door"] = "favorite", ["target"] = "https://docs.example/", ["workspace"] = null, ["value"] = "add", ["sentence"] = null,
            ["title"] = "The guide", ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p9.json"), node.ToJsonString());

        Assert.Equal("The guide", HelpProposals.Find(_home, "p9")!.Title);
        Assert.Null(HelpProposals.Find(_home, File("p10", "setting", "drive", "engine"))!.Title);
    }
}

public sealed partial class HelpStandInDoors
{
    /// <summary>What the browser's door refuses with, as the Browser screen's route would; null to make the change.</summary>
    public string? BrowserRefusal { get; init; }

    public void ChangeBrowser(string setting, string value, string? address, string? title)
    {
        if (BrowserRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"BROWSER {setting} {value} {address} {title}".TrimEnd());
    }
}
