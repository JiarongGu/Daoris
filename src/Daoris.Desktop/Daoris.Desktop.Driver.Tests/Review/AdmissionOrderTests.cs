using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The order runs waiting for the one admission take their turns in (<see cref="AdmissionOrder"/>): the one comparator (<see cref="QueueOrder"/>),
/// read at each look, and the executor's pass rule: a refusal that every run behind would meet too (every slot runs, the admission's lock is
/// held) holds the rest, and one that is the run's own (memory, its repository's slots) lets the next try. Pure: no file, no process.
/// </summary>
public sealed class AdmissionOrderTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private static QueuePlace Place(int minutes, string @class = PriorityClasses.Normal) => new(@class, QueueStage.FirstChecks, At.AddMinutes(minutes));

    private static CheckWait Slots => CheckWait.OfSlots(1, 1);

    private static CheckWait Memory => new(CheckWait.Memory, new Dictionary<string, string> { ["free"] = "4.0", ["need"] = "8" });

    [Fact]
    public void The_first_to_arrive_of_a_class_has_the_first_turn_and_the_rest_wait_for_it_to_be_told()
    {
        var order = new AdmissionOrder();
        using var first = order.Join("a", () => Place(0));
        using var second = order.Join("b", () => Place(1));

        Assert.True(first.MayTry(out _));
        Assert.False(second.MayTry(out var held));
        Assert.Null(held);
    }

    /// <summary>🔴 An urgent check asking after three ordinary ones takes the next freed slot, and nothing running is cut.</summary>
    [Fact]
    public void An_urgent_run_asking_after_three_ordinary_ones_has_the_first_turn()
    {
        var order = new AdmissionOrder();
        using var one = order.Join("one", () => Place(1));
        using var two = order.Join("two", () => Place(2));
        using var three = order.Join("three", () => Place(3));
        using var urgent = order.Join("urgent", () => Place(4, PriorityClasses.Urgent));

        Assert.True(urgent.MayTry(out _));
        Assert.False(one.MayTry(out _));

        urgent.Refused(Slots);
        Assert.False(one.MayTry(out var held));
        Assert.Equal(CheckWait.Slots, held!.Code);

        urgent.Admitted();
        Assert.True(one.MayTry(out _));
        Assert.False(two.MayTry(out _));
        Assert.False(three.MayTry(out _));
    }

    [Fact]
    public void A_refusal_that_is_the_runs_own_lets_the_next_try_and_one_every_run_would_meet_holds_the_rest()
    {
        var order = new AdmissionOrder();
        using var first = order.Join("a", () => Place(0));
        using var second = order.Join("b", () => Place(1));
        using var third = order.Join("c", () => Place(2));

        first.Refused(Memory);
        Assert.True(second.MayTry(out _));
        Assert.False(third.MayTry(out _));

        second.Refused(CheckWait.OfRepository("Daoris", "the verification run r-1"));
        Assert.True(third.MayTry(out _));

        first.Refused(Slots);
        Assert.False(second.MayTry(out var held));
        Assert.Equal(CheckWait.Slots, held!.Code);
    }

    [Fact]
    public void A_run_that_leaves_without_being_admitted_gives_its_turn_to_the_next()
    {
        var order = new AdmissionOrder();
        var first = order.Join("a", () => Place(0));
        using var second = order.Join("b", () => Place(1));
        first.Refused(Slots);
        Assert.False(second.MayTry(out _));

        first.Dispose();

        Assert.True(second.MayTry(out _));
    }

    /// <summary>🔴 A class is read at each look and never kept: a change while it waits moves it.</summary>
    [Fact]
    public void A_class_changed_while_a_run_waits_moves_its_turn()
    {
        var @class = PriorityClasses.Normal;
        var order = new AdmissionOrder();
        using var first = order.Join("a", () => Place(0));
        using var second = order.Join("b", () => Place(1, @class));
        first.Refused(Memory);
        second.Refused(Memory);
        Assert.True(first.MayTry(out _));

        @class = PriorityClasses.High;

        Assert.True(second.MayTry(out _));
        Assert.True(first.MayTry(out _), "a's refusal is its own, so a may still try behind b");
        second.Refused(Slots);
        Assert.False(first.MayTry(out _));
    }

    [Fact]
    public void Two_runs_of_one_class_and_arrival_keep_the_order_they_joined_in()
    {
        var order = new AdmissionOrder();
        using var first = order.Join("a", () => Place(0));
        using var second = order.Join("b", () => Place(0));

        Assert.True(first.MayTry(out _));
        Assert.False(second.MayTry(out _));
    }
}
