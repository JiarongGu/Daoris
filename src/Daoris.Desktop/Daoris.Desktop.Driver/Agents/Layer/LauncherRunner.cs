using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Lyntai.Processes;

namespace Daoris.Driver;

/// <summary>
/// The layer's runner is Daoris's launcher (the agent-lyntai design §5.5): every member a layer door uses starts through Platform's
/// <see cref="AgentStarts"/>, which contains the process as it contains every agent (D201), with no fallback to the layer's own runner; a
/// spawn's container is built over one. Its clock is Daoris's: a turn arms none, whatever window the layer hands it, since a turn ends at the
/// agent's terminal or the person's stop and Daoris limits no session by time; a read is bounded by its agent's patience. It keeps the
/// process the launcher answered, its exit (the layer's terminal carries none), what it wrote on stdin, and whether Daoris stopped it.
/// </summary>
/// <param name="patience">A read's bound; null for a turn's runner, which arms no clock.</param>
/// <param name="exists">Whether a command is spawnable, as Install's toolchain answers: never the layer's optimistic default.</param>
internal sealed class LauncherRunner(LauncherSeat seat, TimeSpan? patience = null, Func<string, bool>? exists = null) : IProcessRunner
{
    private const int TailKept = 40;

    private readonly object _gate = new();

    private readonly List<string> _written = [];

    private readonly Queue<string> _tail = new();

    private Process? _process;

    private int? _exit;

    private bool _stopped;

    /// <summary>The last start's process, as the launcher answered it.</summary>
    public Process? Process
    {
        get { lock (_gate) return _process; }
    }

    /// <summary>The last start's exit; null before it ended and where its patience killed it.</summary>
    public int? Exit
    {
        get { lock (_gate) return _exit; }
    }

    /// <summary>Whether Daoris stopped the process (<see cref="Stop"/>), which the layer then reads as a failed exit.</summary>
    public bool Stopped
    {
        get { lock (_gate) return _stopped; }
    }

    /// <summary>What was written on stdin, each write as written, in order.</summary>
    public IReadOnlyList<string> Written
    {
        get { lock (_gate) return [.. _written]; }
    }

    /// <summary>A stop: Daoris's kill of the process tree, kept so the run reads the exit after it as a stop.</summary>
    public void Stop()
    {
        Process? process;
        lock (_gate)
        {
            _stopped = true;
            process = _process;
        }

        Kill(process);
    }

    public bool CommandExists(string command) => exists?.Invoke(command) ?? false;

    public async Task<ProcessResult> RunAsync(
        string command, IReadOnlyList<string> args, string? stdin = null, TimeSpan? inactivityTimeout = null, TimeSpan? maxDuration = null,
        string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken ct = default)
    {
        var output = new StringBuilder();
        try
        {
            await foreach (var line in LinesAsync(command, args, Fixed(stdin), workingDirectory, environment, ct).ConfigureAwait(false))
            {
                output.AppendLine(line);
            }

            return new ProcessResult(0, output.ToString(), Tail());
        }
        catch (ProcessRunException failed)
        {
            return new ProcessResult(failed.ExitCode, output.ToString(), Tail());
        }
        catch (ProcessTimeoutException)
        {
            return new ProcessResult(-1, output.ToString(), Tail(), ProcessTimeoutKind.MaxDuration);
        }
    }

    public IAsyncEnumerable<string> StreamLinesAsync(
        string command, IReadOnlyList<string> args, string? stdin = null, TimeSpan? inactivityTimeout = null, TimeSpan? maxDuration = null,
        string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken ct = default) =>
        LinesAsync(command, args, Fixed(stdin), workingDirectory, environment, ct);

    public IAsyncEnumerable<string> ExchangeLinesAsync(
        string command, IReadOnlyList<string> args, IAsyncEnumerable<string> stdinLines, TimeSpan? inactivityTimeout = null,
        TimeSpan? maxDuration = null, string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken ct = default) =>
        LinesAsync(command, args, Framed(stdinLines), workingDirectory, environment, ct);

    private async IAsyncEnumerable<string> LinesAsync(
        string command, IReadOnlyList<string> args, Func<Process, CancellationToken, Task> write, string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environment, [EnumeratorCancellation] CancellationToken ct)
    {
        using var bound = patience is { } limit ? new CancellationTokenSource(limit) : null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, bound?.Token ?? CancellationToken.None);
        var process = await StartAsync(command, args, workingDirectory, environment, linked.Token).ConfigureAwait(false);
        await using var kill = linked.Token.Register(() => Kill(process)).ConfigureAwait(false);
        var errors = ErrorsAsync(process);
        var writing = write(process, linked.Token);
        var ended = false;
        try
        {
            // Read to the end of the stream, never to a token: a kill ends it, and the lines it said before are each yielded first.
            while (await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
            {
                seat.Heard();
                var frame = NativeFrame.Of(line);
                seat.Framed(frame);
                if (!frame.IsObject) seat.Outside(line);
                yield return line;
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await errors.ConfigureAwait(false);
            await writing.ConfigureAwait(false);
            ended = true;
        }
        finally
        {
            // An enumerator let go before the end ends its process, as the layer's own runner does.
            if (!ended) Kill(process);
        }

        var timedOut = bound?.IsCancellationRequested == true && !ct.IsCancellationRequested;
        lock (_gate) _exit = timedOut ? null : process.ExitCode;
        if (timedOut) throw new ProcessTimeoutException(command, patience!.Value);
        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new ProcessRunException(command, process.ExitCode, Tail());
    }

    private async Task<Process> StartAsync(
        string command, IReadOnlyList<string> args, string? workingDirectory, IReadOnlyDictionary<string, string?>? environment,
        CancellationToken ct)
    {
        // Not the tools' environment (TOOLS5): the start's environment is the one its shell composed before the layer's door was registered
        // (the agent-lyntai design §5.5), the tools' among it, handed whole in the seat.
        var info = new ProcessStartInfo(seat.Program?.File ?? command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in (IEnumerable<string>?)seat.Program?.Head ?? []) info.ArgumentList.Add(arg);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        if (workingDirectory is { Length: > 0 }) info.WorkingDirectory = workingDirectory;
        Apply(info, seat.Environment);
        if (environment is not null) Apply(info, environment);
        lock (_gate)
        {
            _exit = null;
            _tail.Clear();
        }

        var process = await AgentStarts.StartAsync(info, seat.Kind, seat.Whose, seat.Home, seat.Log, ct).ConfigureAwait(false)
            ?? throw new ProcessRunException(command, -1, "the start gate refused the start");
        bool stopped;
        lock (_gate)
        {
            _process = process;
            stopped = _stopped;
        }

        seat.Started(process);
        if (stopped) Kill(process);
        return process;
    }

    /// <summary>A null value removes an inherited variable; an empty one sets it empty (the layer's runner contract).</summary>
    private static void Apply(ProcessStartInfo info, IReadOnlyDictionary<string, string?> environment)
    {
        foreach (var (name, value) in environment)
        {
            if (value is null) info.Environment.Remove(name);
            else info.Environment[name] = value;
        }
    }

    private async Task ErrorsAsync(Process process)
    {
        while (await process.StandardError.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            seat.Heard();
            lock (_gate)
            {
                _tail.Enqueue(line);
                if (_tail.Count > TailKept) _tail.Dequeue();
            }

            seat.Error(line);
        }
    }

    private Func<Process, CancellationToken, Task> Fixed(string? stdin) => async (process, ct) =>
    {
        try
        {
            if (stdin is not null)
            {
                lock (_gate) _written.Add(stdin);
                await process.StandardInput.WriteAsync(stdin.AsMemory(), ct).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
                seat.Wrote();
            }

            process.StandardInput.Close();
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The process ended before it read its stdin: its exit says what happened.
        }
    };

    private Func<Process, CancellationToken, Task> Framed(IAsyncEnumerable<string> lines) => async (process, ct) =>
    {
        try
        {
            await foreach (var line in lines.WithCancellation(ct).ConfigureAwait(false))
            {
                lock (_gate) _written.Add(line);
                await process.StandardInput.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
            }

            process.StandardInput.Close();
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The process ended while it was spoken to: its exit says what happened.
        }
    };

    private string Tail()
    {
        lock (_gate) return string.Join('\n', _tail);
    }

    private static void Kill(Process? process)
    {
        try
        {
            if (process is { HasExited: false }) OwnedProcesses.End(process);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Ended already, or never started.
        }
    }
}
