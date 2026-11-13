namespace Daoris.Ports.Processes;

/// <summary>
/// A runner's cancellation whose process outlived its kill: the pid and why, so the owner hears the process is not ended rather than
/// a cancellation that reads as one that worked.
/// </summary>
public sealed class ProcessUnendedException(int pid, string reason, CancellationToken token)
    : OperationCanceledException($"process {pid} did not end: {reason}", token)
{
    /// <summary>The process that runs on.</summary>
    public int Pid { get; } = pid;

    /// <summary>Why it is not known to have ended: its kill refused, or no exit seen within the bound.</summary>
    public string Reason { get; } = reason;
}
