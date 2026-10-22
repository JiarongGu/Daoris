using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The one route every agent, plugin process and piece starts by (TERMTRUST1a part (i); D201 point 4; the terminal-trust design §3.3 point
/// 3): its start made the launcher's in place (<see cref="AgentJobs.Contain"/>), so its harness is born in this Daoris process's agents' job,
/// then started through the start gate (<see cref="ProcessStarts"/>), and said on the machine log (<c>agent.contained</c>). The
/// <see cref="Process"/> answered is the launcher's, and the doors read it as the session's process: its streams are the harness's, its exit
/// the harness's, and a kill of it ends the harness through the launcher's own job.
/// </summary>
/// <remarks>
/// <para><b>Best effort</b> (Fable's F2): a start that cannot be contained is made as it was, logged <c>contained: false</c> with why, and
/// holds its process's open event until it ends. A piece is the exception: it is never started outside its own job.</para>
/// <para><c>ProcessStartsCensusTests</c> holds every agent start to this route, and every other start through the gate to a named reason
/// of Daoris's own.</para>
/// </remarks>
public static class AgentStarts
{
    /// <summary>The machine log's source a line is written as where the caller has no log of its own: the driver's, whose starts they are.</summary>
    private const string Source = "driver";

    /// <summary>
    /// Start <paramref name="info"/> as <paramref name="kind"/>, contained where it can be, through the gate as <paramref name="whose"/>.
    /// </summary>
    /// <param name="home">The home beside whose install the launcher is laid out, and whose machine log is written where no log is handed.</param>
    /// <param name="log">The caller's machine log, or null for the home's driver file.</param>
    /// <param name="piece">A piece's job, which the start joins beneath the agents' and is never started outside.</param>
    /// <exception cref="DriverException">A piece that cannot be started through the launcher.</exception>
    public static async Task<Process?> StartAsync(
        ProcessStartInfo info, ContainedKind kind, StartClass whose, string? home, MachineLog? log, CancellationToken ct, string? piece = null)
    {
        using var start = AgentJobs.Current.Contain(info, kind, PieceStarts.LauncherPath(home), piece);
        var process = await ProcessStarts.Shared.StartAsync(info, whose, ct).ConfigureAwait(false);
        start.Lives(process);
        if (process is not null) Said(start, home, log);
        return process;
    }

    /// <summary>The start's line (<c>agent.contained</c>, the machine log design §4): codes only, never a path, an id or a word.</summary>
    internal static void Said(AgentStart start, string? home, MachineLog? log)
    {
        (string, object?)[] data = start.Contained
            ? [("kind", KindName(start.Kind)), ("contained", true)]
            : [("kind", KindName(start.Kind)), ("contained", false), ("why", start.Why)];
        if (log is { Writing: true })
        {
            log.Info("agent.contained", data);
            return;
        }

        if (string.IsNullOrEmpty(home)) return;
        using var own = new MachineLog(home, Source);
        own.Info("agent.contained", data);
    }

    /// <summary>A kind as the log says it.</summary>
    public static string KindName(ContainedKind kind) => kind switch
    {
        ContainedKind.Plugin => "plugin",
        ContainedKind.Piece => "piece",
        _ => "agent",
    };
}
