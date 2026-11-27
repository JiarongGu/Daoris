using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A session's time buckets from its journal: each second in exactly one bucket, so the buckets sum to its wall time whatever the
/// facts, their clocks and the application's down spans, held as a property over random journals and by a few named cases.
/// </summary>
public sealed class SessionBucketsTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-11-28T08:00:00Z");

    private static SessionFact Fact(SessionFactKind kind, int minutes, string? holder = null, SessionOutcome? outcome = null) =>
        new("s1", kind, "test", 1, T0.AddMinutes(minutes)) { Holder = holder, Outcome = outcome };

    [Fact]
    public void Each_random_journal_s_buckets_sum_to_its_wall_time_and_none_is_negative()
    {
        var random = new Random(368);
        var kinds = Enum.GetValues<SessionFactKind>();
        string?[] holders = [null, SessionFact.StartHolder, SessionFact.CheckHolder, "run:r1"];
        for (var trial = 0; trial < 2000; trial++)
        {
            var facts = new List<SessionFact>();
            var minute = 0;
            for (var each = random.Next(1, 12); each > 0; each--)
            {
                // A clock may step back: a fact written behind the one before it.
                minute += random.Next(-30, 240);
                facts.Add(Fact(kinds[random.Next(kinds.Length)], minute, holders[random.Next(holders.Length)], SessionOutcome.Failed));
            }

            List<DownSpan> down = [];
            for (var each = random.Next(0, 4); each > 0; each--)
            {
                var from = T0.AddMinutes(random.Next(-120, 2000));
                down.Add(new(from, from.AddMinutes(random.Next(-10, 600))));
            }

            var now = T0.AddMinutes(random.Next(-60, 3000));

            var times = SessionBuckets.Of(facts, now, down);

            Assert.True(times.Wall >= TimeSpan.Zero, $"trial {trial}: wall {times.Wall}");
            Assert.All(times.Buckets.Values, spent => Assert.True(spent >= TimeSpan.Zero, $"trial {trial}: a bucket at {spent}"));
            Assert.Equal(times.Wall, times.Buckets.Values.Aggregate(TimeSpan.Zero, (sum, spent) => sum + spent));
        }
    }

    [Fact]
    public void A_turn_a_check_and_a_question_fall_in_their_buckets_until_the_end()
    {
        var times = SessionBuckets.Of(
            [
                Fact(SessionFactKind.Opened, 0, SessionFact.StartHolder), Fact(SessionFactKind.TurnBegan, 5),
                Fact(SessionFactKind.StepAwaited, 65, SessionFact.CheckHolder), Fact(SessionFactKind.TurnBegan, 85),
                Fact(SessionFactKind.StepAwaited, 95, "run:r1"), Fact(SessionFactKind.QuestionOpened, 125),
                Fact(SessionFactKind.Ended, 130, outcome: SessionOutcome.Done),
            ],
            T0.AddDays(2), []);

        Assert.Equal(TimeSpan.FromMinutes(130), times.Wall);
        Assert.Equal(TimeSpan.FromMinutes(5), times.Buckets[TimeBucket.WaitingStart]);
        Assert.Equal(TimeSpan.FromMinutes(70), times.Buckets[TimeBucket.Working]);
        Assert.Equal(TimeSpan.FromMinutes(20), times.Buckets[TimeBucket.Checks]);
        Assert.Equal(TimeSpan.FromMinutes(30), times.Buckets[TimeBucket.WaitingStep]);
        Assert.Equal(TimeSpan.FromMinutes(5), times.Buckets[TimeBucket.OnPerson]);
    }

    [Fact]
    public void A_wait_naming_nothing_is_still_and_a_down_span_moves_its_seconds_to_down()
    {
        var times = SessionBuckets.Of(
            [Fact(SessionFactKind.TurnBegan, 0), Fact(SessionFactKind.StepAwaited, 60)],
            T0.AddMinutes(180),
            [new(T0.AddMinutes(30), T0.AddMinutes(90)), new(T0.AddMinutes(80), T0.AddMinutes(100))]);

        Assert.Equal(TimeSpan.FromMinutes(180), times.Wall);
        Assert.Equal(TimeSpan.FromMinutes(30), times.Buckets[TimeBucket.Working]);
        Assert.Equal(TimeSpan.FromMinutes(70), times.Buckets[TimeBucket.Down]);
        Assert.Equal(TimeSpan.FromMinutes(80), times.Buckets[TimeBucket.Still]);
    }

    [Fact]
    public void The_application_is_down_from_the_newest_fact_its_generation_wrote_to_the_next_start()
    {
        AppGeneration[] generations = [new(1, T0), new(2, T0.AddHours(5)), new(3, T0.AddHours(6))];

        var down = SessionBuckets.Down(generations, new Dictionary<long, DateTimeOffset> { [1] = T0.AddHours(2) });

        Assert.Equal([new DownSpan(T0.AddHours(2), T0.AddHours(5)), new DownSpan(T0.AddHours(5), T0.AddHours(6))], down);
    }
}
