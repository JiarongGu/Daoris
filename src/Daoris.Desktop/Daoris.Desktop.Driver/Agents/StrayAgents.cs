using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The look's reaper of agent processes left running by sessions that have ended. It reads the records Daoris kept as it started each
/// process (<see cref="StartRecords"/>) and nothing else: a process is Daoris's own only by its record, with the start time that tells it
/// from another the machine gave its id, so a program of another maker at the same path, which no record names, is never read, said or
/// touched.
/// </summary>
/// <remarks>
/// <para>A record is <b>left</b> where its process still runs as the one it recorded, its start names a program of the toolchain
/// (<c>&lt;toolchain&gt;/&lt;agent&gt;/&lt;version&gt;</c>), and its session is neither one the service lists as active nor one this
/// registry runs: the session has ended, is gone, or the service never knew it. The first look to find it says it in the machine log
/// (<c>agent.stray</c>: its id, agent, pin, age and session) and keeps the time in the record; one more look past <see cref="Grace"/>
/// ends the process and its tree (<c>agent.stray.stopped</c>), since a session's own end ends its processes in seconds and what outlives
/// that has no one to run it. A pin move shortens the wait: a process at a version no pin names any more, of an agent whose pins are
/// read, has no session to wait for and is ended at the look that finds it. A session that runs again before the grace passes
/// clears the time.</para>
///
/// <para>Where the service did not answer, no look is made: "cannot tell" is never "left". A record whose process has gone is removed.</para>
/// </remarks>
/// <param name="markers">The folder of the markers and records (<see cref="HomeEntry.SessionPids"/>).</param>
/// <param name="toolchain">The toolchain folder (<see cref="HomeEntry.Toolchain"/>), whose programs are the ones reaped.</param>
/// <param name="pinsOf">The versions pinned for an agent, the machine's and each workspace's; none where it has no pin.</param>
/// <param name="now">The clock.</param>
/// <param name="log">The machine log, or null where nothing keeps one.</param>
internal sealed class StrayAgents(
    string markers, string toolchain, Func<string, IReadOnlySet<string>> pinsOf, Func<DateTimeOffset> now, MachineLog? log = null)
{
    /// <summary>How long a process left running is given, from the look that first found it, before it is ended.</summary>
    internal static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    /// <param name="active">The sessions the service lists as active.</param>
    /// <param name="runsHere">Whether this registry runs a session's process.</param>
    /// <returns>A line for each process said or ended at this look.</returns>
    public IReadOnlyList<string> Look(IReadOnlySet<string> active, Func<string, bool> runsHere)
    {
        var said = new List<string>();
        foreach (var (file, start) in StartRecords.Read(markers))
        {
            if (!SessionProcesses.IsRunning(start.Pid, start.Started))
            {
                StartRecords.Remove(file);
                continue;
            }

            if (start.Toolchain(toolchain) is not { } program) continue;
            if (active.Contains(start.Session) || runsHere(start.Session))
            {
                if (start.Seen is not null) StartRecords.Mark(file, start, null);
                continue;
            }

            var at = now();
            var moved = pinsOf(program.Agent) is { Count: > 0 } pins && !pins.Contains(program.Pin);
            var firstSeen = start.Seen ?? at;
            var age = at - new DateTimeOffset(start.Started, TimeSpan.Zero);
            if (start.Seen is null)
            {
                StartRecords.Mark(file, start, at);
                said.Add(Say(start, program, age, moved ? "stray.pin-moved" : "stray", "agent.stray"));
            }

            if (!moved && at - firstSeen < Grace) continue;
            if (End(start))
            {
                StartRecords.Remove(file);
                said.Add(Say(start, program, age, moved ? "stopped.pin-moved" : "stopped", "agent.stray.stopped"));
            }
        }

        return said;
    }

    private string Say(StartRecord start, (string Agent, string Pin) program, TimeSpan age, string code, string @event)
    {
        var minutes = (long)Math.Max(0, age.TotalMinutes);
        log?.Warn(
            @event,
            ("pid", start.Pid), ("agent", program.Agent), ("pin", program.Pin), ("ageMinutes", minutes), ("session", start.Session), ("code", code));
        var text = code switch
        {
            "stray" => "left running after its session ended; Daoris ends it if it still runs in {5} minutes",
            "stray.pin-moved" => "left running after its session ended, at a pin that has moved on; ended now",
            _ => "ended: it was left running after its session ended",
        };
        return string.Format(
            CultureInfo.InvariantCulture,
            "Agent process {0} ({1} {2}, {3} min old, session {4}) " + text + ".",
            start.Pid, program.Agent, program.Pin, minutes, start.Session, (int)Grace.TotalMinutes);
    }

    /// <summary>Whether the process, read again as the one the record names, is ended with its tree.</summary>
    private static bool End(StartRecord start) => SessionProcesses.EndRecorded(start.Pid, start.Started);
}
