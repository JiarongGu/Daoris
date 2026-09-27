using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// BRW6: the in-app browser's history, kept in <c>&lt;home&gt;/browser/history.json</c> on this machine,
/// and the address bar's completions from it and the favorites. 🔴 A TWIN file for reading and clearing:
/// the CLI's <c>browser.ts</c> lists and clears it too, and its <c>browser.test.ts</c> carries the same
/// reading table. Recording a visit and completing are the window's alone (the design, §3b).
/// </summary>
public sealed class BrowserHistoryTests : Bridge
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    private string File => BrowserHistory.FilePath(Home);

    private void Write(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, json);
    }

    [Fact]
    public void No_file_is_no_history()
    {
        var read = BrowserHistory.Read(Home);

        Assert.Empty(read.Visits);
        Assert.Null(read.Problem);
    }

    /// <summary>The reading table: most recent first, a title or the host, a row that is not a page skipped.</summary>
    [Fact]
    public void Visits_come_back_most_recent_first_with_a_title_or_the_host()
    {
        Write("""
            { "visits": [
              { "url": "https://site.example/board", "title": "Board", "last": "2026-09-28T09:00:00Z", "count": 3 },
              { "url": "javascript:alert(1)", "last": "2026-09-28T12:00:00Z", "count": 1 },
              { "url": "http://localhost:4200/", "last": "2026-09-28T10:00:00Z" },
              { "url": "https://old.example/", "title": "Old", "last": "not a time", "count": 9 }
            ] }
            """);

        Assert.Equal(
            [
                new Visit("http://localhost:4200/", "localhost", T0.AddHours(1), 1),
                new Visit("https://site.example/board", "Board", T0, 3),
                new Visit("https://old.example/", "Old", DateTimeOffset.MinValue, 9),
            ],
            BrowserHistory.Read(Home).Visits);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "visits": "nope" }""")]
    public void A_file_that_cannot_be_read_shows_none_and_is_not_written_over(string content)
    {
        Write(content);

        Assert.NotNull(BrowserHistory.Read(Home).Problem);
        Assert.Throws<InvalidOperationException>(() => BrowserHistory.Visit(Home, "https://site.example/", "Site", T0));
        Assert.Throws<InvalidOperationException>(() => BrowserHistory.Clear(Home));
        Assert.Equal(content, System.IO.File.ReadAllText(File));
    }

    /// <summary>A visit to a page already there counts it again and moves it forward; a new one is added.</summary>
    [Fact]
    public void A_visit_counts_again_or_is_added()
    {
        BrowserHistory.Visit(Home, "https://site.example/board", "Board", T0);
        BrowserHistory.Visit(Home, "localhost:4200", null, T0.AddMinutes(1));
        BrowserHistory.Visit(Home, "HTTPS://SITE.EXAMPLE/board", "The board", T0.AddMinutes(2));

        Assert.Equal(
            [
                new Visit("https://site.example/board", "The board", T0.AddMinutes(2), 2),
                new Visit("http://localhost:4200/", "localhost", T0.AddMinutes(1), 1),
            ],
            BrowserHistory.Read(Home).Visits);
    }

    /// <summary>Only web pages are history; a blank tab or a script is not a visit.</summary>
    [Fact]
    public void Only_a_web_page_is_a_visit()
    {
        Assert.False(BrowserHistory.Visit(Home, "about:blank", null, T0));
        Assert.False(BrowserHistory.Visit(Home, "javascript:alert(1)", null, T0));
        Assert.False(System.IO.File.Exists(File));
    }

    /// <summary>Bounded: past the most it keeps, the least recent go.</summary>
    [Fact]
    public void History_is_bounded_and_the_least_recent_go()
    {
        for (var n = 0; n < BrowserHistory.Kept + 5; n++)
        {
            BrowserHistory.Visit(Home, $"https://site{n}.example/", null, T0.AddMinutes(n));
        }

        var visits = BrowserHistory.Read(Home).Visits;
        Assert.Equal(BrowserHistory.Kept, visits.Count);
        Assert.Equal($"https://site{BrowserHistory.Kept + 4}.example/", visits[0].Url);
        Assert.DoesNotContain(visits, visit => visit.Url == "https://site0.example/");
    }

    /// <summary>Clearing empties the history and keeps what an editor has no field for.</summary>
    [Fact]
    public void Clearing_empties_the_history_and_keeps_what_it_has_no_field_for()
    {
        Write("""{ "version": 2, "visits": [ { "url": "https://site.example/", "last": "2026-09-28T09:00:00Z" } ] }""");

        BrowserHistory.Clear(Home);

        var file = JsonNode.Parse(System.IO.File.ReadAllText(File))!;
        Assert.Equal(2, file["version"]!.GetValue<int>());
        Assert.Empty(file["visits"]!.AsArray());
        Assert.Empty(BrowserHistory.Read(Home).Visits);
    }

    // The completions: the window's alone.

    private static readonly Favorite[] Favorites =
    [
        new("https://tickets.example/board", "Team board"),
        new("https://docs.example/", "Docs"),
    ];

    private static readonly Visit[] Visits =
    [
        new("https://tickets.example/browse/AR-2185", "AR-2185 notes", T0.AddHours(2), 1),
        new("https://tickets.example/board", "Team board", T0.AddHours(1), 7),
        new("http://localhost:4200/dashboard", "Dashboard", T0.AddHours(3), 12),
        new("https://mail.example/", "Mail", T0, 40),
    ];

    /// <summary>
    /// A favorite first, then history; each once; matched in the address and the title, whatever the
    /// case; a host that starts with what was typed before one that only contains it.
    /// </summary>
    [Fact]
    public void Completions_put_a_favorite_first_then_history_each_once()
    {
        var found = BrowserHistory.Suggest("tick", Favorites, Visits, limit: 8);

        Assert.Equal(
            [("https://tickets.example/board", true), ("https://tickets.example/browse/AR-2185", false)],
            found.Select(s => (s.Url, s.Favorite)));
    }

    [Fact]
    public void Completions_match_a_title_and_rank_history_by_how_often_then_how_lately()
    {
        Assert.Equal(["http://localhost:4200/dashboard"], BrowserHistory.Suggest("DASH", Favorites, Visits, 8).Select(s => s.Url));
        Assert.Equal(
            ["https://mail.example/", "http://localhost:4200/dashboard", "https://tickets.example/browse/AR-2185"],
            BrowserHistory.Suggest("a", [], Visits.Where(v => v.Url != "https://tickets.example/board").ToArray(), 8)
                .Select(s => s.Url));
    }

    /// <summary>Nothing typed completes nothing; a scheme typed is not a reason to match everything.</summary>
    [Fact]
    public void Nothing_typed_or_a_bare_scheme_completes_nothing()
    {
        Assert.Empty(BrowserHistory.Suggest("   ", Favorites, Visits, 8));
        Assert.Empty(BrowserHistory.Suggest("https://", Favorites, Visits, 8));
        Assert.Equal(["https://docs.example/"], BrowserHistory.Suggest("https://docs", Favorites, Visits, 8).Select(s => s.Url));
    }

    [Fact]
    public void Completions_stop_at_the_limit()
    {
        Assert.Equal(2, BrowserHistory.Suggest("example", Favorites, Visits, limit: 2).Count);
    }
}
