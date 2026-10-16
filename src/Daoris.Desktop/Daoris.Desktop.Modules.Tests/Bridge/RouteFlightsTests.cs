using Daoris.Desktop;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The flights a read route's identical asks share (SWEEPFLIGHT1): one ask under way per key, answered to every caller that
/// joined it, kept past its end for no one, stopped only when no caller waits for it, and joined no more once forgotten.
/// </summary>
public sealed class RouteFlightsTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Joins_while_one_is_under_way_share_its_one_ask_and_the_next_after_it_asks_again()
    {
        var flights = new RouteFlights();
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;
        Task<object?> Ask(CancellationToken _)
        {
            Interlocked.Increment(ref asked);
            return held.Task;
        }

        var first = flights.JoinAsync("LINES", Ask, CancellationToken.None);
        var second = flights.JoinAsync("LINES", Ask, CancellationToken.None);
        Assert.Equal(1, asked);

        var answer = new object();
        held.SetResult(answer);
        Assert.Same(answer, await first.WaitAsync(Patience));
        Assert.Same(answer, await second.WaitAsync(Patience));

        Assert.Same(answer, await flights.JoinAsync("LINES", Ask, CancellationToken.None).WaitAsync(Patience));
        Assert.Equal(2, asked);
    }

    [Fact]
    public async Task Different_keys_are_different_flights()
    {
        var flights = new RouteFlights();
        var asked = 0;
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object?> Ask(CancellationToken _)
        {
            Interlocked.Increment(ref asked);
            return held.Task;
        }

        var lines = flights.JoinAsync("LINES", Ask, CancellationToken.None);
        var sweep = flights.JoinAsync("SWEEP_PLAN", Ask, CancellationToken.None);
        Assert.Equal(2, asked);
        held.SetResult(null);
        await Task.WhenAll(lines, sweep).WaitAsync(Patience);
    }

    [Fact]
    public async Task A_refusal_reaches_every_caller_that_joined_and_the_next_asks_again()
    {
        var flights = new RouteFlights();
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;
        Task<object?> Ask(CancellationToken _)
        {
            Interlocked.Increment(ref asked);
            return held.Task;
        }

        var first = flights.JoinAsync("SWEEP_PLAN", Ask, CancellationToken.None);
        var second = flights.JoinAsync("SWEEP_PLAN", Ask, CancellationToken.None);
        var refused = new InvalidOperationException("the driver is still coming up");
        held.SetException(refused);

        Assert.Same(refused, await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(Patience)));
        Assert.Same(refused, await Assert.ThrowsAsync<InvalidOperationException>(() => second.WaitAsync(Patience)));

        held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        held.SetResult("fresh");
        Assert.Equal("fresh", await flights.JoinAsync("SWEEP_PLAN", Ask, CancellationToken.None).WaitAsync(Patience));
        Assert.Equal(2, asked);
    }

    [Fact]
    public async Task An_ask_that_throws_before_its_first_wait_is_every_callers_refusal_too()
    {
        var flights = new RouteFlights();
        var refused = new InvalidOperationException("not ready");

        await Assert.ThrowsAsync<InvalidOperationException>(() => flights.JoinAsync("LINES", _ => throw refused, CancellationToken.None));
        Assert.Equal("asked", await flights.JoinAsync("LINES", _ => Task.FromResult<object?>("asked"), CancellationToken.None));
    }

    [Fact]
    public async Task A_caller_that_stops_waiting_leaves_the_flight_to_the_others()
    {
        var flights = new RouteFlights();
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object?> Ask(CancellationToken ct)
        {
            ct.Register(() => stopped.TrySetResult());
            return held.Task;
        }

        using var leaving = new CancellationTokenSource();
        var left = flights.JoinAsync("SWEEP_PLAN", Ask, leaving.Token);
        var staying = flights.JoinAsync("SWEEP_PLAN", Ask, CancellationToken.None);

        await leaving.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => left.WaitAsync(Patience));
        await Task.Delay(100);
        Assert.False(stopped.Task.IsCompleted, "the look was stopped while a caller still waited for it");

        held.SetResult("listed");
        Assert.Equal("listed", await staying.WaitAsync(Patience));
    }

    [Fact]
    public async Task A_flight_no_caller_waits_for_is_stopped_and_the_next_asks_afresh()
    {
        var flights = new RouteFlights();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;
        async Task<object?> Ask(CancellationToken ct)
        {
            if (Interlocked.Increment(ref asked) > 1) return "fresh";
            ct.Register(() => stopped.TrySetResult());
            await Task.Delay(Timeout.Infinite, ct);
            return "never";
        }

        using var one = new CancellationTokenSource();
        using var two = new CancellationTokenSource();
        var first = flights.JoinAsync("LINES", Ask, one.Token);
        var second = flights.JoinAsync("LINES", Ask, two.Token);
        await one.CancelAsync();
        await two.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Patience));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WaitAsync(Patience));
        await stopped.Task.WaitAsync(Patience);
        Assert.Equal("fresh", await flights.JoinAsync("LINES", Ask, CancellationToken.None).WaitAsync(Patience));
    }

    [Fact]
    public async Task A_flight_forgotten_is_joined_no_more_and_still_answers_those_that_joined_it()
    {
        var flights = new RouteFlights();
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;
        Task<object?> Ask(CancellationToken _) => Interlocked.Increment(ref asked) == 1 ? held.Task : Task.FromResult<object?>("after");

        var before = flights.JoinAsync("LINES", Ask, CancellationToken.None);
        flights.Forget();
        Assert.Equal("after", await flights.JoinAsync("LINES", Ask, CancellationToken.None).WaitAsync(Patience));
        Assert.Equal(2, asked);

        held.SetResult("before");
        Assert.Equal("before", await before.WaitAsync(Patience));
    }
}
