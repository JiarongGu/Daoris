using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// What an Ask Daoris conversation's presses published, kept with it by id. The ids are
/// the service's answer, never read from a sentence; the list survives a restart, since it is a file beside the conversation's other
/// files, and goes with them at a delete. Files only, so this is the suite's fast half.
/// </summary>
public sealed class HelpPublishedTests : HelpProposalsFixture
{
    /// <summary>One answer settles a press, so a second entry for it is never kept.</summary>
    [Fact]
    public void A_press_kept_already_keeps_its_first_entry()
    {
        var kept = new HelpPublished(_home);
        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "press", "work") { Ask = "a1" });
        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T10:00:00Z"), "p1", "press", "work") { Ask = "a9" });

        Assert.Equal(["a1"], kept.Read("h1").Select(entry => entry.Ask));
    }

    /// <summary>Kept on disk: a new reader over the same home, as after a restart, reads every entry, the newest first.</summary>
    [Fact]
    public void The_list_survives_a_restart_and_reads_newest_first()
    {
        var first = new HelpPublished(_home);
        first.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });
        first.Keep("h1", new HelpPublishedEntry(At("2026-10-22T10:00:00Z"), "p2", "setup", "work") { Ask = "a2", Quest = "q2" });
        first.Keep("h2", new HelpPublishedEntry(At("2026-10-22T11:00:00Z"), "p3", "ask", "default") { Quest = "q3" });

        var again = new HelpPublished(_home);

        Assert.Equal(["p2", "p1"], again.Read("h1").Select(entry => entry.Proposal));
        Assert.Equal(("a2", "q2"), (again.Read("h1")[0].Ask, again.Read("h1")[0].Quest));
        Assert.Null(again.Read("h1")[1].Quest);
        Assert.Equal(["q3"], again.Read("h2").Select(entry => entry.Quest));
    }

    /// <summary>The file beside the conversation's own: one object, its entries oldest first, an absent id left out, as LF UTF-8.</summary>
    [Fact]
    public void The_file_is_kept_beside_the_conversation_as_the_home_writes_its_files()
    {
        var kept = new HelpPublished(_home);
        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });

        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Sessions, "h1.published.json"), kept.PathOf("h1"));
        var bytes = System.IO.File.ReadAllBytes(kept.PathOf("h1"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        using var document = JsonDocument.Parse(bytes);
        var entry = Assert.Single(document.RootElement.GetProperty("published").EnumerateArray());
        Assert.Equal("a1", entry.GetProperty("ask").GetString());
        Assert.False(entry.TryGetProperty("quest", out _));
        Assert.Equal("p1", entry.GetProperty("proposal").GetString());
    }

    /// <summary>A file that does not read, a conversation with none and an id that is no session's are nothing kept; a keep starts it again.</summary>
    [Fact]
    public void A_file_that_does_not_read_is_nothing_kept()
    {
        var kept = new HelpPublished(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(kept.PathOf("h1"))!);
        System.IO.File.WriteAllText(kept.PathOf("h1"), "{ not json");

        Assert.Empty(kept.Read("h1"));
        Assert.Empty(kept.Read("h9"));
        Assert.Empty(kept.Read("../escape"));
        Assert.Throws<DriverException>(() => kept.Keep("../escape", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" }));

        kept.Keep("h1", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });
        Assert.Single(kept.Read("h1"));
    }

    /// <summary>A delete takes the list with the conversation's other files, under Ask Daoris's own name (<see cref="SessionHomeFiles"/>).</summary>
    [Fact]
    public void A_delete_removes_the_list()
    {
        var kept = new HelpPublished(_home);
        kept.Keep("help0005", new HelpPublishedEntry(At("2026-10-22T09:00:00Z"), "p1", "ask", "work") { Ask = "a1" });
        Assert.True(System.IO.File.Exists(kept.PathOf("help0005")));

        var removed = new SessionHomeFiles(_home).Remove(["help0005"], events: null);

        Assert.False(System.IO.File.Exists(kept.PathOf("help0005")));
        Assert.Contains(SessionHomeFiles.Help, removed.Went["help0005"]);
    }

    private static DateTimeOffset At(string when) => DateTimeOffset.Parse(when, System.Globalization.CultureInfo.InvariantCulture);
}
