using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSWATCH1b (D200; the session-watch design §2.1, cases 2, 8 and 10): a quest session the watcher marks quiet is told once, urgent, as
/// Daoris's word under its episode's key; the word is taken off when the episode ends or the run ends; an intake is told nothing.
/// </summary>
public sealed class QuietWordTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "quiet-word", Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionEvents _events;
    private readonly SessionWatch _watch;
    private TimeSpan _now = TimeSpan.FromHours(1);

    public QuietWordTests()
    {
        Directory.CreateDirectory(_home);
        _events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _watch = SessionWatch.Of(_events);
        _watch.Clock = new QuietClock(() => _now, stopsInSleep: true);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private static readonly TimeSpan Pace = TimeSpan.FromSeconds(15);

    private List<QuietStep> Run(TimeSpan span)
    {
        var steps = new List<QuietStep>();
        for (var end = _now + span; _now < end;)
        {
            _now += Pace;
            steps.AddRange(_watch.Look(_ => new JobRead(true, TimeSpan.Zero), Pace));
        }

        return steps;
    }

    private static async Task Settled(Func<bool> done)
    {
        for (var tries = 0; tries < 200 && !done(); tries++) await Task.Delay(10);
    }

    [Fact]
    public async Task SESSWATCH1b_a_quiet_quest_session_is_told_once_urgent_and_the_word_is_taken_off_when_it_is_heard_again()
    {
        var said = new StandInSaid();
        using var run = _watch.Watch("s1", new WatchedRun(SessionWatch.Quest, "q1", "claude-code"));
        using var word = QuietWord.Tell(_watch, "s1", new DrivenInbox(_ => { }), said)!;

        var marked = Run(SessionWatch.DefaultQuiet + Pace);
        Assert.Contains(marked, step => step.Step == SessionWatch.Marked);
        await Settled(() => said.Said.Count > 0);

        var kept = Assert.Single(said.Said);
        Assert.StartsWith("Daoris: nothing came from this session for 10 minutes.", kept.Text);
        Assert.Single(word.Told);

        Run(Pace * 4);
        Assert.Single(said.Said);

        _watch.Heard("s1");
        Run(Pace);
        await Settled(() => said.TakenOff.Count > 0);
        Assert.Equal([kept.Id!], said.TakenOff);
        Assert.Empty(word.Told);
    }

    [Fact]
    public async Task SESSWATCH1b_an_intake_is_marked_and_told_nothing_and_a_run_s_end_takes_its_waiting_word_off()
    {
        var said = new StandInSaid();
        using (var intake = _watch.Watch("i1", new WatchedRun(SessionWatch.Intake, null, "claude-code")))
        using (QuietWord.Tell(_watch, "i1", new DrivenInbox(_ => { }), said))
        {
            Assert.Contains(Run(SessionWatch.DefaultQuiet + Pace), step => step.Step == SessionWatch.Marked);
        }

        Assert.Empty(said.Said);

        var quest = _watch.Watch("s2", new WatchedRun(SessionWatch.Quest, "q2", "claude-code"));
        var word = QuietWord.Tell(_watch, "s2", new DrivenInbox(_ => { }), said)!;
        Run(SessionWatch.DefaultQuiet + Pace);
        await Settled(() => said.Said.Count > 0);
        word.Dispose();
        quest.Dispose();
        Assert.Equal([said.Said.Single().Id!], said.TakenOff);
    }

    [Fact]
    public void SESSWATCH1b_the_words_name_the_call_or_the_work_and_the_key_names_the_episode()
    {
        var step = new QuietStep("s1", SessionWatch.Quest, "claude-code", "A", 12, SessionWatch.Marked, "open", 4) { Call = "npm test" };
        Assert.Equal(
            "Daoris: nothing came from this session for 12 minutes, while your call `npm test` ran. If a command waits on input, a lock or the "
            + "network, stop it and go another way; run anything long in the background and read its output; if something only the person can "
            + "give blocks you, ask with `question_ask`. Daoris has stopped nothing.",
            QuietWord.Words(step));
        Assert.Contains("while your background work `vite`, `tsc` showed no progress", QuietWord.Words(step with { Call = null, Works = ["vite", "tsc"] }));

        Assert.Equal("watch/s1/4", DaorisKeys.Watch("s1", 4));
        Assert.True(DaorisKeys.IsWatch("watch/s1/4"));
        Assert.False(DaorisKeys.IsResume("watch/s1/4"));
        Assert.Null(DaorisKeys.Read("watch/s1/4"));
    }
}
