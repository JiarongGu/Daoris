using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1a (D181 points 2-4): the machine's facts, kept by judges on background tasks and read as last judged. A reader takes
/// the snapshot as it stands and never waits on a judgement; a wake judges its own subject alone; a subject is never judged twice
/// at once; a topic judges a few of its subjects together; and a judgement that does not answer in its bound holds no other.
/// </summary>
/// <remarks>No process and no git: every judge here is a stand-in.</remarks>
public sealed class MachineFactsTests
{
    private const string Lines = "lines";

    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    private static readonly FactRules Rules = new(Deadline: TimeSpan.FromMinutes(5), Bound: TimeSpan.FromSeconds(30), AtOnce: 2);

    /// <summary>What a test's judge was asked, by subject, from any thread.</summary>
    private sealed class Asked
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string subject)
        {
            lock (_counts) _counts[subject] = _counts.GetValueOrDefault(subject) + 1;
        }

        public int this[string subject]
        {
            get
            {
                lock (_counts) return _counts.GetValueOrDefault(subject);
            }
        }
    }

    [Fact]
    public async Task A_subject_is_judged_once_the_facts_start_and_its_fact_says_when()
    {
        var at = DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        using var facts = new MachineFacts(() => at);
        facts.Topic(Lines, Rules, (subject, _) => Task.FromResult<object?>($"`main` for {subject}"));
        facts.Subject(Lines, "engine");
        var changed = new TaskCompletionSource<MachineFactsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        facts.Changed += snapshot => changed.TrySetResult(snapshot);

        // Known, not started: nothing is judged, and nothing reads as judged.
        Assert.Null(facts.Current.Of(Lines, "engine"));

        facts.Start();
        var landed = await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

        Assert.Equal("`main` for engine", landed.Value<string>(Lines, "engine"));
        Assert.Equal(at, landed.Of(Lines, "ENGINE")!.At);
        Assert.Equal("`main` for engine", (await changed.Task.WaitAsync(Within)).Value<string>(Lines, "engine"));
    }

    [Fact]
    public async Task A_wake_judges_its_own_subject_alone()
    {
        var asked = new Asked();
        using var facts = new MachineFacts();
        facts.Topic(Lines, Rules, (subject, _) =>
        {
            asked.Add(subject);
            return Task.FromResult<object?>($"{subject} #{asked[subject]}");
        });
        facts.Subject(Lines, "engine");
        facts.Subject(Lines, "site");
        facts.Start();
        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);
        await facts.FirstAsync(Lines, "site", CancellationToken.None).WaitAsync(Within);

        facts.Wake(Lines, "site");
        await Poll.Until(() => facts.Current.Value<string>(Lines, "site") == "site #2", within: Within);

        Assert.Equal((1, 2), (asked["engine"], asked["site"]));
        Assert.Equal("engine #1", facts.Current.Value<string>(Lines, "engine"));
    }

    [Fact]
    public async Task Two_wakes_for_one_subject_judge_it_once_at_a_time_and_once_more_after()
    {
        var asked = new Asked();
        var inside = 0;
        var most = 0;
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var facts = new MachineFacts();
        facts.Topic(Lines, Rules, async (subject, _) =>
        {
            var now = Interlocked.Increment(ref inside);
            InterlockedMax(ref most, now);
            asked.Add(subject);
            entered.TrySetResult();
            if (asked[subject] == 1) await first.Task;
            Interlocked.Decrement(ref inside);
            return $"judged {asked[subject]}";
        });
        facts.Subject(Lines, "engine");
        facts.Start();
        await entered.Task.WaitAsync(Within);

        facts.Wake(Lines, "engine");
        facts.Wake(Lines, "engine");
        first.SetResult();

        await Poll.Until(() => facts.Current.Value<string>(Lines, "engine") == "judged 2", within: Within);
        await Task.Delay(200);
        Assert.Equal(1, Volatile.Read(ref most));
        Assert.Equal(2, asked["engine"]);
    }

    [Fact]
    public async Task A_judge_that_never_answers_holds_no_other_subject_and_is_never_judged_twice_at_once()
    {
        var asked = new Asked();
        using var facts = new MachineFacts();
        // One at a time, so the subject that never answers holds the only place until its bound gives it back.
        facts.Topic(Lines, Rules with { AtOnce = 1, Bound = TimeSpan.FromMilliseconds(200) }, (subject, _) =>
        {
            asked.Add(subject);
            return subject == "stuck" ? new TaskCompletionSource<object?>().Task : Task.FromResult<object?>("`main`");
        });
        facts.Subject(Lines, "stuck");
        facts.Subject(Lines, "engine");
        facts.Start();

        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);
        facts.Wake(Lines, "stuck");
        facts.Wake(Lines, "engine");
        await Poll.Until(() => asked["engine"] == 2, within: Within);

        Assert.Equal(1, asked["stuck"]);
        Assert.Null(facts.Current.Of(Lines, "stuck"));
    }

    /// <summary>FACTSLATE1: a judge cut at its own bound is late, said once and no error, and the fact stays as last judged.</summary>
    [Fact]
    public async Task A_judge_cut_at_its_bound_is_said_once_as_late_and_never_as_an_error()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-facts-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var calls = 0;
            using (var log = new MachineLog(home, "desktop"))
            using (var facts = new MachineFacts { Log = log })
            {
                facts.Topic(Lines, Rules with { Bound = TimeSpan.FromMilliseconds(200) }, async (_, ct) =>
                {
                    if (Interlocked.Increment(ref calls) == 1) return "`main`";
                    await Task.Delay(Timeout.Infinite, ct);
                    return "`trunk`";
                });
                facts.Subject(Lines, "engine");
                facts.Start();
                await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

                await facts.JudgedAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

                Assert.Equal("`main`", facts.Current.Value<string>(Lines, "engine"));
            }

            var lines = MachineLogReader.Read(DaorisHome.Entry(home, HomeEntry.Logs), new LogFilter()).Lines;
            Assert.Equal(["facts.late"], lines.Select(line => line.Event));
            Assert.Equal("warn", lines[0].Level);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public async Task A_read_never_waits_on_a_judgement_under_way()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var facts = new MachineFacts();
        facts.Topic(Lines, Rules, async (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1) return "`main`";
            entered.TrySetResult();
            await release.Task;
            return "`trunk`";
        });
        facts.Subject(Lines, "engine");
        facts.Start();
        var before = await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

        facts.Wake(Lines, "engine");
        await entered.Task.WaitAsync(Within);
        var reading = Stopwatch.StartNew();
        var during = await Task.Run(() => facts.Current).WaitAsync(Within);
        reading.Stop();

        Assert.Same(before, during);
        Assert.Equal("`main`", during.Value<string>(Lines, "engine"));
        Assert.True(reading.Elapsed < TimeSpan.FromSeconds(2), $"a read took {reading.Elapsed}");

        release.SetResult();
        await Poll.Until(() => facts.Current.Value<string>(Lines, "engine") == "`trunk`", within: Within);
    }

    [Fact]
    public async Task A_topic_judges_no_more_of_its_subjects_together_than_its_bound()
    {
        var inside = 0;
        var most = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var facts = new MachineFacts();
        facts.Topic(Lines, Rules with { AtOnce = 2 }, async (subject, _) =>
        {
            InterlockedMax(ref most, Interlocked.Increment(ref inside));
            await release.Task;
            Interlocked.Decrement(ref inside);
            return subject;
        });
        foreach (var subject in new[] { "a", "b", "c", "d" }) facts.Subject(Lines, subject);
        facts.Start();

        await Poll.Until(() => Volatile.Read(ref inside) == 2, within: Within);
        await Task.Delay(200);
        Assert.Equal(2, Volatile.Read(ref inside));

        release.SetResult();
        foreach (var subject in new[] { "a", "b", "c", "d" }) await facts.FirstAsync(Lines, subject, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref most));
    }

    [Fact]
    public async Task A_subject_is_judged_again_at_its_deadline_and_not_before()
    {
        var now = DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var asked = new Asked();
        using var facts = new MachineFacts(() => now);
        facts.Topic(Lines, Rules with { Deadline = TimeSpan.FromMinutes(5) }, (subject, _) =>
        {
            asked.Add(subject);
            return Task.FromResult<object?>(subject);
        });
        facts.Subject(Lines, "engine");
        facts.Start();
        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

        now += TimeSpan.FromMinutes(4);
        facts.Reconcile();
        await facts.Settled().WaitAsync(Within);
        Assert.Equal(1, asked["engine"]);

        now += TimeSpan.FromMinutes(1);
        facts.Reconcile();
        await Poll.Until(() => asked["engine"] == 2, within: Within);
    }

    /// <summary>
    /// A topic's rest makes a burst of wakes one judgement each rest; a door that asks afresh and waits (<c>JudgedAsync</c>) is
    /// judged now.
    /// </summary>
    [Fact]
    public async Task Wakes_within_a_subject_s_rest_wait_for_it_and_an_ask_afresh_does_not()
    {
        var asked = new Asked();
        using var facts = new MachineFacts();
        facts.Topic(Lines, Rules with { Rest = TimeSpan.FromMinutes(5) }, (subject, _) =>
        {
            asked.Add(subject);
            return Task.FromResult<object?>($"{subject} #{asked[subject]}");
        });
        facts.Subject(Lines, "engine");
        facts.Start();
        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

        facts.Wake(Lines, "engine");
        await Task.Delay(200);
        Assert.Equal(1, asked["engine"]);

        await facts.JudgedAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);
        Assert.Equal("engine #2", facts.Current.Value<string>(Lines, "engine"));
    }

    /// <summary>
    /// OWNMOVES1: a wake afresh, a move Daoris made itself, is judged at once past the subject's rest and waits for nothing, while a
    /// plain wake of another subject in the same moment is still held; the clock never moves, so no rest ends.
    /// </summary>
    [Fact]
    public async Task A_wake_afresh_is_judged_past_the_rest_and_a_plain_wake_beside_it_is_held()
    {
        var at = DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var asked = new Asked();
        using var facts = new MachineFacts(() => at);
        facts.Topic(Lines, Rules with { Rest = TimeSpan.FromMinutes(5) }, (subject, _) =>
        {
            asked.Add(subject);
            return Task.FromResult<object?>($"{subject} #{asked[subject]}");
        });
        facts.Subject(Lines, "engine");
        facts.Subject(Lines, "site");
        facts.Start();
        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);
        await facts.FirstAsync(Lines, "site", CancellationToken.None).WaitAsync(Within);

        facts.Wake(Lines, "engine");
        facts.Wake(Lines, "site", afresh: true);
        await Poll.Until(() => facts.Current.Value<string>(Lines, "site") == "site #2", within: Within);
        await facts.Settled().WaitAsync(Within);
        await Task.Delay(200);

        Assert.Equal((1, 2), (asked["engine"], asked["site"]));
    }

    /// <summary>
    /// OWNMOVES1: a wake afresh that lands while its subject is judged is judged once more right after, past the rest the judgement
    /// under way just began, since that one may have read the subject before the move.
    /// </summary>
    [Fact]
    public async Task A_wake_afresh_during_a_judgement_is_judged_right_after_it_past_the_rest()
    {
        var at = DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var asked = new Asked();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var facts = new MachineFacts(() => at);
        facts.Topic(Lines, Rules with { Rest = TimeSpan.FromMinutes(5) }, async (subject, _) =>
        {
            asked.Add(subject);
            if (asked[subject] == 2)
            {
                entered.TrySetResult();
                await held.Task;
            }

            return $"{subject} #{asked[subject]}";
        });
        facts.Subject(Lines, "engine");
        facts.Start();
        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

        // The second judgement asked afresh, held while it runs; a third asked afresh meanwhile follows it at once.
        facts.Wake(Lines, "engine", afresh: true);
        await entered.Task.WaitAsync(Within);
        facts.Wake(Lines, "engine", afresh: true);
        held.SetResult();

        await Poll.Until(() => facts.Current.Value<string>(Lines, "engine") == "engine #3", within: Within);
        await facts.Settled().WaitAsync(Within);
        Assert.Equal(3, asked["engine"]);
    }

    private static readonly AsyncLocal<string?> Waker = new();

    [Fact]
    public async Task A_judgement_carries_the_owner_s_context_never_the_waker_s()
    {
        var seen = new List<string?>();
        using var facts = new MachineFacts();
        facts.Topic(Lines, Rules, (subject, _) =>
        {
            lock (seen) seen.Add(Waker.Value);
            return Task.FromResult<object?>(subject);
        });
        facts.Subject(Lines, "engine");
        facts.Start();
        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);

        Waker.Value = "a press";
        await facts.JudgedAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);
        Waker.Value = null;

        lock (seen) Assert.Equal([null, null], seen);
    }

    [Fact]
    public async Task A_wake_before_the_facts_start_is_kept_and_judged_once_they_do()
    {
        var asked = new Asked();
        using var facts = new MachineFacts();
        facts.Topic(Lines, Rules, (subject, _) =>
        {
            asked.Add(subject);
            return Task.FromResult<object?>(subject);
        });
        facts.Wake(Lines, "engine");
        await Task.Delay(100);
        Assert.Equal(0, asked["engine"]);

        facts.Start();
        await facts.FirstAsync(Lines, "engine", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, asked["engine"]);
    }

    private static void InterlockedMax(ref int most, int now)
    {
        var seen = Volatile.Read(ref most);
        while (now > seen)
        {
            var was = Interlocked.CompareExchange(ref most, now, seen);
            if (was == seen) return;
            seen = was;
        }
    }
}
