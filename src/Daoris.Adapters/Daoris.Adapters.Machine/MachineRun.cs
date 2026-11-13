using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>
/// One process the host started: its streams read from its start into channels, so neither pipe fills while the application reads the
/// other, and its end awaited after its streams close or a moment after its exit where a process it started holds one open. A run
/// disposed before its end is stopped, its tree with it.
/// </summary>
internal sealed class MachineRun : IChildRun
{
    /// <summary>How long a run's streams are waited for once its process has exited: a descendant that inherited a pipe holds it open past the exit.</summary>
    internal static readonly TimeSpan PipeGrace = TimeSpan.FromSeconds(2);

    /// <summary>How long a stop waits for the exit it asked for.</summary>
    internal static readonly TimeSpan ExitBound = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly Channel<ProcessOutput> _output = Channel.CreateUnbounded<ProcessOutput>(new() { SingleReader = true });
    private readonly Channel<ReadOnlyMemory<byte>> _bytes = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new() { SingleReader = true });
    private readonly Task _read;
    private int _stopped;

    internal MachineRun(Process process, bool input, bool raw)
    {
        _process = process;
        Id = process.Id;
        Created = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        if (!input) process.StandardInput.Close();
        _read = Task.WhenAll(raw ? ReadBytesAsync(process.StandardOutput.BaseStream) : ReadAsync(process.StandardOutput, ProcessStream.Output), ReadAsync(process.StandardError, ProcessStream.Error))
            .ContinueWith(_ => Complete(), TaskScheduler.Default);
    }

    public int Id { get; }

    public DateTimeOffset Created { get; }

    private void Complete()
    {
        _output.Writer.TryComplete();
        _bytes.Writer.TryComplete();
    }

    private async Task ReadAsync(StreamReader reader, ProcessStream stream)
    {
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            _output.Writer.TryWrite(new ProcessOutput(stream, new string(buffer, 0, read)));
        }
    }

    private async Task ReadBytesAsync(Stream stream)
    {
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            _bytes.Writer.TryWrite(buffer.AsMemory(0, read).ToArray());
        }
    }

    public async IAsyncEnumerable<ProcessOutput> Output([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var piece in _output.Reader.ReadAllAsync(ct).ConfigureAwait(false)) yield return piece;
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> Bytes([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var piece in _bytes.Reader.ReadAllAsync(ct).ConfigureAwait(false)) yield return piece;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken ct)
    {
        await _process.StandardInput.BaseStream.WriteAsync(input, ct).ConfigureAwait(false);
        await _process.StandardInput.BaseStream.FlushAsync(ct).ConfigureAwait(false);
    }

    public ValueTask CloseInputAsync()
    {
        try
        {
            _process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The pipe closed under it: the process is ending, and its own exit says so.
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<ProcessEnd> EndAsync(CancellationToken ct)
    {
        await _process.WaitForExitAsync(ct).ConfigureAwait(false);
        await Task.WhenAny(_read, Task.Delay(PipeGrace, ct)).ConfigureAwait(false);
        Complete();
        return new ProcessEnd(_process.ExitCode, Volatile.Read(ref _stopped) == 1);
    }

    public async ValueTask<string?> StopAsync(CancellationToken ct)
    {
        string? refused = null;
        try
        {
            _process.Kill(entireProcessTree: true);
            Volatile.Write(ref _stopped, 1);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException or AggregateException)
        {
            // It had exited, or a member of its tree refused: the exit wait says which.
            refused = error.Message;
        }

        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(ExitBound);
        try
        {
            await _process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var seconds = ExitBound.TotalSeconds.ToString("0");
            return refused is null
                ? $"no exit within {seconds} s of its kill"
                : $"its kill was refused ({refused}) and it did not exit within {seconds} s";
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited) await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _process.Dispose();
    }
}
