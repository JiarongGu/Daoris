namespace Daoris.Ports.Processes;

/// <summary>
/// Starts the machine's processes. The application can neither make nor inspect a real process: it asks the host for a run of a
/// specification and reads what the run says.
/// </summary>
public interface IProcessHost
{
    /// <summary>Starts the process <paramref name="specification"/> describes, admitted, owned and contained as the host does for its class.</summary>
    ValueTask<IChildRun> StartAsync(ProcessSpec specification, CancellationToken ct);
}

/// <summary>One started process, seen from the application.</summary>
public interface IChildRun : IAsyncDisposable
{
    /// <summary>What it writes, as it is written, to its standard output and its standard error.</summary>
    IAsyncEnumerable<ProcessOutput> Output(CancellationToken ct);

    /// <summary>Writes to its standard input, which its specification left open.</summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken ct);

    /// <summary>Waits for it to end and answers how.</summary>
    ValueTask<ProcessEnd> EndAsync(CancellationToken ct);

    /// <summary>Ends it and what it started.</summary>
    ValueTask StopAsync(CancellationToken ct);
}
