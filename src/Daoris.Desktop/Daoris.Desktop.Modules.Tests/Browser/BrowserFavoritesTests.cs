using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// BRW5: the in-app browser's favorites, kept in <c>&lt;home&gt;/browser/favorites.json</c>, the
/// person's and never a session's. 🔴 A TWIN file: the CLI's <c>browser.ts</c> reads and edits it too,
/// and its <c>browser.test.ts</c> carries the same tables. A case changed here is changed there, in the
/// same commit (the in-app browser design, §3a).
/// </summary>
public sealed class BrowserFavoritesTests : Bridge
{
    private string File => BrowserFavorites.FilePath(Home);

    private void Write(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, json);
    }

    /// <summary>The address rule — the bar's. The CLI's twin table holds these cases, answer for answer.</summary>
    [Theory]
    [InlineData("site.example/board", "https://site.example/board")]
    [InlineData("  site.example  ", "https://site.example/")]
    [InlineData("HTTPS://Site.Example", "https://site.example/")]
    [InlineData("https://site.example:443/", "https://site.example/")]
    [InlineData("site.example:8443/x", "https://site.example:8443/x")]
    [InlineData("localhost:4200", "http://localhost:4200/")]
    [InlineData("127.0.0.1:5231/popup", "http://127.0.0.1:5231/popup")]
    [InlineData("http://127.0.0.1:5231/popup", "http://127.0.0.1:5231/popup")]
    [InlineData("about:blank", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("mailto:a@b.example", null)]
    [InlineData("file:///C:/secrets.txt", null)]
    [InlineData("https://user:pw@site.example/", null)]
    [InlineData("a b", null)]
    [InlineData("", null)]
    public void An_address_is_kept_in_the_bars_form_or_not_at_all(string typed, string? kept)
    {
        Assert.Equal(kept, BrowserFavorites.Address(typed));
    }

    [Fact]
    public void No_file_is_no_favorites()
    {
        var read = BrowserFavorites.Read(Home);

        Assert.Empty(read.Favorites);
        Assert.Null(read.Problem);
    }

    /// <summary>Kept in the file's order; a title is its own, or else the address's host.</summary>
    [Fact]
    public void Favorites_come_back_in_order_with_a_title_or_the_host()
    {
        Write("""
            { "favorites": [
              { "url": "https://site.example/board", "title": "Board" },
              { "url": "http://localhost:4200/" },
              { "url": "https://other.example/", "title": "   " }
            ] }
            """);

        Assert.Equal(
            [new Favorite("https://site.example/board", "Board"), new Favorite("http://localhost:4200/", "localhost"),
             new Favorite("https://other.example/", "other.example")],
            BrowserFavorites.Read(Home).Favorites);
    }

    /// <summary>A row that is not a web page is skipped by a reader, and nothing else is lost with it.</summary>
    [Fact]
    public void A_row_that_is_not_a_page_is_skipped_by_a_reader()
    {
        Write("""{ "favorites": [ { "url": "javascript:alert(1)" }, "a string", { "title": "no url" }, { "url": "https://site.example/" } ] }""");

        Assert.Equal(["https://site.example/"], BrowserFavorites.Read(Home).Favorites.Select(f => f.Url));
    }

    /// <summary>A file that cannot be read shows none and says why; an editor refuses to write over it.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "favorites": "nope" }""")]
    public void A_file_that_cannot_be_read_shows_none_and_is_not_written_over(string content)
    {
        Write(content);

        var read = BrowserFavorites.Read(Home);
        Assert.Empty(read.Favorites);
        Assert.NotNull(read.Problem);
        Assert.Throws<InvalidOperationException>(() => BrowserFavorites.Add(Home, "site.example", null));
        Assert.Throws<InvalidOperationException>(() => BrowserFavorites.Remove(Home, "site.example"));
        Assert.Equal(content, System.IO.File.ReadAllText(File));
    }

    /// <summary>Adding appends; adding one already kept keeps its place and takes a new title if given.</summary>
    [Fact]
    public void Adding_appends_and_adding_again_keeps_its_place()
    {
        BrowserFavorites.Add(Home, "site.example/board", "Board");
        BrowserFavorites.Add(Home, "localhost:4200", null);
        BrowserFavorites.Add(Home, "https://site.example/board", null);
        BrowserFavorites.Add(Home, "site.example/board", "The board");

        Assert.Equal(
            [new Favorite("https://site.example/board", "The board"), new Favorite("http://localhost:4200/", "localhost")],
            BrowserFavorites.Read(Home).Favorites);
    }

    /// <summary>Not a page is not added, in a sentence.</summary>
    [Fact]
    public void An_address_that_is_not_a_page_is_refused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => BrowserFavorites.Add(Home, "javascript:alert(1)", null));

        Assert.Contains("not a web page", refused.Message);
        Assert.False(System.IO.File.Exists(File));
    }

    /// <summary>Removing is by address, under the same rule; one not kept is no change.</summary>
    [Fact]
    public void Removing_is_by_address_under_the_same_rule()
    {
        BrowserFavorites.Add(Home, "site.example/board", "Board");
        BrowserFavorites.Add(Home, "localhost:4200", null);

        Assert.True(BrowserFavorites.Remove(Home, "HTTPS://SITE.EXAMPLE/board"));
        Assert.False(BrowserFavorites.Remove(Home, "never.example"));
        Assert.Equal(["http://localhost:4200/"], BrowserFavorites.Read(Home).Favorites.Select(f => f.Url));
    }

    /// <summary>An editor keeps what it has no field for — on the file, on each row, and a row it skips.</summary>
    [Fact]
    public void An_editor_keeps_what_it_has_no_field_for()
    {
        Write("""
            { "version": 7, "favorites": [
              { "url": "https://site.example/", "title": "Site", "icon": "star.png" },
              { "url": "javascript:alert(1)", "note": "the CLI wrote this" }
            ] }
            """);

        BrowserFavorites.Add(Home, "localhost:4200", null);

        var file = JsonNode.Parse(System.IO.File.ReadAllText(File))!;
        Assert.Equal(7, file["version"]!.GetValue<int>());
        var rows = file["favorites"]!.AsArray();
        Assert.Equal(3, rows.Count);
        Assert.Equal("star.png", rows[0]!["icon"]!.GetValue<string>());
        Assert.Equal("the CLI wrote this", rows[1]!["note"]!.GetValue<string>());
        Assert.Equal("http://localhost:4200/", rows[2]!["url"]!.GetValue<string>());
    }
}
