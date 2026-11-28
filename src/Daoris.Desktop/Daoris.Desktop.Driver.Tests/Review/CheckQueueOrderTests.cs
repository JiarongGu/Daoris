using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The check slots' waiting rows carry their class and first arrival, and the reader orders them by
/// class, then first arrival; a run back at its checks keeps the arrival of its first.
/// </summary>
public sealed class CheckQueueOrderTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "check-queue", Guid.NewGuid().ToString("N")[..8]);

    public CheckQueueOrderTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static VerifyWaiter Waiter(string run, int minutes, string @class = PriorityClasses.Normal, int? arrived = null) =>
        new(1, 1, run, "i-" + run, "Daoris", At.AddMinutes(minutes), minutes)
        {
            Class = @class, Arrived = At.AddMinutes(arrived ?? minutes),
        };

    [Fact]
    public void Three_ordinary_runs_waiting_and_an_urgent_one_asking_after_them_reads_first()
    {
        VerifySlot.Wait(_home, Waiter("first", 1));
        VerifySlot.Wait(_home, Waiter("second", 2));
        VerifySlot.Wait(_home, Waiter("third", 3));
        VerifySlot.Wait(_home, Waiter("urgent", 4, PriorityClasses.Urgent) with { Source = PriorityBook.Quest, By = "the person", Why = "the fix that slows every session" });

        var waiters = VerifySlot.Look(_home, (_, _) => true).Waiters;

        Assert.Equal(["urgent", "first", "second", "third"], waiters.Select(waiter => waiter.Run));
        Assert.Equal((PriorityClasses.Urgent, PriorityBook.Quest, "the person", "the fix that slows every session"), (waiters[0].Class, waiters[0].Source, waiters[0].By, waiters[0].Why));
    }

    [Fact]
    public void A_run_back_at_its_checks_sorts_by_its_first_arrival_within_its_class()
    {
        VerifySlot.Wait(_home, Waiter("later", 5));
        VerifySlot.Wait(_home, Waiter("back", 9, arrived: 1));
        VerifySlot.Wait(_home, Waiter("low", 0, PriorityClasses.Low));

        Assert.Equal(["back", "later", "low"], VerifySlot.Look(_home, (_, _) => true).Waiters.Select(waiter => waiter.Run));
    }

    [Fact]
    public void A_run_s_first_arrival_is_kept_across_its_attempts()
    {
        Assert.Equal(At, CheckArrivals.Keep(_home, "v-0000000000000001", At));
        Assert.Equal(At, CheckArrivals.Keep(_home, "v-0000000000000001", At.AddHours(5)));
        Assert.Equal(At.AddHours(1), CheckArrivals.Keep(_home, "v-0000000000000002", At.AddHours(1)));
    }

    [Fact]
    public void A_run_whose_check_passed_arrives_afresh_at_its_next_check()
    {
        CheckArrivals.Keep(_home, "v-0000000000000001", At);
        CheckArrivals.Forget(_home, "v-0000000000000001");

        Assert.Equal(At.AddDays(2), CheckArrivals.Keep(_home, "v-0000000000000001", At.AddDays(2)));
    }

    [Fact]
    public void A_held_waiter_reads_back_held_and_one_never_held_reads_not()
    {
        VerifySlot.Wait(_home, Waiter("held", 1) with { Held = true, For = "held by the person: make room" });
        VerifySlot.Wait(_home, Waiter("free", 2));

        var waiters = VerifySlot.Look(_home, (_, _) => true).Waiters;

        Assert.Equal([("held", true), ("free", false)], waiters.Select(waiter => (waiter.Run, waiter.Held)));
    }

    [Fact]
    public void A_waiter_with_no_class_set_reads_normal_arriving_when_it_was_sent()
    {
        VerifySlot.Wait(_home, new VerifyWaiter(1, 1, "old", "i-old", "Daoris", At, 1));

        var waiter = Assert.Single(VerifySlot.Look(_home, (_, _) => true).Waiters);
        Assert.Equal((PriorityClasses.Normal, At), (waiter.Class, waiter.FirstArrival));
    }
}
