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
