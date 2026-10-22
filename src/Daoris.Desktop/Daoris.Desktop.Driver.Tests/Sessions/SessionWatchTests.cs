using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSWATCH1a's watcher (D200, the session-watch design §1, §2, §7), on a clock the test moves and a job CPU it hands: a session heard is
/// left alone however long it runs; one that says nothing and runs nothing is marked at Q and brought to the person at 3Q; a busy one
/// that says nothing is brought at 3Q as busy, offered no end; busy is read by window, never from past work; a held permission request
/// is the person's wait; the clock that stops in sleep counts no sleep, and off it a gap is not counted; and nothing is ended but under a
/// headless <c>--quiet-end</c>. No test waits a bound out on the machine's clock (the held-clocks knowledge).
/// </summary>
public sealed class SessionWatchTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-watch", Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionEvents _events;
    private readonly SessionWatch _watch;
    private TimeSpan _now = TimeSpan.FromHours(1);
    private readonly Dictionary<string, JobRead?> _jobs = new(StringComparer.Ordinal);
    private int _jobReads;

    public SessionWatchTests()
    {
        Directory.CreateDirectory(_home);
        _events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _watch = SessionWatch.Of(_events);
        _watch.Clock = new QuietClock(() => _now, stopsInSleep: true);
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private static readonly TimeSpan Pace = TimeSpan.FromSeconds(15);

    private JobRead? Job(string session)
    {
        _jobReads++;
        return _jobs.TryGetValue(session, out var read) ? read : new JobRead(true, TimeSpan.Zero);
    }

    /// <summary>Looks at the pace until <paramref name="span"/> has passed, the steps of every look in order.</summary>
    private List<QuietStep> Run(TimeSpan span, TimeSpan? pace = null, Action? each = null)
    {
        var steps = new List<QuietStep>();
        var end = _now + span;
        while (_now < end)
        {
            _now += pace ?? Pace;
            each?.Invoke();
            steps.AddRange(_watch.Look(Job, pace ?? Pace));
        }

        return steps;
    }

    private SessionWatch.WatchHandle Quest(string session = "s1") => _watch.Watch(session, new WatchedRun(SessionWatch.Quest, "q1", "claude-code"));

    // ——— Case 1: streaming is left alone however long it runs.

    [Fact]
    public void A_session_heard_every_few_seconds_is_left_alone_for_three_hours()
    {
        using var run = Quest();
        var heard = 0;

        var steps = Run(TimeSpan.FromHours(3), pace: TimeSpan.FromSeconds(5), each: () =>
        {
            _watch.Heard("s1");
            heard++;
        });

        Assert.Empty(steps);
        Assert.Null(_watch.RowOf("s1"));
        Assert.Null(_watch.QuietSince("s1"));
        Assert.True(heard > 2000);
        // Heard within half its bound, its job was never read.
        Assert.Equal(0, _jobReads);
    }

    /// <summary>A capture's stream stamps its session heard at each line it reads, a stderr line and a subagent's among them.</summary>
    [Fact]
    public void Each_line_a_capture_reads_stamps_its_session_heard()
    {
        using var run = Quest();
        _now += TimeSpan.FromMinutes(9);
        using var reader = _watch.Hearing(new StringReader("one\ntwo\n"), "s1");

        Assert.Equal("one", reader.ReadLine());
        _now += TimeSpan.FromMinutes(9);

        // Eighteen minutes since it started, nine since the line: not quiet.
        Assert.Empty(_watch.Look(Job, Pace));
        Assert.Null(_watch.QuietSince("s1"));
    }

    // ——— Case 2 without the word: a silent session is marked at Q and brought to the person at 3Q.

    [Fact]
    public void A_silent_idle_session_is_marked_at_its_bound_and_brought_to_the_person_at_three_times_it()
    {
        using var run = Quest();

        var steps = Run(TimeSpan.FromMinutes(31));

        Assert.Equal([SessionWatch.Marked, SessionWatch.Brought], steps.Select(step => step.Step));
        Assert.Equal([10, 30], steps.Select(step => step.Minutes));
        var row = _watch.RowOf("s1")!;
        Assert.False(row.Busy);
        Assert.Equal(("q1", SessionWatch.Quest), (row.Quest, row.Kind));
        Assert.NotNull(_watch.QuietSince("s1"));

        // The mark's coded note is on the record.
        var notes = _events.Page("s1").Events.Where(e => e.Kind == SessionEventKind.Note).ToList();
        Assert.Equal("watch.quiet", Assert.Single(Assert.Single(notes).Parts!).Code);

        // Heard again: the episode is over, said, and the mark and the row go.
        _watch.Heard("s1");
        var over = Run(Pace);
        Assert.Equal(("over", "heard"), (Assert.Single(over).Step, over[0].Outcome));
        Assert.Null(_watch.RowOf("s1"));
        Assert.Null(_watch.QuietSince("s1"));
    }

    /// <summary>
    /// §5.1's <c>{{what}}</c>: the mark's note names the call its agent was inside as it went quiet, or in state C the background work
    /// that showed nothing, each as a coded note of its own.
    /// </summary>
    [Fact]
    public void The_mark_names_the_call_it_was_inside_or_the_work_it_waited_on()
    {
        using var run = Quest();
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c1", Title = "npm test", Status = "in_progress", At = DateTimeOffset.UtcNow });
        Run(TimeSpan.FromMinutes(11));

        var call = _events.Page("s1").Events.Last(e => e.Kind == SessionEventKind.Note).Parts!.Single();
        Assert.Equal(("watch.quiet-call", "npm test"), (call.Code, call.Values.Single(value => value.Key == "call").Value.ToString()));

        using var waiting = _watch.Watch("s2", new WatchedRun(SessionWatch.Quest, "q2", "claude-code-acp"));
        _watch.OwnWork("s2", () => true, () => ["Run the tests"]);
        Run(TimeSpan.FromMinutes(31));

        var work = _events.Page("s2").Events.Last(e => e.Kind == SessionEventKind.Note).Parts!.Single();
        Assert.Equal("watch.quiet-work", work.Code);
        Assert.Contains("Run the tests", work.Text, StringComparison.Ordinal);
    }

    // ——— Case 4: busy is a sign of work, read by window.

    [Fact]
    public void A_busy_silent_session_is_never_marked_and_is_brought_at_three_times_its_bound_as_busy()
    {
        using var run = Quest();
        var cpu = TimeSpan.Zero;

        var steps = Run(TimeSpan.FromMinutes(31), each: () =>
        {
            cpu += TimeSpan.FromSeconds(10);
            _jobs["s1"] = new JobRead(true, cpu);
        });

        Assert.Equal([SessionWatch.Brought], steps.Select(step => step.Step));
        Assert.True(_watch.RowOf("s1")!.Busy);
        Assert.Null(_watch.QuietSince("s1"));
    }

    [Fact]
    public void A_session_that_computed_then_hung_reads_idle_within_a_minute_of_hanging_its_past_work_never_counted()
    {
        using var run = Quest();
        var cpu = TimeSpan.Zero;
        var hungAt = _now + TimeSpan.FromMinutes(12);

        var steps = Run(TimeSpan.FromMinutes(14), each: () =>
        {
            if (_now < hungAt) cpu += TimeSpan.FromSeconds(10);
            _jobs["s1"] = new JobRead(true, cpu);
        });

        var marked = Assert.Single(steps, step => step.Step == SessionWatch.Marked);
        Assert.InRange(marked.Minutes, 12, 13);
    }

    [Fact]
    public void A_session_whose_job_could_not_be_joined_is_busy_unknown_marked_and_brought_and_never_ended()
    {
        using var run = Quest();
        _jobs["s1"] = new JobRead(false, null);

        var steps = Run(TimeSpan.FromMinutes(31));

        Assert.Equal([SessionWatch.Marked, SessionWatch.Brought], steps.Select(step => step.Step));
        Assert.False(_watch.RowOf("s1")!.Busy);
        Assert.False(run.Ending.IsCancellationRequested);
    }

    // ——— Case 5: a held permission request is the person's wait.

    [Fact]
    public void A_session_whose_call_waits_on_the_persons_answer_is_never_quiet()
    {
        using var run = Quest();
        _ = RunningCalls.Of(_events);
        var holding = HeldPermissions.Of(_events).For("s1", TimeSpan.FromHours(2))
            .Hold("900", "c1", "Run npm start", [new("yes", "Allow", PermissionChoice.AllowOnce)])!;

        Assert.Empty(Run(TimeSpan.FromMinutes(45)));

        Assert.True(HeldPermissions.Of(_events).Answer("s1", "900", "yes").Answered);
        Assert.True(holding.IsCompleted);
    }

    /// <summary>
    /// Case 5's second half (§1.3): a call waiting for its repository's heavy-run slot behind another run is a wait, never marked, and at
    /// three times its bound it is brought to the person all the same, naming the holder: a terminal's run has no session to help.
    /// </summary>
    [Fact]
    public void A_session_waiting_for_the_heavy_slot_is_a_wait_until_three_times_its_bound_then_brought_naming_the_holder()
    {
        using var run = Quest();
        var asked = 0;
        HeavyWait? Waits(string session, string? tree)
        {
            asked++;
            return session == "s1" ? new HeavyWait("s9") : null;
        }

        var steps = new List<QuietStep>();
        for (var at = 0; at < 124; at++)
        {
            _now += Pace;
            steps.AddRange(_watch.Look(Job, Pace, Waits));
        }

        Assert.Equal([SessionWatch.Brought], steps.Select(step => step.Step));
        var row = _watch.RowOf("s1")!;
        Assert.Equal(("s9", false), (row.Heavy, row.Busy));
        Assert.Null(_watch.QuietSince("s1"));
        // Read only once it had gone unheard for half its bound.
        Assert.InRange(asked, 100, 124 - 19);
    }

    // ——— Case 11: the clock.

    [Fact]
    public void A_sleep_of_the_machine_counts_no_quiet_on_the_clock_that_stops_in_sleep()
    {
        using var run = Quest();
        Run(TimeSpan.FromMinutes(5));

        // The machine sleeps eight hours: the unbiased clock does not move, so the next look reads five minutes, not eight hours.
        Assert.Empty(_watch.Look(Job, Pace));
        Assert.Empty(Run(TimeSpan.FromMinutes(4)));
    }

    [Fact]
    public void Looks_three_minutes_apart_still_count_quiet()
    {
        using var run = Quest();

        var steps = Run(TimeSpan.FromMinutes(33), pace: TimeSpan.FromSeconds(180));

        Assert.Equal([SessionWatch.Marked, SessionWatch.Brought], steps.Select(step => step.Step));
    }

    [Fact]
    public void Off_a_clock_that_stops_in_sleep_a_long_gap_between_looks_is_not_counted()
    {
        _watch.Clock = new QuietClock(() => _now, stopsInSleep: false);
        using var run = Quest();
        Run(TimeSpan.FromMinutes(2));

        _now += TimeSpan.FromHours(8);

        Assert.Empty(_watch.Look(Job, Pace));
        var steps = Run(TimeSpan.FromMinutes(9));
        Assert.Equal(SessionWatch.Marked, Assert.Single(steps).Step);
    }

    // ——— Case 13: an intake.

    [Fact]
    public void An_intake_is_marked_and_brought_as_an_intake()
    {
        using var run = _watch.Watch("i1", new WatchedRun(SessionWatch.Intake, null, "claude-code"));

        Run(TimeSpan.FromMinutes(31));

        var row = _watch.RowOf("i1")!;
        Assert.Equal((SessionWatch.Intake, (string?)null), (row.Kind, row.Quest));
    }

    // ——— The person's Leave it running.

    [Fact]
    public void Left_running_the_row_goes_until_the_session_is_heard_again()
    {
        using var run = Quest();
        Run(TimeSpan.FromMinutes(31));

        Assert.True(_watch.Leave("s1"));

        Assert.Null(_watch.RowOf("s1"));
        Assert.Empty(Run(TimeSpan.FromMinutes(60)));
        Assert.Contains(_events.Page("s1").Events, e => e.Parts?.Any(part => part.Code == "watch.left") == true);

        _watch.Heard("s1");
        var again = Run(TimeSpan.FromMinutes(31));
        Assert.Equal([SessionWatch.Marked, SessionWatch.Brought], again.Select(step => step.Step));
    }

    // ——— Case 12's watcher: --quiet-end, and nothing ended without it.

    [Fact]
    public void Under_quiet_end_a_session_unheard_that_long_busy_or_not_is_ended_once()
    {
        _watch.EndAfter = TimeSpan.FromMinutes(20);
        using var run = Quest();
        var cpu = TimeSpan.Zero;

        var steps = Run(TimeSpan.FromMinutes(25), each: () =>
        {
            cpu += TimeSpan.FromSeconds(10);
            _jobs["s1"] = new JobRead(true, cpu);
        });

        Assert.Equal(SessionWatch.Ended, Assert.Single(steps).Step);
        Assert.True(run.Ending.IsCancellationRequested);
        Assert.Equal(20, run.EndedMinutes);
        Assert.Equal(20, _watch.EndedMinutes("s1"));
    }

    [Fact]
    public void Without_quiet_end_nothing_is_ended_however_long_it_is_quiet()
    {
        using var run = Quest();

        Run(TimeSpan.FromHours(5));

        Assert.False(run.Ending.IsCancellationRequested);
        Assert.Null(run.EndedMinutes);
        Assert.NotNull(_watch.RowOf("s1"));
    }

    /// <summary>A run that ends ends its episode, said, and its row goes with it.</summary>
    [Fact]
    public void A_run_that_ends_takes_its_row_with_it()
    {
        var run = Quest();
        Run(TimeSpan.FromMinutes(31));

        run.Dispose();

        Assert.Null(_watch.RowOf("s1"));
        Assert.Empty(_watch.Rows());
    }

    // ——— Case 14: the cost is flat.

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void A_look_over_sessions_heard_reads_no_job_and_one_job_a_look_for_each_silent_one(int sessions)
    {
        var runs = Enumerable.Range(0, sessions).Select(at => Quest($"s{at}")).ToList();
        var silent = Math.Max(1, sessions / 10);

        _now += TimeSpan.FromMinutes(6);
        foreach (var at in Enumerable.Range(silent, sessions - silent)) _watch.Heard($"s{at}");
        _watch.Look(Job, Pace);

        Assert.Equal(silent, _jobReads);
        foreach (var run in runs) run.Dispose();
    }
}
