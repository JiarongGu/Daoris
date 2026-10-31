using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The memory changes that wait on the person (KNOWCENTRAL2e; the memory coordinator design §4), read from the service on the driver's
/// poll and held in memory, so <i>What needs you</i> judges them without a read of its own. An end or a replacement awaiting its approver,
/// and a stopped session's staged rows, each session once; it raises <see cref="Event"/> when what waits changes, so the band judges again.
/// </summary>
/// <remarks>A refused or dropped read keeps what was last read: the service going away takes its rows with it, and the next answer says so.</remarks>
public sealed class MemoryWaits(IEventBus? bus, MachineLog? log = null)
{
    /// <summary>The event raised when what waits changes.</summary>
    public const string Event = "MEMORY_WAITS";

    private readonly Lock _gate = new();
    private IReadOnlyList<JsonElement> _waiting = [];
    private string _said = "";

    /// <summary>How often the service is read.</summary>
    public TimeSpan Pace { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>What waits as last read; none before the first answer.</summary>
    public IReadOnlyList<JsonElement> Now()
    {
        lock (_gate) return _waiting;
    }

    /// <summary>Take <paramref name="waiting"/> as what waits now; true, and told, where it differs from what was held.</summary>
    public bool Tell(IReadOnlyList<JsonElement> waiting)
    {
        lock (_gate)
        {
            var said = string.Join("\n", waiting.Select(each => each.GetRawText()));
            if (said == _said) return false;
            (_said, _waiting) = (said, waiting);
        }

        bus?.Emit(StatePublisher.Module, Event, new { });
        return true;
    }

    /// <summary>Read <paramref name="service"/> every <see cref="Pace"/> until <paramref name="ct"/> ends.</summary>
    public async Task RunAsync(ServiceClient service, CancellationToken ct)
    {
        var failing = false;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Tell(await service.MemoryWaitsAsync(ct).ConfigureAwait(false));
                failing = false;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception failed)
            {
                // The service restarting or away: said once a streak.
                if (!failing) log?.Warn("memory.waits", ("error", failed.GetType().Name));
                failing = true;
            }

            try
            {
                await Task.Delay(Pace, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
