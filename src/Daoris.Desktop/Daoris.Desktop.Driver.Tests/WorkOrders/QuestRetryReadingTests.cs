using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A retry right after a stop reads the records the stop wrote, never only the plan of a look made before it.</summary>
public sealed class QuestRetryReadingTests
{
    private static readonly QuestView Taken = new("q1", "game", "engine", "Cap the chunk budget", "A body.", "Taken");
    private static readonly PriorSession Stop = new("s1a2b3c4", "X:/trees/s1", "stopped", "the person stopped it.");

    private static Task<IReadOnlyList<Consideration>> Held(CancellationToken _) =>
        Task.FromResult<IReadOnlyList<Consideration>>([new Consideration(Taken, StartVerdict.Stopped, "you stopped it") { HeldBy = Stop }]);

    [Fact]
    public async Task A_stop_made_after_the_last_look_is_released_by_a_retry_at_once()
    {
        IReadOnlyList<Consideration> stale = [new Consideration(Taken, StartVerdict.Start, "carrying on")];

        var decision = await QuestRetryReading.DecideAsync("q1", stale, Held, CancellationToken.None);

        Assert.Equal((RetryDecision.Released, "s1a2b3c4"), (decision.Did, decision.Session));
    }

    [Fact]
    public async Task A_quest_the_records_hold_nothing_for_is_still_refused()
    {
        IReadOnlyList<Consideration> stale = [new Consideration(Taken, StartVerdict.Start, "carrying on")];

        var refused = await Assert.ThrowsAsync<ChoiceRefusal>(() => QuestRetryReading.DecideAsync(
            "q1", stale, _ => Task.FromResult<IReadOnlyList<Consideration>>(stale), CancellationToken.None));

        Assert.Equal(ChoiceRefusal.NotHeld, refused.Check);
    }

    [Fact]
    public async Task Where_the_records_cannot_be_read_the_look_s_refusal_stands()
    {
        IReadOnlyList<Consideration> stale = [new Consideration(Taken, StartVerdict.Start, "carrying on")];

        var refused = await Assert.ThrowsAsync<ChoiceRefusal>(() => QuestRetryReading.DecideAsync(
            "q1", stale, _ => throw new DriverException("the service did not answer."), CancellationToken.None));

        Assert.Equal(ChoiceRefusal.NotHeld, refused.Check);
    }
}
