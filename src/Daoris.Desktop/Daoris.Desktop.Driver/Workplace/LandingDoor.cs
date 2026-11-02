using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The door a run's <c>land</c> is performed through (WORKFLOWCLEAN1 1c; the land node design §3, §7): the runtime starts it, once per node and
/// tip, and it lands the run's work off the pass, from the node's bound intent, through <see cref="AutoLander.PerformAsync"/> over the session
/// trees, so the landing runs under the tree's hold and the delivery's key as every landing does. A press and a pass at once land one delivery.
/// What each landing came to is kept (<see cref="LandOutcomes"/>), for the press that waits to answer from.
/// </summary>
/// <param name="lander">Builds the performer's lander for one landing; it lands as the press does, authorized by the review's word.</param>
/// <param name="log">Told a landing that could not be tried; the machine log's line.</param>
/// <param name="runs">Whether a landing may begin now: not while an update drains (DRAINHOLD1); null always may.</param>
public sealed class LandingDoor(Func<AutoLander> lander, Action<string>? log = null, Func<bool>? runs = null) : ILandingDoor
{
    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Start(LandStart start)
    {
        if (runs is not null && !runs()) return false;
        var key = $"{start.Unit}\n{start.Node}\n{start.Tip}";
        // One landing of a node at a tip at a time; the next pass finds it ended and judges again, which lands nothing if nothing moved.
        var gate = new TaskCompletionSource();
        if (!_running.TryAdd(key, gate.Task)) return false;
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var said = await lander().PerformAsync(start).ConfigureAwait(false);
                    if (said is not null) LandOutcomes.Kept(start.Unit, start.Tip, said);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException
                                                  or UnauthorizedAccessException)
                {
                    log?.Invoke($"landing  run {start.Run}: its `land` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
                }
                finally
                {
                    _running.TryRemove(key, out _);
                    gate.TrySetResult();
                }
            });
        }

        return true;
    }

    /// <summary>Completes once every landing started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => Task.WhenAll(_running.Values);
}

/// <summary>
/// What the performer's landings came to, in memory (WORKFLOWCLEAN1 1c): the sentence of each run's newest, by unit and tip, which the press that
/// asked for the pass reads to answer. The delivery and the landing record are the durable facts; this is only their words for a press that
/// waited, so a restart loses it and the press then answers from them.
/// </summary>
public static class LandOutcomes
{
    private static readonly ConcurrentDictionary<string, string> Said = new(StringComparer.Ordinal);

    internal static void Kept(string unit, string tip, string said) => Said[$"{unit}\n{tip}"] = said;

    /// <summary>The sentence the performer's landing of <paramref name="unit"/> at <paramref name="tip"/> ended with; null where none was kept.</summary>
    public static string? Of(string unit, string tip) => Said.GetValueOrDefault($"{unit}\n{tip}");
}
