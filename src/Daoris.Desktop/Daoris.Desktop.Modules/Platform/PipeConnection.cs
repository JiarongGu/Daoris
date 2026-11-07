using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// One terminal's connection to the <see cref="InstancePipe"/>: its hello, then its requests. What is answered at once is written in
/// order before the next line is taken, so a terminal that sends on without reading holds its own lines back; a press is answered as it
/// ends, at most <see cref="PipeLimits.InFlight"/> at once, and lines sent past <see cref="PipeLimits.Queued"/> close the connection.
/// </summary>
internal sealed class PipeConnection(InstancePipe pipe, NamedPipeServerStream stream)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly SemaphoreSlim _writing = new(1, 1);
    private readonly Channel<string> _lines = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(1, pipe.Limits.Queued))
    {
        SingleReader = true,
        SingleWriter = true,
    });

    private int _underWay;
    private int _closed;

    /// <summary>
    /// The terminal at the far end, as the system names the pipe's client: what the window's card names as the asker,
    /// read once as the connection is taken, never from what the terminal says of itself.
    /// </summary>
    public PipeAsker Asker { get; } = AskerOf(stream);

    /// <summary>
    /// The terminal's process, held by a handle opened as the connection is taken and kept for its life : what the standing confirmation's judge asks the jobs about.
    /// </summary>
    public PipeClientProcess Client { get; } = PipeClientProcess.Of(stream);

    // This connection's hello's nonce and when it is drawn, in memory for this connection alone.
    private readonly Lock _nonceGate = new();
    private string? _nonce;
    private long _nonceAt;

    /// <summary>How long a hello's nonce may be used: forgotten at its first use, at the connection's end or after this.</summary>
    public static readonly TimeSpan NonceLife = TimeSpan.FromSeconds(60);

    /// <summary>A fresh nonce for this connection's hello, replacing any it drew before.</summary>
    public string DrawNonce()
    {
        lock (_nonceGate)
        {
            _nonce = SupervisorProof.Nonce();
            _nonceAt = Environment.TickCount64;
            return _nonce;
        }
    }

    /// <summary>The hello's nonce for one act, forgotten as it is taken: null where none was drawn, it was used, or its minute passed.</summary>
    public string? TakeNonce()
    {
        lock (_nonceGate)
        {
            var nonce = _nonce;
            var fresh = nonce is not null && Environment.TickCount64 - _nonceAt <= (long)NonceLife.TotalMilliseconds;
            _nonce = null;
            return fresh ? nonce : null;
        }
    }

    /// <summary>Whether a press this connection sent has not been answered yet.</summary>
    public bool Answering => Volatile.Read(ref _underWay) > 0;

    /// <summary>Whether this connection may have one more press under way.</summary>
    public bool HasRoom => Volatile.Read(ref _underWay) < pipe.Limits.InFlight;

    public async Task RunAsync()
    {
        try
        {
            _ = ReadAsync();
            // The hello first, within its bound: anything else closes the connection unanswered.
            if (await NextAsync(pipe.Limits.Hello, waitWhileBusy: false).ConfigureAwait(false) is not { } first
                || Parse(first) is not ({ } hello, _, _)
                || !string.Equals(hello.Module, InstancePipe.Module, StringComparison.OrdinalIgnoreCase) || hello.Type != "HELLO")
            {
                return;
            }

            var answered = pipe.Hello(hello, this);
            await WriteAsync(IpcJson.Serialize(answered)).ConfigureAwait(false);
            if (!answered.Success) return;

            while (await NextAsync(pipe.Limits.Idle, waitWhileBusy: true).ConfigureAwait(false) is { } line)
            {
                if (Parse(line) is not ({ } request, var key, var hold)) return;
                var (now, later) = pipe.Accept(request, key, this, hold);
                if (now is not null)
                {
                    await WriteAsync(now).ConfigureAwait(false);
                }
                else
                {
                    // Counted from here, on this loop alone, so the room asked above is the room taken.
                    Interlocked.Increment(ref _underWay);
                    _ = AnswerLaterAsync(later!);
                }
            }
        }
        finally
        {
            Close();
            pipe.Ended(this);
        }
    }

    private async Task AnswerLaterAsync(Task<string> answer)
    {
        try
        {
            await WriteAsync(await answer.ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _underWay);
        }
    }

    /// <summary>The next line, or null: the connection closed, a line too long, or nothing said within <paramref name="within"/>.</summary>
    private async Task<string?> NextAsync(TimeSpan within, bool waitWhileBusy)
    {
        while (true)
        {
            // A connection closed under it (it ran past its lines' bound, or the instance ends) takes no more of them.
            if (Volatile.Read(ref _closed) == 1) return null;
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(pipe.Lifetime.Token);
            bound.CancelAfter(within);
            try
            {
                return await _lines.Reader.ReadAsync(bound.Token).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
            catch (OperationCanceledException) when (!pipe.Lifetime.IsCancellationRequested)
            {
                // Idle is a connection with nothing under way: one waiting on a long press is not, nor one holding a conversation.
                if (waitWhileBusy && (Volatile.Read(ref _underWay) > 0 || pipe.Holding.HeldBy(this) is not null)) continue;
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }

    /// <summary>Reads the connection's bytes into lines, bounded in length; ends the lines where it closes or a line runs over.</summary>
    private async Task ReadAsync()
    {
        var buffer = new byte[64 * 1024];
        using var line = new MemoryStream();
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, pipe.Lifetime.Token).ConfigureAwait(false);
                if (read == 0) break;
                var start = 0;
                for (var at = 0; at < read; at++)
                {
                    if (buffer[at] != (byte)'\n') continue;
                    line.Write(buffer, start, at - start);
                    start = at + 1;
                    if (line.Length > pipe.Limits.LineBytes) return;
                    // Lines past the bound pile up only where nothing it was answered is read: the connection ends.
                    if (!_lines.Writer.TryWrite(Utf8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r')))
                    {
                        Close();
                        return;
                    }

                    line.SetLength(0);
                }

                line.Write(buffer, start, read - start);
                if (line.Length > pipe.Limits.LineBytes) return;
            }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Closed under it: the lines end here.
        }
        finally
        {
            _lines.Writer.TryComplete();
        }
    }

    /// <summary>
    /// The kit's envelope on a line, the <c>key</c> beside it (its sender's secret, which binds the id),
    /// and <c>hold</c> beside it (a start that holds what it opens); null where the line is no request.
    /// </summary>
    private static (IpcRequest Request, string? Key, bool Hold)? Parse(string line)
    {
        try
        {
            if (IpcJson.Deserialize<IpcRequest>(line) is not { Module: { Length: > 0 }, Type: { Length: > 0 } } request) return null;
            using var document = JsonDocument.Parse(line);
            var key = document.RootElement.TryGetProperty("key", out var named) && named.ValueKind == JsonValueKind.String
                ? named.GetString()
                : null;
            var hold = document.RootElement.TryGetProperty("hold", out var held) && held.ValueKind == JsonValueKind.True;
            return (request, key, hold);
        }
        catch (Exception error) when (error is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>One line to the terminal, whole; nothing where it has gone.</summary>
    public async Task WriteAsync(string json)
    {
        if (Volatile.Read(ref _closed) == 1) return;
        var bytes = Utf8.GetBytes(json + "\n");
        try
        {
            await _writing.WaitAsync().ConfigureAwait(false);
            try
            {
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _writing.Release();
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The terminal has gone: its press runs on, and RESULT says how it ended.
        }
    }

    /// <summary>
    /// Closes this end. Not a disconnect, which drops what the terminal has not read yet: a refusal written just before still
    /// reaches it, and it then reads the end.
    /// </summary>
    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1) return;
        TakeNonce();
        stream.Dispose();
        Client.Dispose();
    }

    private static PipeAsker AskerOf(NamedPipeServerStream stream)
    {
        if (!OperatingSystem.IsWindows()) return new PipeAsker(null, null);
        try
        {
            if (!GetNamedPipeClientProcessId(stream.SafePipeHandle, out var id)) return new PipeAsker(null, null);
            var pid = (int)id;
            try
            {
                using var process = Process.GetProcessById(pid);
                return new PipeAsker(process.ProcessName, pid);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Gone already, or not ours to read: the card names its process id alone.
                return new PipeAsker(null, pid);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or EntryPointNotFoundException)
        {
            return new PipeAsker(null, null);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint clientProcessId);
}
