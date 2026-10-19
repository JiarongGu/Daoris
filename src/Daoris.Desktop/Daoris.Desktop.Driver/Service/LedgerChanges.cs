using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The driver's one wait on the local service's change stream (FABRIC-EVENT3b, the fabric events design §A4.2): held for the loop's
/// life, and each answer that names a family wakes the <c>ledger</c> topic once, so the look's local plan runs as soon as a quest
/// an agent published, a session's record, an ask or an ask's channel moved, never the remote sync (FABRIC-EVENT3a). The wake's
/// subject is the families the answer named, joined with commas (<c>sessions</c>, <c>quests</c>, <c>asks</c>, <c>channel</c>;
/// PUSH1d1), so whoever hears it tells them apart. The <c>look</c> topic's deadline (<c>pollSeconds</c>) still runs behind it, the
/// reconciliation for anything the stream does not carry.
/// </summary>
/// <remarks>
/// <para><b>Never a hot loop.</b> The next ask is never sooner than <see cref="Floor"/> after the last began; a refused or dropped
/// ask is backed off from a second to <see cref="LongestBackOff"/>; an answer with nothing, sooner than half its wait (a ninth
/// wait, or a host stopping), is backed off the same way; and a host with no such door (one from before) is asked again only
/// after <see cref="Absent"/>.</para>
///
/// <para><b>The bootstrap wakes the plan once</b>, as a cursor the host no longer knows (a restart, a reconnected observer) does:
/// it is asked beside the loop's first look, not before it, so its cursor may already hold a change that look read too early, and
/// without a wake that change would wait for the deadline. One plan more at the start is the cost; a pass that moves nothing still
/// wakes nothing after it (canary 5).</para>
/// </remarks>
internal static class LedgerChanges
{
    /// <summary>How long each wait asks to be held: under the client's own timeout (<see cref="DriverHttp.Timeout"/>).</summary>
    public const int WaitSeconds = 20;

    /// <summary>The least time between two asks.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(250);

    /// <summary>The first pause after a refused, dropped or empty-early ask.</summary>
    public static readonly TimeSpan FirstBackOff = TimeSpan.FromSeconds(1);

    /// <summary>The longest pause between failed asks.</summary>
    public static readonly TimeSpan LongestBackOff = TimeSpan.FromSeconds(30);

    /// <summary>How long a host with no change stream is left before it is asked again (an update may bring one).</summary>
    public static readonly TimeSpan Absent = TimeSpan.FromMinutes(1);

    /// <summary>Hold one wait after another until <paramref name="ct"/> ends, waking <paramref name="wake"/> with each family that moved.</summary>
    public static async Task RunAsync(ServiceClient service, Action<string, string> wake, MachineLog? log, CancellationToken ct)
    {
        string? since = null;
        var backOff = TimeSpan.Zero;
        var said = false;
        while (!ct.IsCancellationRequested)
        {
            var began = Stopwatch.GetTimestamp();
            TimeSpan pause;
            try
            {
                var path = $"/api/changes?wait={WaitSeconds}" + (since is null ? "" : $"&since={Uri.EscapeDataString(since)}");
                var reply = await service.SendAsync(ServiceVerb.Get, path, null, ct).ConfigureAwait(false);
                if (reply.Status == 404)
                {
                    if (!said) log?.Info("changes.absent");
                    (said, since, pause) = (true, null, Absent);
                }
                else if (reply.Status != 200)
                {
                    pause = backOff = Next(backOff);
                    if (backOff == FirstBackOff) log?.Warn("changes.refused", ("status", reply.Status));
                }
                else
                {
                    var (next, moved) = Read(reply.Payload);
                    if (moved.Count > 0) wake(ChangeTopics.Ledger, string.Join(',', moved));

                    var took = Stopwatch.GetElapsedTime(began);
                    var early = since is not null && moved.Count == 0 && took < TimeSpan.FromSeconds(WaitSeconds / 2.0);
                    since = next;
                    (pause, backOff) = early ? (Next(backOff), Next(backOff)) : (Floor - took, TimeSpan.Zero);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failed)
            {
                // A dropped or misread ask: the host restarting, or away. Said once a streak, and asked again after a pause.
                pause = backOff = Next(backOff);
                if (backOff == FirstBackOff) log?.Warn("changes.dropped", ("error", failed.GetType().Name));
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

    /// <summary>The cursor to ask from next and the families that moved, as the door answers them.</summary>
    private static (string Since, IReadOnlyList<string> Moved) Read(string payload)
    {
        using var answer = JsonDocument.Parse(payload);
        var root = answer.RootElement;
        var since = root.GetProperty("since").GetString() ?? throw new JsonException("the change stream answered no cursor");
        IReadOnlyList<string> moved = [.. root.GetProperty("moved").EnumerateArray()
            .Select(family => family.GetString())
            .OfType<string>()
            .Where(family => family.Length > 0)];
        return (since, moved);
    }
}
