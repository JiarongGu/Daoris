using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// An agent's ask to hand its session over, over the bridge (MIXAGENT1c, D163 point 3; the mixed-agents design §2, §5): the session's
/// page reads the ask with where the driver's look left it (its note the agent's claim, the agent it would go to), and the person's
/// yes and no to one that waits on them under the `handOver` rule's `ask`, by the judge the terminal's verb shares.
/// </summary>
public sealed class DriverModuleHandOverAskTests : DriverModuleBridge
{
    private HandOverAskFiles Asked(string session = "c1", string? state = null, string? code = null, string id = "a1b2c3d4")
    {
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Sessions, session + HandOverAskFiles.AskSuffix),
            $$"""{"id":"{{id}}","session":"{{session}}","note":"Near my limit; ParserTests are left.","askedBy":"agent","at":"2026-10-20T09:00:00Z"}""");
        var files = new HandOverAskFiles(Home);
        if (state is not null) files.WriteState(session, new HandOverAskState(id, state, code, "codex-acp", DateTimeOffset.Parse("2026-10-20T09:01:00Z")));
        return files;
    }

    /// <summary>The session's ask, as the look left it: its note, when, its state and code and the agent it goes to; none where it asked none.</summary>
    [Fact]
    public async Task A_sessions_ask_is_read_with_where_the_look_left_it()
    {
        Asked(state: HandOverAskStates.Waiting);

        var ask = (await AnswerAsync(Module(), "SESSION_HAND_OVER_ASK", new { id = "c1" })).GetProperty("ask");
        var none = (await AnswerAsync(Module(), "SESSION_HAND_OVER_ASK", new { id = "c2" })).GetProperty("ask");

        Assert.Equal(("a1b2c3d4", "Near my limit; ParserTests are left.", "waiting", "codex-acp"),
            (ask.GetProperty("id").GetString(), ask.GetProperty("note").GetString(), ask.GetProperty("state").GetString(), ask.GetProperty("agent").GetString()));
        Assert.Equal(JsonValueKind.Null, ask.GetProperty("code").ValueKind);
        Assert.True(DateTimeOffset.TryParse(ask.GetProperty("at").GetString(), out _));
        Assert.Equal(JsonValueKind.Null, none.ValueKind);
    }

    /// <summary>An ask the look has not read yet, and one it refused, are read as they stand: the page words the state, never guesses it.</summary>
    [Fact]
    public async Task An_unread_or_refused_ask_reads_as_it_stands()
    {
        Asked();
        var unread = (await AnswerAsync(Module(), "SESSION_HAND_OVER_ASK", new { id = "c1" })).GetProperty("ask");
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (unread.GetProperty("state").ValueKind, unread.GetProperty("agent").ValueKind));

        Asked(state: HandOverAskStates.Refused, code: HandOverAskWhy.Off);
        var refused = (await AnswerAsync(Module(), "SESSION_HAND_OVER_ASK", new { id = "c1" })).GetProperty("ask");
        Assert.Equal(("refused", "off"), (refused.GetProperty("state").GetString(), refused.GetProperty("code").GetString()));
    }

    /// <summary>The person's yes: the ask waiting on it goes, `approved` with the agent the look named, for the look to take; again, the same yes.</summary>
    [Fact]
    public async Task The_persons_yes_lets_a_waiting_ask_go_to_the_agent_named()
    {
        var files = Asked(state: HandOverAskStates.Waiting);

        var yes = await AnswerAsync(Module(), "SESSION_HAND_OVER_APPROVE", new { session = "c1", ask = "a1b2c3d4" });
        var again = await AnswerAsync(Module(), "SESSION_HAND_OVER_APPROVE", new { session = "c1" });

        Assert.True(yes.GetProperty("approved").GetBoolean());
        Assert.Equal("codex-acp", yes.GetProperty("agent").GetString());
        Assert.True(again.GetProperty("approved").GetBoolean());
        Assert.Equal((HandOverAskStates.Approved, "codex-acp"), (files.ReadState("c1")!.State, files.ReadState("c1")!.Agent));
    }

    /// <summary>The person's no: the ask refused `declined` with the note the look writes for every refusal, on the session's record.</summary>
    [Fact]
    public async Task The_persons_no_refuses_a_waiting_ask_with_its_note()
    {
        var files = Asked(state: HandOverAskStates.Waiting);

        var no = await AnswerAsync(Module(), "SESSION_HAND_OVER_DECLINE", new { session = "c1", ask = "a1b2c3d4" });

        Assert.True(no.GetProperty("declined").GetBoolean());
        Assert.Equal((HandOverAskStates.Refused, HandOverAskWhy.Declined), (files.ReadState("c1")!.State, files.ReadState("c1")!.Code));
        var note = Assert.Single(new SessionEvents(DaorisHome.Entry(Home, HomeEntry.Sessions)).After("c1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal("handover.refused", note.Parts![0].Code);
    }

    /// <summary>What waits on no word of theirs is answered by its code, the state untouched: gone, a later ask, past it, not read yet, a yes given.</summary>
    [Fact]
    public async Task What_waits_on_no_word_is_refused_by_code_and_changes_nothing()
    {
        Asked(state: HandOverAskStates.Taken);
        var module = Module();

        Assert.Equal("gone", (await AnswerAsync(module, "SESSION_HAND_OVER_APPROVE", new { session = "c9" })).GetProperty("why").GetString());
        Assert.Equal("gone", (await AnswerAsync(module, "SESSION_HAND_OVER_APPROVE", new { session = "c1", ask = "e0e0e0e0" })).GetProperty("why").GetString());
        Assert.Equal("past", (await AnswerAsync(module, "SESSION_HAND_OVER_APPROVE", new { session = "c1" })).GetProperty("why").GetString());

        // A later ask of the same session, which the look has not read: the state on file names the earlier one.
        Asked(id: "b0b0b0b0");
        Assert.Equal("unread", (await AnswerAsync(module, "SESSION_HAND_OVER_DECLINE", new { session = "c1" })).GetProperty("why").GetString());
        Asked(state: HandOverAskStates.Approved);
        Assert.Equal("approved", (await AnswerAsync(module, "SESSION_HAND_OVER_DECLINE", new { session = "c1" })).GetProperty("why").GetString());
        Assert.Equal(HandOverAskStates.Approved, new HandOverAskFiles(Home).ReadState("c1")!.State);
    }
}
