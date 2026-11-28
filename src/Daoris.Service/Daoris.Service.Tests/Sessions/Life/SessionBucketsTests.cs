using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A session's time buckets from its journal: each second in exactly one bucket, held against an independent oracle that walks the
/// session minute by minute over random journals, and by a few named cases, the application's down spans among them.
/// </summary>
public sealed class SessionBucketsTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-11-28T08:00:00Z");

    private static SessionFact Fact(SessionFactKind kind, int minutes, SessionHolder? holder = null, SessionOutcome? outcome = null) =>
        new("s1", kind, new FactWitness(WitnessDoor.Advance), 1, T0.AddMinutes(minutes)) { Holder = holder ?? SessionHolder.None, Outcome = outcome };

    /// <summary>
    /// The oracle: every whole minute from the first fact to the end, put in the bucket of the fact that holds it (the last one at or
    /// before it, clocks stepped back taken as no later than the one before), Down where a down span covers it, and a turn's lease void
    /// from the first start after it began. Every boundary falls on a whole minute, so its count is exact.
    /// </summary>
    private static Dictionary<TimeBucket, TimeSpan> Minutes(
        IReadOnlyList<SessionFact> facts, DateTimeOffset now, IReadOnlyList<DownSpan> down, IReadOnlyList<DateTimeOffset> starts)
    {
        var moments = new List<DateTimeOffset>();
        foreach (var fact in facts) moments.Add(moments.Count == 0 || fact.At > moments[^1] ? fact.At : moments[^1]);
        var end = facts[^1].Kind == SessionFactKind.Ended || now < moments[^1] ? moments[^1] : now;
        var counted = Enum.GetValues<TimeBucket>().ToDictionary(bucket => bucket, _ => TimeSpan.Zero);
        for (var minute = moments[0]; minute < end; minute = minute.AddMinutes(1))
        {
            var holding = moments.FindLastIndex(moment => moment <= minute);
            var fact = facts[holding];
            var leasing = fact.Kind != SessionFactKind.TurnBegan ? Leasing.None
                : starts.Any(start => start > moments[holding] && start <= minute) ? Leasing.Void : Leasing.Live;
            var bucket = down.Any(span => span.From <= minute && minute < span.To) ? TimeBucket.Down : SessionBuckets.BucketOf(fact, leasing);
            counted[bucket] += TimeSpan.FromMinutes(1);
        }

        return counted;
    }

    [Fact]
    public void Each_random_journal_s_buckets_match_a_minute_by_minute_walk_and_sum_to_its_wall_time()
    {
        var random = new Random(368);
        var kinds = Enum.GetValues<SessionFactKind>();
        SessionHolder[] holders = [SessionHolder.None, SessionHolder.MachineRoom, new(HolderKind.CheckSlot), new(HolderKind.Step, "r1"), SessionHolder.Person];
        for (var trial = 0; trial < 500; trial++)
        {
            var facts = new List<SessionFact>();
            var minute = 0;
            for (var each = random.Next(1, 10); each > 0; each--)
            {
                // A clock may step back: a fact written behind the one before it.
                minute += random.Next(-30, 90);
                facts.Add(Fact(kinds[random.Next(kinds.Length)], minute, holders[random.Next(holders.Length)], SessionOutcome.Failed));
            }

            var withDown = trial % 2 == 0;
            List<DownSpan> down = [];
            for (var each = withDown ? random.Next(1, 4) : 0; each > 0; each--)
            {
                var from = T0.AddMinutes(random.Next(-120, 600));
                down.Add(new(from, from.AddMinutes(random.Next(-10, 150))));
            }

            List<DateTimeOffset> starts = [.. Enumerable.Range(0, random.Next(0, 3)).Select(_ => T0.AddMinutes(random.Next(-60, 800)))];
            var now = T0.AddMinutes(random.Next(-60, 900));

            var times = SessionBuckets.Of(facts, now, down, starts);
            var walked = Minutes(facts, now, down, starts);

            Assert.Equal(times.Wall, times.Buckets.Values.Aggregate(TimeSpan.Zero, (sum, spent) => sum + spent));
            Assert.All(Enum.GetValues<TimeBucket>(), bucket => Assert.True(walked[bucket] == times.Buckets[bucket], $"trial {trial}: {bucket} {times.Buckets[bucket]} against {walked[bucket]}"));
            // Down is never more than the down spans' overlap with the session's wall time.
            var overlap = TimeSpan.FromMinutes(Enumerable.Range(0, (int)times.Wall.TotalMinutes)
                .Select(at => facts[0].At.AddMinutes(at))
                .Count(moment => down.Any(span => span.From <= moment && moment < span.To)));
            Assert.True(times.Buckets[TimeBucket.Down] <= overlap, $"trial {trial}: down {times.Buckets[TimeBucket.Down]} over {overlap}");
            if (!withDown) Assert.Equal(TimeSpan.Zero, times.Buckets[TimeBucket.Down]);
        }
    }

    [Fact]
    public void A_turn_a_check_and_a_question_fall_in_their_buckets_until_the_end()
    {
        var times = SessionBuckets.Of(
            [
                Fact(SessionFactKind.Opened, 0, SessionHolder.MachineRoom), Fact(SessionFactKind.TurnBegan, 5),
                Fact(SessionFactKind.StepAwaited, 65, new(HolderKind.CheckSlot)), Fact(SessionFactKind.TurnBegan, 85),
                Fact(SessionFactKind.StepAwaited, 95, new(HolderKind.Step, "r1")), Fact(SessionFactKind.QuestionOpened, 125, SessionHolder.Person),
                Fact(SessionFactKind.Ended, 130, outcome: SessionOutcome.Done),
            ],
            T0.AddDays(2), []);

        Assert.Equal(TimeSpan.FromMinutes(130), times.Wall);
        Assert.Equal(
            (TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(70), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5)),
            (times.Buckets[TimeBucket.WaitingStart], times.Buckets[TimeBucket.Working], times.Buckets[TimeBucket.Checks],
             times.Buckets[TimeBucket.WaitingStep], times.Buckets[TimeBucket.OnPerson]));
    }

    [Fact]
    public void A_turn_is_working_until_the_next_start_and_still_after_it_as_the_fold_reads_it()
    {
        var times = SessionBuckets.Of([Fact(SessionFactKind.TurnBegan, 0)], T0.AddMinutes(90), [], [T0.AddMinutes(-30), T0.AddMinutes(60)]);

        Assert.Equal((TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30)), (times.Buckets[TimeBucket.Working], times.Buckets[TimeBucket.Still]));
    }

    [Fact]
    public void A_wait_naming_nothing_is_still_and_a_down_span_moves_its_seconds_to_down()
    {
        var times = SessionBuckets.Of(
            [Fact(SessionFactKind.TurnBegan, 0), Fact(SessionFactKind.StepAwaited, 60)],
            T0.AddMinutes(180),
            [new(T0.AddMinutes(30), T0.AddMinutes(90)), new(T0.AddMinutes(80), T0.AddMinutes(100))]);

        Assert.Equal(TimeSpan.FromMinutes(180), times.Wall);
        Assert.Equal(
            (TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(70), TimeSpan.FromMinutes(80)),
            (times.Buckets[TimeBucket.Working], times.Buckets[TimeBucket.Down], times.Buckets[TimeBucket.Still]));
    }

    [Fact]
    public void The_application_is_down_from_a_generation_s_last_sign_to_the_next_start()
    {
        AppGeneration[] generations =
        [
            // A fact at 2 h and a tick at 3 h; no fact and a tick at 5.5 h; neither, so down from its start.
            new(1, T0, Seen: T0.AddHours(3)), new(2, T0.AddHours(5), Seen: T0.AddHours(5.5)), new(3, T0.AddHours(6)), new(4, T0.AddHours(8)),
        ];

        var down = SessionBuckets.Down(generations, new Dictionary<long, DateTimeOffset> { [1] = T0.AddHours(2) });

        Assert.Equal(
            [new DownSpan(T0.AddHours(3), T0.AddHours(5)), new DownSpan(T0.AddHours(5.5), T0.AddHours(6)), new DownSpan(T0.AddHours(6), T0.AddHours(8))],
            down);
    }
}
