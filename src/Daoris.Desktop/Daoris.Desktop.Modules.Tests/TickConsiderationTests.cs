using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// One consideration as the loop's tick hands it to the page (<see cref="DriverLoop.TickConsideration"/>): the quest, its
/// repository, the verdict and the driver's sentence, and since SESSUX1d the session a person's stop holds it by.
/// </summary>
public sealed class TickConsiderationTests
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly QuestView Quest = new("q1", "game", "engine", "Expose a streaming budget", "A body.", "Taken");

    /// <summary>
    /// SESSUX1d (D126 §3.3): the page says a stop's sentence in the reader's language by its verdict, and the sentence
    /// names the session stopped, which the tick now carries as a fact rather than inside the driver's English.
    /// </summary>
    [Fact]
    public void A_quest_held_by_a_stop_names_the_session_that_holds_it()
    {
        var held = new Consideration(Quest, StartVerdict.Stopped, "you stopped session `s1a2b3c4`; Try again carries it on.")
        {
            HeldBy = new PriorSession("s1a2b3c4", "D:/trees/s-1", "stopped"),
        };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held), Wire);

        Assert.Equal("q1", shape.GetProperty("quest").GetString());
        Assert.Equal("engine", shape.GetProperty("repository").GetString());
        Assert.Equal("Stopped", shape.GetProperty("verdict").GetString());
        Assert.Equal("s1a2b3c4", shape.GetProperty("heldBy").GetString());
        // The tree stays here: the page is told whose stop, never where its work is.
        Assert.DoesNotContain("trees", shape.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// SESSUX1i (D126 §4.6): Overview's *What needs you* holds a quest parked on its failed sessions, waiting since its last
    /// session ended, and says why in the reader's language. The tick carries both as facts: how many failed and when the
    /// last one ended. The session and its note stay here.
    /// </summary>
    [Fact]
    public void A_parked_quest_says_how_many_failed_and_since_when()
    {
        var parked = new Consideration(Quest, StartVerdict.Exhausted, "3 session(s) have failed on `#q1` without landing anything.");
        var park = new QuestPark("q1", "engine")
        {
            Session = "s3", Strikes = 3, Note = "You've hit your limit.", Since = new DateTimeOffset(2026, 10, 1, 9, 21, 0, TimeSpan.Zero),
        };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(parked, park), Wire);

        Assert.Equal(3, shape.GetProperty("strikes").GetInt32());
        Assert.Equal(park.Since, shape.GetProperty("since").GetDateTimeOffset());
        Assert.DoesNotContain("limit", shape.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("s3", shape.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>A park the loop has not read the records for yet, and every other verdict, says neither.</summary>
    [Fact]
    public void A_quest_with_no_park_read_says_no_number_and_no_time()
    {
        var parked = new Consideration(Quest, StartVerdict.Exhausted, "parked after 3 failed sessions.");
        var waiting = new Consideration(Quest, StartVerdict.RepositoryBusy, "`engine` is busy.");

        foreach (var shape in new[]
        {
            JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(parked), Wire),
            JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(waiting, new QuestPark("q1", "engine") { Strikes = 3 }), Wire),
        })
        {
            Assert.Equal(JsonValueKind.Null, shape.GetProperty("strikes").ValueKind);
            Assert.Equal(JsonValueKind.Null, shape.GetProperty("since").ValueKind);
        }
    }

    /// <summary>Every other verdict holds by no session, and says none.</summary>
    [Fact]
    public void A_quest_no_stop_holds_names_no_session()
    {
        var parked = new Consideration(Quest, StartVerdict.Exhausted, "parked after 3 failed sessions.");

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(parked), Wire);

        Assert.Equal("Exhausted", shape.GetProperty("verdict").GetString());
        Assert.Equal(JsonValueKind.Null, shape.GetProperty("heldBy").ValueKind);
    }
}
