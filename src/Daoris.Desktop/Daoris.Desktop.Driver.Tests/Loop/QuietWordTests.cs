using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest session the watcher marks quiet is told once, urgent, as
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
        // The row says it is told, for as long as the episode lasts.
        await Settled(() => _watch.QuietNow().TryGetValue("s1", out var mark) && mark.Told);
        Assert.True(_watch.QuietNow()["s1"].Told);

        Run(Pace * 4);
        Assert.Single(said.Said);

        _watch.Heard("s1");
        Run(Pace);
        await Settled(() => said.TakenOff.Count > 0);
        Assert.Equal([kept.Id!], said.TakenOff);
        Assert.Empty(word.Told);
    }

    /// <summary>An episode that ends while its word is being kept has the word taken off where the keep returns, so no turn after the quiet gets it.</summary>
    [Fact]
    public async Task An_episode_that_ends_while_its_word_is_being_kept_has_the_word_taken_off_when_the_keep_returns()
    {
        var release = new TaskCompletionSource();
        var said = new StandInSaid { Answering = _ => release.Task };
        using var run = _watch.Watch("s1", new WatchedRun(SessionWatch.Quest, "q1", "claude-code"));
        using var word = QuietWord.Tell(_watch, "s1", new DrivenInbox(_ => { }), said)!;

        Run(SessionWatch.DefaultQuiet + Pace);
        await Settled(() => said.Said.Count > 0);
        var kept = said.Said.Single().Id!;
        _watch.Heard("s1");
        Run(Pace);
        release.SetResult();

        await Settled(() => said.TakenOff.Count > 0);
        Assert.Equal([kept], said.TakenOff);
        Assert.Empty(said.Said);
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
        var kept = said.Said.Single().Id!;
        word.Dispose();
        quest.Dispose();
        Assert.Equal([kept], said.TakenOff);
    }

    /// <summary>
    /// Case 10, on a door that takes words at a turn's end: the word is judged where it leaves for the wire, and stands only while its
    /// episode does; a word taken for the event log is marked the watcher's, which no answers-alone count resets by.
    /// </summary>
    [Fact]
    public async Task SESSWATCH1b_the_word_goes_to_no_turn_once_its_episode_or_run_ended_and_is_marked_the_watchers()
    {
        var said = new StandInSaid();
        var inbox = new DrivenInbox(_ => { });
        using var run = _watch.Watch("s1", new WatchedRun(SessionWatch.Quest, "q1", "codex"));
        var word = QuietWord.Tell(_watch, "s1", inbox, said)!;

        Run(SessionWatch.DefaultQuiet + Pace);
        await Settled(() => said.Said.Count > 0);
        var held = inbox.TakeAllOrClose()!.Single();

        Assert.True(DaorisKeys.IsWatch(held.Daoris!.Key));
        Assert.True(await held.Daoris.Stands!(CancellationToken.None));
        Assert.True(ChatMessage.Words(held).Watcher);
        Assert.Null(ChatMessage.Words(new ChatMessage("ok", [])).Watcher);

        _watch.Heard("s1");
        Run(Pace);
        await Settled(() => said.TakenOff.Count > 0);
        Assert.False(await held.Daoris.Stands!(CancellationToken.None));
        word.Dispose();
    }

    /// <summary>Case 10: a watch word found on an ended record is no word waiting, so no look goes on with it and no carry-on resumes from it.</summary>
    [Fact]
    public void SESSWATCH1b_a_watch_word_left_on_an_ended_record_is_no_word_that_waits()
    {
        var watch = new SaidWordView("said-1", "Daoris: nothing came.", DateTimeOffset.UnixEpoch, [], false) { Key = DaorisKeys.Watch("s1", 1), From = DaorisWords.From };
        var person = new SaidWordView("said-2", "Go on.", DateTimeOffset.UnixEpoch, [], false);
        var ended = new PriorSession("s1", null, "failed");

        Assert.False((ended with { Said = [watch] }).WordsWaiting);
        Assert.Empty((ended with { Said = [watch] }).Waiting);
        Assert.Equal(["said-2"], (ended with { Said = [watch, person] }).Waiting.Select(word => word.Id));
        Assert.True((ended with { Said = [watch, person] }).WordsWaiting);
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
