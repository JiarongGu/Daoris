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
    private readonly SemaphoreSlim _reading = new(1, 1);
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

    /// <summary>Read the world again through <paramref name="service"/> and keep its census; a refused read keeps what was held.</summary>
    public async Task RefreshAsync(ServiceClient service, string configPath, string home, CancellationToken ct)
    {
        try
        {
            await _reading.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var world = await ConvertWorlds.ReadAsync(service.RegistryAsync, service.EveryQuestAsync, DriverConfig.Load(configPath), home, ct).ConfigureAwait(false);
                Tell(WorkflowLegacy.Census(world, new ConversionReceipts(home).HeldPlaces()));
            }
            finally
            {
                _reading.Release();
            }
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            log?.Warn("convert.census", ("error", failed.GetType().Name));
        }
    }
}
