using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FOUNDATION1 l-ii (the foundation design §4.2): the page's <c>SESSION_ENDED</c> keeps its contract, a conversation's end, and is told once
/// however many of this process's paths see the end: the route that ran the conversation and the journal, which tells the ends another
/// process drove.
/// </summary>
public sealed class ConversationEndsTests : DriverModuleBridge
{
    private static JournalBatch Ends(long position, string record, string state, string marks = "chat", string? old = "Working", string codes = "ended.done") =>
        new("store-1", 1, position, [new JournalEntry(position, 1_761_800_000_000, record, "sessions", "state", old, state, false, false, [codes], marks.Split(','), "team", null)]);

    private int Told(string session) =>
        Raised.Count(each => each.Type == DriverLoop.EndedType && JsonSerializerText(each.Payload).Contains($"\"Session\":\"{session}\"", StringComparison.Ordinal));

    private static string JsonSerializerText(object? payload) => System.Text.Json.JsonSerializer.Serialize(payload);

    /// <summary>A conversation another process drove ends: the journal tells the page once, in the shape the route's end has.</summary>
    [Fact]
    public async Task Another_process_s_conversation_end_is_told_once_by_the_journal()
    {
        await PartEndRelay.TellAsync(Bus, Ends(5, "c1", "Completed"));

        Assert.Equal(1, Told("c1"));
        Assert.Equal("""{"Session":"c1","State":"completed"}""", JsonSerializerText(Raised.Single(each => each.Type == DriverLoop.EndedType).Payload));
    }

    /// <summary>A driven part's end is no conversation's: the journal tells it as <c>part.ended</c> alone.</summary>
    [Fact]
    public async Task A_driven_end_is_no_session_ended()
    {
        await PartEndRelay.TellAsync(Bus, Ends(5, "d1", "Failed", marks: "driven", codes: "ended.exit"));

        Assert.Equal(0, Told("d1"));
        Assert.Single(Raised, each => each.Type == DriverLoop.PartEndedType);
    }

    /// <summary>Whichever of the route and the journal sees an end second tells nothing: this process's own end is told once, and at once by the route.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_end_seen_by_the_route_and_the_journal_is_told_once(bool routeFirst)
    {
        if (routeFirst) await DriverLoop.Ended(Bus, "c2", "completed");
        await PartEndRelay.TellAsync(Bus, Ends(6, "c2", "Completed"));
        if (!routeFirst) await DriverLoop.Ended(Bus, "c2", "completed");

        Assert.Equal(1, Told("c2"));
    }

    /// <summary>An end, a reopen and an end are two ends: each told once, though each is seen twice.</summary>
    [Fact]
    public async Task An_end_a_reopen_and_an_end_are_told_as_two()
    {
        await DriverLoop.Ended(Bus, "c3", "failed");
        await PartEndRelay.TellAsync(Bus, Ends(7, "c3", "Failed", codes: "ended.exit"));
        await PartEndRelay.TellAsync(Bus, Ends(8, "c3", "Working", old: "Failed", codes: "ended.done"));
        await DriverLoop.Ended(Bus, "c3", "failed");
        await PartEndRelay.TellAsync(Bus, Ends(9, "c3", "Failed", codes: "ended.exit"));

        Assert.Equal(2, Told("c3"));
    }
}
