using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// What waits at the service's door for the person, told whole at each change, numbered so a window keeps the newest telling (as the
/// pipe's <see cref="PipeConfirmationsNow"/> is). Each is the service's own confirmation, as <c>GET /api/confirmations</c> lists it.
/// </summary>
public sealed record ServiceConfirmationsNow(long Seq, IReadOnlyList<JsonElement> Waiting);

/// <summary>
/// The presses waiting at the service's own door, told to the windows (NOTIFY2j, D196 point 4, the notification design §2.5, §3.3):
/// the desktop's host holds one wait on the service (<see cref="ConfirmationDoors.WaitAsync"/>) and raises <c>DAORIS</c>
/// <see cref="Event"/> with what waits at each change, beside the pipe's <see cref="PipeConfirmations.Event"/>, so every window counts
/// one list and no window polls. The bridge's <c>SERVICE_CONFIRMATIONS</c> reads it for a window that opens mid-wait.
/// </summary>
/// <remarks>
/// <para><b>Told at a change, once.</b> An answer that holds what was told already tells nothing: a wait answered at its bound, a
/// ninth wait, a restarted service whose new cursor finds the same list.</para>
///
/// <para><b>An older service keeps a poll here</b>, not in each window: a host with no <see cref="ConfirmationDoors.Waiting"/> door
/// has its list read every <see cref="Pace"/>, one read for every window.</para>
///
/// <para><b>Never a hot loop</b>, as the ledger's change stream is read: the next ask is never sooner than <see cref="Floor"/> after
/// the last began; a refused or dropped ask, and an early answer that moved nothing, are backed off from a second to
/// <see cref="LongestBackOff"/>. Meanwhile the windows keep what was told: a service gone took its confirmations with it, and the
/// next answer says so.</para>
/// </remarks>
public sealed class ServiceConfirmations(IEventBus? bus, MachineLog? log = null)
{
    /// <summary>The event that tells every window what waits at the service.</summary>
    public const string Event = "SERVICE_CONFIRMATIONS";

    /// <summary>How long each wait asks to be held: under the client's own timeout.</summary>
    public const int WaitSeconds = 20;

    /// <summary>How often an older service's list is read.</summary>
    public static readonly TimeSpan Pace = TimeSpan.FromSeconds(5);

    /// <summary>The least time between two asks.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(250);

    /// <summary>The first pause after a refused, dropped or empty-early ask.</summary>
    public static readonly TimeSpan FirstBackOff = TimeSpan.FromSeconds(1);

    /// <summary>The longest pause between failed asks.</summary>
    public static readonly TimeSpan LongestBackOff = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private ServiceConfirmationsNow _now = new(0, []);
    private string _said = "";

    /// <summary>What waits at the service as last told; none before the first answer.</summary>
    public ServiceConfirmationsNow Now()
    {
        lock (_gate) return _now;
    }

    /// <summary>
    /// Take <paramref name="waiting"/> as what waits now: where it differs from what was told, numbered and told to every window.
    /// True where it was told.
    /// </summary>
    public bool Tell(IReadOnlyList<JsonElement> waiting)
    {
        ServiceConfirmationsNow told;
        lock (_gate)
        {
            var said = string.Join("\n", waiting.Select(each => each.GetRawText()));
            if (said == _said && _now.Seq > 0) return false;
            _said = said;
            told = _now = new ServiceConfirmationsNow(_now.Seq + 1, waiting);
        }

        bus?.Emit(StatePublisher.Module, Event, told);
        return true;
    }

    /// <summary>Hold one wait after another on <paramref name="doors"/> until <paramref name="ct"/> ends, telling each change.</summary>
    public async Task RunAsync(ConfirmationDoors doors, CancellationToken ct)
    {
        string? since = null;
        var backOff = TimeSpan.Zero;
        while (!ct.IsCancellationRequested)
        {
            var began = Stopwatch.GetTimestamp();
            TimeSpan pause;
            try
            {
                if (await doors.WaitAsync(since, WaitSeconds, ct).ConfigureAwait(false) is { } answer)
                {
                    Tell(answer.Waiting);
                    var took = Stopwatch.GetElapsedTime(began);
                    var early = answer.Since == since && took < TimeSpan.FromSeconds(WaitSeconds / 2.0);
                    since = answer.Since;
                    (pause, backOff) = early ? (Next(backOff), Next(backOff)) : (Floor - took, TimeSpan.Zero);
                }
                else
                {
                    Tell(await doors.ListAsync(ct).ConfigureAwait(false));
                    (since, pause, backOff) = (null, Pace, TimeSpan.Zero);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                // The loop's client went with the loop.
                return;
            }
            catch (Exception failed)
            {
                // A dropped or refused ask: the service restarting, or away. Said once a streak, and asked again after a pause.
                pause = backOff = Next(backOff);
                if (backOff == FirstBackOff) log?.Warn("confirmations.dropped", ("error", failed.GetType().Name));
            }

            if (pause <= TimeSpan.Zero) continue;
            try
            {
                await Task.Delay(pause, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static TimeSpan Next(TimeSpan backOff) =>
        backOff <= TimeSpan.Zero ? FirstBackOff
        : backOff * 2 > LongestBackOff ? LongestBackOff
        : backOff * 2;
}
