using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>
/// One process the host started: both streams read from its start into one channel, so neither pipe fills while the application
/// reads the other, and its end awaited after both streams close. A run disposed before its end is stopped, its tree with it.
/// </summary>
internal sealed class MachineRun : IChildRun
{
    private readonly Process _process;
    private readonly Channel<ProcessOutput> _output = Channel.CreateUnbounded<ProcessOutput>(new() { SingleReader = true });
    private readonly Task _read;
    private int _stopped;

    internal MachineRun(Process process, bool input)
    {
        _process = process;
        Id = process.Id;
        Created = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        if (!input) process.StandardInput.Close();
        _read = Task.WhenAll(ReadAsync(process.StandardOutput, ProcessStream.Output), ReadAsync(process.StandardError, ProcessStream.Error))
            .ContinueWith(_ => _output.Writer.TryComplete(), TaskScheduler.Default);
    }

    public int Id { get; }

    public DateTimeOffset Created { get; }

    private async Task ReadAsync(StreamReader reader, ProcessStream stream)
    {
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            _output.Writer.TryWrite(new ProcessOutput(stream, new string(buffer, 0, read)));
        }
    }

    public async IAsyncEnumerable<ProcessOutput> Output([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var piece in _output.Reader.ReadAllAsync(ct).ConfigureAwait(false)) yield return piece;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken ct)
    {
        await _process.StandardInput.BaseStream.WriteAsync(input, ct).ConfigureAwait(false);
        await _process.StandardInput.BaseStream.FlushAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<ProcessEnd> EndAsync(CancellationToken ct)
    {
        await _process.WaitForExitAsync(ct).ConfigureAwait(false);
        await _read.WaitAsync(ct).ConfigureAwait(false);
        return new ProcessEnd(_process.ExitCode, Volatile.Read(ref _stopped) == 1);
    }

    public async ValueTask StopAsync(CancellationToken ct)
    {
        try
        {
            _process.Kill(entireProcessTree: true);
            Volatile.Write(ref _stopped, 1);
        }
        catch (InvalidOperationException)
        {
            // It had exited: nothing is left to end.
        }

        await _process.WaitForExitAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited) await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _process.Dispose();
    }
}
