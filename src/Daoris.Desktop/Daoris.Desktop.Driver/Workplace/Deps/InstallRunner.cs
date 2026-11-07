namespace Daoris.Driver;

/// <summary>
/// What the store asks of the machine to run an install: a place in its admission as a heavy step, and the command started held in a
/// job. The review keeps both for its heavy runs and implements this, installed when the driver loads
/// (<c>Review/Heavy/InstallRunnerWiring.cs</c>).
/// </summary>
internal interface IInstallRunner
{
    /// <summary>A place in the machine's admission for an install of <paramref name="what"/>, held until disposed.</summary>
    /// <exception cref="OperationCanceledException">Cancelled while waiting; nothing is held.</exception>
    Task<IDisposable> AdmitAsync(string home, string what, string repository, string tree, CancellationToken ct);

    /// <summary>
    /// Start <paramref name="command"/> in <paramref name="directory"/> with exactly <paramref name="environment"/>, joined to
    /// <paramref name="job"/> before it runs, its output written to <paramref name="log"/>.
    /// </summary>
    /// <exception cref="DriverException">It would not start, or could not be held in the job, so it did not run.</exception>
    IRunningInstall Start(ProcessJob? job, string command, string directory, IReadOnlyDictionary<string, string> environment, string log);
}

/// <summary>An install's command as it runs.</summary>
internal interface IRunningInstall : IDisposable
{
    /// <exception cref="OperationCanceledException">Cancelled first; the command is not ended by this.</exception>
    Task<int> ExitAsync(CancellationToken ct);

    /// <summary>End what the command started where its job cannot.</summary>
    void Stop();
}

internal static class InstallRunner
{
    private static IInstallRunner? _current;

    internal static IInstallRunner Current
    {
        get => _current ?? throw new InvalidOperationException("no install runner is installed: Review installs it when the driver loads");
        set => _current = value;
    }
}
