using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The call each working session's agent is inside as every window holds it (WAITSAY1c1): what the rail says as *running `npm test`
/// for 8 min*, pushed as the call starts and ends rather than read again at a tick or a record's move.
/// </summary>
/// <remarks>
/// <para><b>Its own slice</b> (D182's WAITSAY1c1 note says why not the records'). The call is a fact of this loop alone, heard from
/// the events its sessions write (<see cref="RunningCalls"/>, WAITSAY1b), so the judge reads memory and no service: judged at once,
/// never <c>notJudged</c> past its first judgement.</para>
///
/// <para><b>One partition, keyed by session</b> (<see cref="StateSlice.Keyed"/>): each running call under its session's id as
/// <c>SESSION_GROUPS</c> writes a row's <c>calling</c>, its title and when it began; a call's start pushes that session alone and its
/// end names the session among the keys gone. With nothing running the partition is judged empty and pushed so, never gone, so the
/// page's calls empty rather than keep what they held.</para>
///
/// <para><b>Its wake.</b> <see cref="RunningCalls.Changed"/>, raised only where a session's call moved; the slice's coalescing makes
/// a burst one judgement. No bus event wakes it.</para>
///
/// <para><b>Desktop-only.</b> A call's title is the agent's words for it and may name a file by its path, which only this machine's
/// windows read (D47, D182 point 2).</para>
/// </remarks>
public static class CallsStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "calls";

    /// <summary>Its one partition, named for the field of a session's row it stands beside.</summary>
    public const string Partition = "calling";

    /// <summary>The slice the shell's loop owns, judging the calls heard from <paramref name="loop"/>'s record.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus)
    {
        var calls = RunningCalls.Of(loop.Events);
        return new(Name, bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(Judge(calls.Now())))
        {
            Keyed = true,
            Hears = [wake => HearCalls(calls, wake)],
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };
    }

    /// <summary>
    /// One judgement of the calls running now: the one partition, each call under its session as <c>SESSION_GROUPS</c> writes a row's
    /// <c>calling</c> (<c>{title, since, waiting}</c>, the start as the ISO string the page's clock reads, and whether it waits on the
    /// person's answer, AUTONOMY1b3b).
    /// </summary>
    public static IReadOnlyList<SliceJudged> Judge(IReadOnlyDictionary<string, RunningCall> now)
    {
        var part = new JsonObject();
        foreach (var (session, call) in now.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            part[session] = JsonSerializer.SerializeToNode(new { call.Title, call.Since, call.Waiting }, IpcJson.Options);
        }

        return [new SliceJudged(Partition, part)];
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where a session's running call moved; an event that moved none wakes nothing.
    /// Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearCalls(RunningCalls calls, Action wake)
    {
        void Heard(string _) => wake();

        calls.Changed += Heard;
        return new Hearing(() => calls.Changed -= Heard);
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
