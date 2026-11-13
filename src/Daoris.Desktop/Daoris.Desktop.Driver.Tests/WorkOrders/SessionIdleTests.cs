using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation whose turn ended reads idle, never working: its record stays <c>working</c> while its process waits for the
/// person's next word, so the reader says what the look knows of its turns (<see cref="SessionLook.Between"/>), in the list, the
/// terminal and the page alike; one mid-turn still reads working.
/// </summary>
public sealed class SessionIdleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 19, 53, 0, TimeSpan.Zero);

    private static JsonObject Chat(string id) => new()
    {
        ["id"] = id, ["repository"] = "ask-daoris", ["adapter"] = "claude-code", ["state"] = "working", ["kind"] = "chat",
        ["created"] = T0.ToString("O"), ["updated"] = T0.AddMinutes(1).ToString("O"),
    };

    [Fact]
    public void A_conversation_between_turns_reads_idle_and_one_mid_turn_reads_working()
    {
        var look = SessionLook.From(new JsonArray(Chat("rest"), Chat("turn")).ToJsonString(), [], [], _ => 0)
            with { Between = new HashSet<string>(["rest"], StringComparer.Ordinal) };
        var reader = new SessionStateReader(look);

        var rest = reader.Place(look.Records.Single(record => record.Id == "rest"));
        var turn = reader.Place(look.Records.Single(record => record.Id == "turn"));

        Assert.Equal((ShownState.Idle, SessionGroup.Working, ShownState.Idle), (rest.Shown, rest.State, rest.Reason));
        Assert.Equal(("working", SessionGroup.Working), (turn.Shown, turn.State));
        Assert.Equal("idle", SessionsListing.Word(rest.Shown));
    }
}
