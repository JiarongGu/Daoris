using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The conversion's census as the loop last read it (WORKFLOWCLEAN1g-ii; the cleanup design §4.3), held in memory so <i>What needs you</i>
/// judges the row without a read of its own. It is read again when the driver comes up and when <c>driver.json</c> moves (an apply or an undo
/// publishes the driver scope); it raises <see cref="Event"/> when the row's wait changes.
/// </summary>
public sealed class ConversionCensus(IEventBus? bus, MachineLog? log = null)
{
    /// <summary>The event raised when the wait appears, goes, or counts differently.</summary>
    public const string Event = "CONVERSION_CENSUS";

    private readonly Lock _gate = new();
    private Task _last = Task.CompletedTask;
    private Task? _queued;
    private int? _places;
    private string? _since;

    /// <summary>The row's wait, <c>{places, since}</c>, while the kept census has not ended; null before a reading and once it has.</summary>
    public JsonObject? Wait()
    {
        lock (_gate) return _places is { } places ? new JsonObject { ["places"] = places, ["since"] = _since } : null;
    }

    /// <summary>Take <paramref name="census"/> as the kept one; true, and told, where the wait differs from what was held.</summary>
    public bool Tell(LegacyCensus census)
    {
        lock (_gate)
        {
            int? places = census.Ended ? null : census.Places.Count;
            if (places == _places) return false;
            _since = places is null ? null : _since ?? Moments.Write(DateTimeOffset.UtcNow);
            _places = places;
        }

        bus?.Emit(StatePublisher.Module, Event, new { });
        return true;
    }

    /// <summary>The home's census as <paramref name="service"/> and the files read it now.</summary>
    public static Func<CancellationToken, Task<LegacyCensus>> Reading(ServiceClient service, string configPath, string home) => async ct =>
    {
        var world = await ConvertWorlds.ReadAsync(service.RegistryAsync, service.EveryQuestAsync, DriverConfig.Load(configPath), home, ct).ConfigureAwait(false);
        return WorkflowLegacy.Census(world, new ConversionReceipts(home).HeldPlaces());
    };

    /// <summary>
    /// Read the census again and keep it; a refused read keeps what was held. One reading runs at a time, and a call made while one runs
    /// shares the one queued after it: a burst of changes is one more reading, each caller answered by a reading begun after its call.
    /// </summary>
    public Task RefreshAsync(Func<CancellationToken, Task<LegacyCensus>> read, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_queued is { } queued) return queued;
            var previous = _last;
            _queued = _last = AfterAsync(previous, read, ct);
            return _queued;
        }
    }

    private async Task AfterAsync(Task previous, Func<CancellationToken, Task<LegacyCensus>> read, CancellationToken ct)
    {
        // Always yields, so the reading never begins inside RefreshAsync's lock, before it is the queued one.
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ForceYielding);
        // Reading begins: a change from here on is not covered by it, and queues the next.
        lock (_gate) _queued = null;
        try
        {
            Tell(await read(ct).ConfigureAwait(false));
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            log?.Warn("convert.census", ("error", failed.GetType().Name));
        }
    }
}
