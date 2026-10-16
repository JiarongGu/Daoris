using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's one wait on the service's change stream (FABRIC-EVENT3b, the fabric events design §A4.2): each answer that names a
/// family wakes the <c>ledger</c> topic once, the bootstrap wakes nothing, and a door that is missing or refusing is asked again
/// only after a pause, never in a hot loop.
/// </summary>
public sealed class LedgerChangesTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly StandInLedger _ledger = new();
    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(30));
    private readonly List<(string Topic, string Subject)> _woken = [];

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
    }

    private Task Listen(ServiceClient service) =>
        LedgerChanges.RunAsync(service, (topic, subject) =>
        {
            lock (_woken) _woken.Add((topic, subject));
        }, log: null, _closing.Token);

    private List<(string Topic, string Subject)> Woken()
    {
        lock (_woken) return [.. _woken];
    }

    [Fact]
    public async Task A_publish_wakes_the_ledger_topic_once_and_the_bootstrap_wakes_nothing()
    {
        _ledger.Changes = StandInLedger.ChangeDoor.Answers;
        using var service = _ledger.Client();
        var listening = Listen(service);

        await Poll.Until(() => _ledger.ChangesHeld == 1, () => "no wait held", Bound);
        Assert.Empty(Woken());

        _ledger.Publish("q1", "engine", from: "agent");
        await Poll.Until(() => Woken().Count == 1, () => "the publish woke nothing", Bound);
        Assert.Equal((ChangeTopics.Ledger, "quests"), Woken()[0]);

        // Asked again from where it answered: held, and one wait at a time (the bootstrap, the wait the publish answered, this one).
        await Poll.Until(() => _ledger.ChangesHeld == 1, () => "no wait held after the answer", Bound);
        await Task.Delay(300);
        Assert.Single(Woken());
        Assert.Equal(3, _ledger.ChangesAsked);

        await _closing.CancelAsync();
        await listening.WaitAsync(Bound);
    }

    /// <summary>A host from before has no door: asked once, and not again for a minute.</summary>
    [Fact]
    public async Task A_missing_door_is_not_asked_again_at_once()
    {
        using var service = _ledger.Client();
        var listening = Listen(service);

        await Poll.Until(() => _ledger.ChangesAsked == 1, () => "never asked", Bound);
        await Task.Delay(1500);

        Assert.Equal(1, _ledger.ChangesAsked);
        Assert.Empty(Woken());
        await _closing.CancelAsync();
        await listening.WaitAsync(Bound);
    }

    /// <summary>A door that refuses is backed off, a second and then longer: never a hot loop.</summary>
    [Fact]
    public async Task A_refusing_door_is_backed_off()
    {
        _ledger.Changes = StandInLedger.ChangeDoor.Refuses;
        using var service = _ledger.Client();
        var listening = Listen(service);

        await Task.Delay(2500);

        Assert.InRange(_ledger.ChangesAsked, 1, 3);
        Assert.Empty(Woken());
        await _closing.CancelAsync();
        await listening.WaitAsync(Bound);
    }
}
