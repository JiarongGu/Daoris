using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A session as the rail acts on it over the bridge (`DriverModule.Sessions.cs`, MOD5): a stop, a parked
/// session's resolution, and the openings and search read off this machine's own record.
/// </summary>
public sealed class DriverModuleSessionsTests : DriverModuleBridge
{
    /// <summary>
    /// RAIL1: what a person first said in each session, and a search of what sessions said, answered from
    /// this machine's own record — no service is asked, because none holds it (D47 §4), so both answer on
    /// a cold start too.
    /// </summary>
    [Fact]
    public async Task Openings_and_a_search_are_answered_from_the_machines_own_record()
    {
        var loop = Loop();
        loop.Events.Append("chat1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Cap the hydration per frame" });
        loop.Events.Append("chat1", new SessionEvent { Kind = SessionEventKind.Message, Text = "Capped in the streamer." });
        var module = new DriverModule(Bus, loop);

        var openings = await AnswerAsync(module, "SESSION_OPENINGS", new { ids = new[] { "chat1", "none1" } });
        Assert.Equal("Cap the hydration per frame", openings.GetProperty("openings").GetProperty("chat1").GetString());
        Assert.False(openings.GetProperty("openings").TryGetProperty("none1", out _));

        var search = await AnswerAsync(module, "SESSION_SEARCH", new { q = "streamer" });
        var hit = Assert.Single(search.GetProperty("hits").EnumerateArray());
        Assert.Equal("chat1", hit.GetProperty("session").GetString());
        Assert.Equal("message", hit.GetProperty("kind").GetString());
        Assert.Contains("streamer", hit.GetProperty("snippet").GetString());
        Assert.False(search.GetProperty("cut").GetBoolean());

        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(module, "SESSION_SEARCH", new { }));
    }

    /// <summary>
    /// Stopping a session that has already finished is FALSE, not an error: the record says how it
    /// ended, and a page that showed a failure would be reporting the race rather than the outcome.
    /// </summary>
    [Fact]
    public async Task Stopping_a_session_that_is_not_running_answers_false()
    {
        var state = await AnswerAsync(Module(), "STOP_SESSION", new { id = "nothing-here" });

        Assert.False(state.GetProperty("stopped").GetBoolean());
        // Nor an orphan: with no service up there is no record to have ended.
        Assert.False(state.GetProperty("orphan").GetBoolean());
        // Nor run by another process here: nothing marked it (REV3 chat F8).
        Assert.False(state.GetProperty("elsewhere").GetBoolean());
    }

    /// <summary>
    /// The person's three moves on a parked session (design §4) — and the fourth the ledger allows
    /// is NOT one of them. Narrowed on this side because it is a surface rule: `awaiting-person` →
    /// `working` is the driver's observation of a session that carried on, which a person causes by
    /// answering it, not by pressing anything.
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData("failed")]
    [InlineData("queued")]
    public async Task A_move_that_is_not_the_persons_is_refused_before_the_service_is_asked(string state)
    {
        var refusal = await RefusalAsync(Module(), "RESOLVE_SESSION", new { id = "s1a2b3c4", state });

        Assert.Contains(Refusals.SessionMoveNotYours, refusal);
        // The state is carried as a PARAMETER, so the sentence the person reads can name it.
        Assert.Contains($"state={state}", refusal);
    }

    /// <summary>
    /// The same rule the quest door holds, for the same reason: the note is the part whoever reads
    /// the record can act on. Refused before the service is asked, so a reasonless decline never
    /// half-happens.
    /// </summary>
    [Fact]
    public async Task Declining_a_parked_session_needs_a_reason()
    {
        var refusal = await RefusalAsync(
            Module(), "RESOLVE_SESSION", new { id = "s1a2b3c4", state = "declined" });

        Assert.Contains(Refusals.SessionDeclineNeedsReason, refusal);
    }

    /// <summary>
    /// A move the person MAY make still needs somewhere to record it. On a cold start that is the
    /// same sentence every other service-needing control gives, rather than a crash.
    /// </summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("stopped")]
    public async Task A_persons_move_before_the_service_answers_says_so(string state)
    {
        var refusal = await RefusalAsync(
            Module(), "RESOLVE_SESSION", new { id = "s1a2b3c4", state, note = "looked at it; it is right." });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }
}
