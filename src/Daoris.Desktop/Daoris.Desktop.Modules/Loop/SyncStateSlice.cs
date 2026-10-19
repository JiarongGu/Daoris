using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Where each wired circle stands with its remote, as every window holds it (PUSH1a2, D182 points 2 and 5): what <c>/api/sync</c>
/// answers for each circle this machine has a remote for, read once per wake for every window and pushed when it moves, so a tick
/// reads <c>/api/sync</c> again in no window.
/// </summary>
/// <remarks>
/// <para><b>The route's own look.</b> The judge reads the service's answer for each circle the remotes map wires
/// (<see cref="ServiceClient.SyncStandingAsync"/> over <see cref="Wired"/>, what each pass and <c>daoris-driver sync status</c> read),
/// not the pass's own result: a quest written here by any door and a conflict's press move what is ahead and in conflict too, and the
/// passes' record stays outside the change stream, since a pass rewrites when it last tried at every tick. So a pushed standing is as
/// fresh as the pull's. Null before the service answers, which keeps the slice not judged; a read that fails keeps what was judged, so
/// a slice judged once has judged every wired circle.</para>
///
/// <para><b>A partition per wired circle</b>, named as the map names it, each the record the route answers for it
/// (<see cref="Record"/>), pushed whole: a record is small, and each pass moves its <c>tried</c>. A circle with no remote here is no
/// partition: the route answers it <c>wired: false</c> and nothing else, which the window writes itself for a circle the slice does
/// not hold.</para>
///
/// <para><b>Its wakes</b>, each only asking. The end of every pass through the loop's one set, the tick's and <i>Sync now</i>'s
/// (<see cref="HearPasses"/>), a circle wired or unwired since being read at the pass the remotes editor nudges; the change stream's
/// <c>quests</c> family (<see cref="HearLedger"/>), a quest written by any door, a connector's or the service's own conflict doors
/// among them; a change declared to <c>sync</c>; and the service coming up. Never the loop's look itself, whose pass tells its own
/// end.</para>
///
/// <para><b>Desktop-only.</b> The wall is the host's own sentence about this machine's remote, and the circles are this machine's
/// wiring (D47, D182 point 2); the remote's key is read to name the circles and never written into a partition.</para>
/// </remarks>
public static class SyncStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "sync";

    /// <summary>
    /// What each circle's record carries: the fields the route answers, as the service names them. A field the route comes to answer
    /// is pushed once it is named here or in <see cref="PulledOnly"/>, which <c>SyncStateSliceTests</c> holds against the service's
    /// answer record and the page's type.
    /// </summary>
    public static readonly IReadOnlyList<string> Fields = ["workspace", "wired", "ahead", "behind", "conflicts", "synced", "tried", "problem"];

    /// <summary>What the route answers of a circle that is never pushed: nothing yet.</summary>
    public static readonly IReadOnlyList<string> PulledOnly = [];

    /// <summary>
    /// What wakes it on the kit's bus: a change declared to where each circle stands (<i>Sync now</i>'s), and the service coming up.
    /// Each pass's end and the change stream's <c>quests</c> family wake it too (<see cref="HearPasses"/>, <see cref="HearLedger"/>).
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && changed.Scopes.Contains(Name)),
        new("DRIVER_READY"),
    ];

    /// <summary>The slice the shell's loop owns, reading <paramref name="loop"/>'s service once it answers.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, async ct => loop.Service is { } service ? await JudgeAsync(service, Wired(), ct).ConfigureAwait(false) : null)
        {
            WakesOn = WakesOn,
            Hears = [wake => HearPasses(loop, wake), wake => HearLedger(loop, wake)],
            // The service away or refusing: the windows keep what was judged, and the next wake reads it again.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>The circles this machine has a remote for, in name order, as each pass reads the map: the environment's pair, else the file.</summary>
    public static IReadOnlyList<string> Wired() => [.. RemoteTarget.Load().Keys.Order(StringComparer.Ordinal)];

    /// <summary>One judgement: each of <paramref name="circles"/> a partition named by it, the record the route answers for it.</summary>
    public static async Task<IReadOnlyList<SliceJudged>> JudgeAsync(ServiceClient service, IReadOnlyList<string> circles, CancellationToken ct)
    {
        var judged = new List<SliceJudged>(circles.Count);
        foreach (var circle in circles)
        {
            judged.Add(new SliceJudged(circle, Record(await service.SyncStandingAsync(circle, ct).ConfigureAwait(false))));
        }

        return judged;
    }

    /// <summary>
    /// A circle's standing as the route writes it: each field of <see cref="Fields"/>, a null one left out as the service's host
    /// leaves it out, so the pull's answer and the push's read alike.
    /// </summary>
    public static JsonObject Record(SyncStanding standing)
    {
        var written = JsonSerializer.SerializeToNode(standing, IpcJson.Options) as JsonObject ?? new JsonObject();
        var picked = new JsonObject();
        foreach (var field in Fields)
        {
            if (written.TryGetPropertyValue(field, out var value) && value is not null) picked[field] = value.DeepClone();
        }

        return picked;
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) at the end of each sync pass the loop's set runs, told by
    /// <see cref="DriverLoop.HeardPass"/>. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearPasses(DriverLoop loop, Action wake)
    {
        loop.Passed += wake;
        return new Hearing(() => loop.Passed -= wake);
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where the loop hears the change stream name the <c>quests</c> family: a quest written
    /// by any door, which moves what is ahead and what is in conflict. Another family, or another topic, wakes nothing. Disposed, it
    /// hears no more.
    /// </summary>
    public static IDisposable HearLedger(DriverLoop loop, Action wake)
    {
        void Heard(string topic, string subject)
        {
            if (topic == ChangeTopics.Ledger
                && subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains("quests", StringComparer.Ordinal))
            {
                wake();
            }
        }

        loop.MoveHeard += Heard;
        return new Hearing(() => loop.MoveHeard -= Heard);
    }

    private sealed class Hearing(Action stop) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0) stop();
        }
    }
}
