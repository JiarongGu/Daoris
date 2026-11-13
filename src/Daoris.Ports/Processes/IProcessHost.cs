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
    /// <summary>Its id and <see cref="Created"/>, its creation time, which together name it to <see cref="IProcessLiveness"/>.</summary>
    int Id { get; }

    DateTimeOffset Created { get; }

    /// <summary>
    /// What it writes, as it is written, to its standard output and its standard error; where its specification asked for raw output,
    /// its standard error alone. It ends once the process has exited and its streams are read, or a moment after the exit where a
    /// process it started holds a stream open.
    /// </summary>
    IAsyncEnumerable<ProcessOutput> Output(CancellationToken ct);

    /// <summary>What it writes to its standard output as bytes, as they are written, where its specification asked for raw output; none otherwise.</summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> Bytes(CancellationToken ct);

    /// <summary>Writes to its standard input, which its specification left open.</summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken ct);

    /// <summary>Closes its standard input, so a program that reads to the end of it reads that end.</summary>
    ValueTask CloseInputAsync();

    /// <summary>Waits for it to end and answers how.</summary>
    ValueTask<ProcessEnd> EndAsync(CancellationToken ct);

    /// <summary>
    /// Ends it and what it started, and waits a bounded time for its exit. Null where it has exited; else why it is not known to
    /// have ended: its kill was refused, or no exit was seen within the bound.
    /// </summary>
    ValueTask<string?> StopAsync(CancellationToken ct);
}
