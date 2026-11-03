using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The conversion's census as the loop last read it, every part's (ONEWAY1ae; the oneway design §1.1, the cleanup design §4.3), held in memory so
/// <i>What needs you</i> judges its one row without a read of its own. It is read again when the driver comes up and when <c>driver.json</c> moves
/// (an apply or an undo publishes the driver scope); it raises <see cref="Event"/> when the row's wait changes. It also keeps the plans this Daoris
/// answered (<see cref="Answered"/>), which a press is compared against where a part no longer answers.
/// </summary>
public sealed class ConversionCensus(IEventBus? bus, MachineLog? log = null)
{
    /// <summary>The event raised when the wait appears, goes, or counts differently.</summary>
    public const string Event = "CONVERSION_CENSUS";

    private readonly Lock _gate = new();
    private Task _last = Task.CompletedTask;
    private Task? _queued;
    private SortedDictionary<string, int>? _parts;
    private string? _since;

    /// <summary>The plans this Daoris answered, by digest, read by a press.</summary>
    public AnsweredPlans Answered { get; } = new();

    /// <summary>
    /// The row's wait, <c>{records, parts, since}</c>, the old records every part holds and their count by part, while some part's census has not
    /// ended; null before a reading and once each has.
    /// </summary>
    public JsonObject? Wait()
    {
        lock (_gate)
        {
            if (_parts is not { } parts) return null;
            var by = new JsonObject();
            foreach (var (part, count) in parts) by[part] = count;
            return new JsonObject { ["records"] = parts.Values.Sum(), ["parts"] = by, ["since"] = _since };
        }
    }

    /// <summary>Take <paramref name="parts"/> as the kept census; true, and told, where the wait differs from what was held.</summary>
    public bool Tell(IReadOnlyList<PartCensus> parts)
    {
        lock (_gate)
        {
            var holding = parts.Where(each => !each.Ended).ToList();
            SortedDictionary<string, int>? next = holding.Count == 0 ? null : new(holding.ToDictionary(each => each.Part, each => each.Count), StringComparer.Ordinal);
            if (next is null ? _parts is null : _parts is not null && next.SequenceEqual(_parts)) return false;
            _since = next is null ? null : _since ?? Moments.Write(DateTimeOffset.UtcNow);
            _parts = next;
        }

        bus?.Emit(StatePublisher.Module, Event, new { });
        return true;
    }

    /// <summary>
    /// Every part's census as the doors read it now. A part that does not answer refuses the reading, so what was held is kept rather than a
    /// part counted as having nothing.
    /// </summary>
    public static Func<CancellationToken, Task<IReadOnlyList<PartCensus>>> Reading(Func<ConvertDoors> doors) => async ct =>
    {
        var (parts, unread) = await ConversionApplies.CensusAsync(doors(), ct).ConfigureAwait(false);
        return unread.Count == 0 ? parts : throw new ConversionUnread($"`{unread[0].Part}` was not read: {unread[0].Said}");
    };

    /// <summary>
    /// Read the census again and keep it; a refused read keeps what was held. One reading runs at a time, and a call made while one runs
    /// shares the one queued after it: a burst of changes is one more reading, each caller answered by a reading begun after its call.
    /// </summary>
    public Task RefreshAsync(Func<CancellationToken, Task<IReadOnlyList<PartCensus>>> read, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_queued is { } queued) return queued;
            var previous = _last;
            _queued = _last = AfterAsync(previous, read, ct);
            return _queued;
        }
    }

    private async Task AfterAsync(Task previous, Func<CancellationToken, Task<IReadOnlyList<PartCensus>>> read, CancellationToken ct)
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
