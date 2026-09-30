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
    /// LOOK2b: the rail said *in s-2394e5d9* of a session whose landing had tidied that tree away and whose branch was
    /// gone. Where each session's work is now is answered for the whole rail in one ask, from this machine's own files:
    /// whether the tree it opened is still here, and its landing, standing or a trace (D113). No service is asked and no
    /// git is run, so it answers on a cold start, and a folder Daoris did not open is never looked at.
    /// </summary>
    [Fact]
    public async Task Where_each_sessions_work_is_now_is_answered_from_the_machines_own_files()
    {
        var loop = Loop();
        var trees = new SessionTrees(loop.Home);
        var standing = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-2394e5d9");
        var kept = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-5a1f0c2b");
        var carriedOn = Path.Combine(trees.TreesRoot, "aurora", "game", "s-77e0d9a4");
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(kept, "README.md"), "kept");
        Directory.CreateDirectory(carriedOn);
        File.WriteAllText(Path.Combine(carriedOn, "README.md"), "carried on");
        var at = DateTimeOffset.Parse("2026-10-01T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        trees.Recorded.Record(new LandedBranch("engine", "aurora", "feature/42-streamer", "main", "abc123", "landed1", "42", null, at));
        trees.Recorded.Record(new LandedBranch("game", "aurora", "feature/7-hud", "main", "def456", "carried1", "7", null, at));
        trees.Recorded.Gone("game", ["feature/7-hud"]);
        trees.Recorded.Record(new LandedBranch("engine", "aurora", "feature/9-cache", "main", "fed789", "gone1", "9", null, at));
        trees.Recorded.Gone("engine", ["feature/9-cache"]);
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, "SESSION_WHERE", new
        {
            sessions = new object[]
            {
                new { id = "landed1", tree = standing },
                new { id = "gone1", tree = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-0b3c4d5e") },
                new { id = "carried1", tree = carriedOn },
                new { id = "working1", tree = kept },
                new { id = "rooted1", tree = Path.Combine(Home, "elsewhere", "engine") },
            },
        });
        var rows = answer.GetProperty("sessions").EnumerateArray().ToDictionary(row => row.GetProperty("session").GetString()!);

        // Its tree tidied and its branch standing: where it landed.
        Assert.True(rows["landed1"].GetProperty("treeGone").GetBoolean());
        Assert.Equal("feature/42-streamer", rows["landed1"].GetProperty("landed").GetProperty("branch").GetString());
        Assert.Equal("standing", rows["landed1"].GetProperty("landed").GetProperty("state").GetString());
        // Its tree tidied and its branch gone since: a trace.
        Assert.Equal("gone", rows["gone1"].GetProperty("landed").GetProperty("state").GetString());
        // A tree still here after its branch went: the session carried on in it, as its review reads (D113 §1).
        Assert.False(rows["carried1"].GetProperty("treeGone").GetBoolean());
        Assert.Equal("gone", rows["carried1"].GetProperty("landed").GetProperty("state").GetString());
        // No landing: its tree, still here.
        Assert.False(rows["working1"].GetProperty("treeGone").GetBoolean());
        Assert.Equal(JsonValueKind.Null, rows["working1"].GetProperty("landed").ValueKind);
        // A folder that is no tree of this home's is never looked at, and nothing is said of it.
        Assert.False(rows.ContainsKey("rooted1"));
        // Never a machine path back: the page sent the trees it was answered, and has no use for them again.
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), answer.GetRawText(), StringComparison.OrdinalIgnoreCase);
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
