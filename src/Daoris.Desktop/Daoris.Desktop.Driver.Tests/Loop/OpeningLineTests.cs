using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>session.opening</c> (OPENSLOW1): a driven session's open up to its agent's spawn, each run stage, each step of its files stage
/// and the hand's own preparation timed in the compact line <c>watch.looked</c> carries, written once at the hand.
/// </summary>
public sealed class OpeningLineTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-opening-" + Guid.NewGuid().ToString("N")[..8]);

    public OpeningLineTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_line_names_the_session_its_door_its_ms_and_each_step_by_name_alone()
    {
        var at = new DateTimeOffset(2026, 11, 16, 7, 10, 0, 123, TimeSpan.Zero);
        using var log = new MachineLog(_home, "desktop", () => at);

        SessionLog.WriteAccount(log, OpeningLine.Of("c69733ec", "claude-code-acp", 52_000, "tree=9800 files=38000 files.deps=26000 hand=6000"));

        Assert.Equal(
            """{"time":"2026-11-16T07:10:00.123Z","source":"desktop","level":"info","event":"session.opening","data":{"session":"c69733ec","door":"claude-code-acp","ms":52000,"stages":"tree=9800 files=38000 files.deps=26000 hand=6000"}}""",
            Assert.Single(StubFile.Lines(Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs)).Single())));
    }

    [Fact]
    public void Said_at_the_hand_it_ends_the_hands_own_step_and_takes_every_step_once()
    {
        var clock = 0L;
        var steps = new LookStages(clock: () => clock += 20);
        using (steps.Begin("tree")) { }
        using (steps.Begin("files")) { }
        var hand = steps.Begin("hand");
        var said = new List<AccountLine>();

        OpeningLine.Say(said.Add, steps, Stopwatch.GetTimestamp(), hand, "s1", "codex-acp");
        hand.Dispose();

        var line = Assert.Single(said);
        Assert.Equal("session.opening", line.Event);
        Assert.Equal("tree=20 files=20 hand=20", line.Data.Single(field => field.Key == "stages").Value);
        Assert.Equal("codex-acp", line.Data.Single(field => field.Key == "door").Value);
        Assert.Null(steps.Take());
    }
}
