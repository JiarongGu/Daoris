namespace Daoris.Driver;

/// <summary>
/// A partner's strict job (COAGENT1b, D199 point 2; the co-agent design §2.3 steps 5 and 6, §7.2): an empty job made before anything of
/// the partner's starts, named so a restart can find it, ending what it holds when its last handle closes. Never the best effort an
/// ordinary session's job is (<see cref="ProcessJob.Hold(System.Diagnostics.Process)"/>): where the system makes no job, or the name
/// is already another's, the piece is refused <see cref="PartnerWhy.NoJob"/> and nothing runs, as a heavy run's gates are held
/// (HEAVYRUN1a). Its end is <see cref="ProcessJob.Terminate"/> then <see cref="ProcessJob.WaitEmptyAsync"/>: only then is
/// <see cref="PartnerStates.Ending"/> written, so no process of the partner's, nor a child, can still write its copy.
/// </summary>
public static class PieceJobs
{
    /// <summary>The job's name for a piece, in this sign-in's own namespace: what the state's <c>job</c> keeps.</summary>
    public static string NameOf(string piece) => PieceLauncher.JobPrefix + piece;

    /// <summary>A piece's job, made empty and wholly this one's; null where there is none to be had, which refuses the piece.</summary>
    internal static ProcessJob? Create(string piece) => ProcessJob.Create(NameOf(piece));

    /// <summary>
    /// What a look knows of a piece's job (<see cref="PartnerReconcile"/>): <paramref name="held"/> is the job this process made, read
    /// by its own count; otherwise the named job opened for its count. A job no name answers to, or a count that does not read, is
    /// <see cref="PartnerJob.Unknown"/>: after a restart its processes ended with its last handle, but Daoris cannot tell that from a job
    /// never made, so the copy waits for the person (the design §7.2).
    /// </summary>
    internal static PartnerJob Observe(string? name, ProcessJob? held = null)
    {
        var active = held is not null ? held.Active : name is { Length: > 0 } ? ProcessJob.ActiveOf(name) : null;
        return active switch
        {
            0 => PartnerJob.Empty,
            > 0 => PartnerJob.Running,
            _ => PartnerJob.Unknown,
        };
    }
}

/// <summary>What is known of a partner's strict job.</summary>
public enum PartnerJob
{
    /// <summary>Never started, or ended and observed empty.</summary>
    Empty,

    /// <summary>A process of the partner's, or a child of it, still runs in it.</summary>
    Running,

    /// <summary>Its state cannot be known (a restart lost its handle).</summary>
    Unknown,
}
