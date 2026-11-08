using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What the relay asks of the machine, and the bounds it keeps: each the machine's but in a test, which holds the clock.</summary>
public sealed class RelayWorld
{
    /// <summary>Every wait the relay measures: the first line's, the batch's gathering and the bound.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>The home's lease, read: its live holder, and whether the read is a doubt (busy, or a newer build's), which counts as held.</summary>
    public Func<string, (LeaseHolder? Holder, bool Doubt)> Lease { get; init; } = home => (HomeLease.HeldBy(home, out var doubt), doubt);

    /// <summary>Whether the holder's agents' job holds the client's process: null where the job or the process would not open.</summary>
    public Func<LeaseHolder, int, bool?> Contained { get; init; } = RelayRun.InAgentsJob;

    /// <summary>The session's process ending, waited on and never polled.</summary>
    public Func<int, CancellationToken, Task> SessionExit { get; init; } = RelayRun.ExitOfAsync;

    /// <summary>How long a client has to send its first line.</summary>
    public TimeSpan FirstLine { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a batch gathers once its first line is kept.</summary>
    public TimeSpan Gather { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long the relay waits with no client connected before it reads the lease.</summary>
    public TimeSpan Bound { get; init; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// One relay's life: its subscriber's file put in place whole, its pipe served, each
/// client checked (a stranger the holder's agents' job does not hold refused, one it cannot check taken untold, the first line's schema
/// always), one plugin connected and a second that passes ending it, every frame checked by the grammar into the batch, and an end for
/// every reason, each said: the batch gathered, the connection ended, three bad frames, another build, a second client, the bound while an
/// instance holds the home, or the session gone, which prints nothing. It opens no door of Daoris's and starts no process.
/// </summary>
public sealed class RelayRun
{
    private const int BadInARow = 3;

    private readonly object _gate = new();
    private readonly TaskCompletionSource<bool> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _home;
    private readonly RelayWorld _world;
    private readonly RelayBatch _batch;
    private bool _connected;
    private bool _refused;
    private bool _notContained;
    private bool _unchecked;
    private bool _gathering;

    private RelayRun(string home, RelayAsk ask, string build, RelayWorld world) =>
        (_home, _world, _batch) = (home, world, new RelayBatch(ask.Since, ask.Kinds, build, ask.NotBy));

    /// <summary>The subscribers' folder in <paramref name="home"/>, every folder down to it plain.</summary>
    /// <exception cref="RelayStop">A folder on the way is a link (<c>home</c>).</exception>
    public static string Subscribers(string home)
    {
        var plugins = DaorisHome.Entry(home, HomeEntry.Plugins);
        var data = Path.Combine(plugins, PluginCatalog.DataFolder);
        var plugin = Path.Combine(data, RelayChecks.Plugin);
        var subscribers = Path.Combine(plugin, "subscribers");
        if (new[] { plugins, data, plugin, subscribers }.Any(folder => !RelayCopy.Plain(folder))) throw new RelayStop("home");
        Directory.CreateDirectory(subscribers);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(subscribers, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return subscribers;
    }

    /// <summary>The relay, run to its end in <paramref name="home"/> from the copy of <paramref name="build"/>: its batch printed, exit 0.</summary>
    public static async Task<int> RunAsync(string home, RelayAsk ask, string build, TextWriter output, TextWriter error, RelayWorld world, CancellationToken ct = default)
    {
        var subscribers = Subscribers(home);
        RemoveDead(subscribers);
        var run = new RelayRun(home, ask, build, world);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        RelayPipe pipe;
        try
        {
            pipe = RelayPipe.Serve(subscribers);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Net.Sockets.SocketException)
        {
            throw new RelayStop("pipe");
        }

        await using (pipe)
        {
            var file = Announce(subscribers, pipe.Name);
            try
            {
                var accepting = run.AcceptAsync(pipe, stop.Token);
                var bound = run.BoundAsync(stop.Token);
                if (ask.SessionPid is { } session) _ = run.SessionAsync(session, stop.Token);
                var print = await run._done.Task.WaitAsync(ct).ConfigureAwait(false);
                await stop.CancelAsync().ConfigureAwait(false);
                if (!print) return 0;
                lock (run._gate)
                {
                    var (said, tallied) = run._batch.Printed();
                    foreach (var line in tallied) error.WriteLine(line);
                    if (run._unchecked) error.WriteLine(RelaySaid.Unchecked);
                    if (run._notContained) error.WriteLine(RelaySaid.NotContained);
                    foreach (var line in said) output.WriteLine(line);
                }

                return 0;
            }
            finally
            {
                await stop.CancelAsync().ConfigureAwait(false);
                File.Delete(file);
            }
        }
    }

    /// <summary>
    /// Whether the agents' job of the instance <paramref name="holder"/> names holds <paramref name="client"/>, the job opened by its name for
    /// query alone: null where the client's process or the job would not open, as across elevations, or off Windows.
    /// </summary>
    public static bool? InAgentsJob(LeaseHolder holder, int client)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var process = OpenProcess(QueryLimited, false, (uint)client);
        if (process == IntPtr.Zero) return null;
        try
        {
            return ProcessJob.NamedHolds(AgentJobs.NameOf(holder.Pid), process);
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>A process's exit, waited on; at once where it is already gone.</summary>
    public static async Task ExitOfAsync(int pid, CancellationToken ct)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return;
        }

        using (process) await process.WaitForExitAsync(ct).ConfigureAwait(false);
    }

    // A file a dead relay left, its name and its pid agreeing on a process that is not alive; no other is touched.
    private static void RemoveDead(string subscribers)
    {
        foreach (var file in Directory.EnumerateFiles(subscribers, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!name.All(char.IsAsciiDigit) || !int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)) continue;
            try
            {
                if (new FileInfo(file).Length > 1024) continue;
                using var read = JsonDocument.Parse(AtomicFile.ReadText(file));
                if (!read.RootElement.TryGetProperty("pid", out var named) || !named.TryGetInt32(out var said) || said != pid) continue;
                if (!Alive(pid)) File.Delete(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                // Not a relay's file, or held: left.
            }
        }
    }

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return error is System.ComponentModel.Win32Exception;
        }
    }

    // The subscriber's file, written under another name and moved in with no overwrite, so the plugin reads it whole.
    private static string Announce(string subscribers, string pipe)
    {
        var pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var file = Path.Combine(subscribers, pid + ".json");
        var staged = Path.Combine(subscribers, "." + pid + ".json");
        File.WriteAllText(staged, JsonSerializer.Serialize(new { pipe, pid = Environment.ProcessId }), new UTF8Encoding(false));
        File.Move(staged, file, overwrite: false);
        return file;
    }

    private void Finish(bool print) => _done.TrySetResult(print);

    private async Task AcceptAsync(RelayPipe pipe, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            RelayClient client;
            try
            {
                client = await pipe.AcceptAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                lock (_gate) _batch.End((_, _) => RelaySaid.Stopped("pipe"));
                Finish(true);
                return;
            }

            _ = ServeAsync(client, ct);
        }
    }

    private async Task ServeAsync(RelayClient client, CancellationToken ct)
    {
        var connected = false;
        try
        {
            if (!Checked(client)) return;
            var lines = new RelayLines(client.Stream);
            using var timed = new CancellationTokenSource(_world.FirstLine, _world.Clock);
            using var first = CancellationTokenSource.CreateLinkedTokenSource(ct, timed.Token);
            var (line, _) = await lines.ReadAsync(RelayChecks.FirstLineBytes, first.Token).ConfigureAwait(false);
            if (line is null || RelayChecks.First(line) is not { } said)
            {
                Refuse();
                return;
            }

            lock (_gate)
            {
                if (_connected)
                {
                    _batch.End((run, seq) => RelaySaid.SecondClient(run, seq));
                    Finish(true);
                    return;
                }

                _connected = connected = true;
                _batch.First(said);
                if (_batch.Ends)
                {
                    Finish(true);
                    return;
                }
            }

            await FramesAsync(lines, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Refuse();
        }
        catch (Exception error) when (error is OperationCanceledException or IOException or ObjectDisposedException)
        {
            if (connected) End();
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    // The strangers it can see. The lease names an instance, and that instance's agents' job holds the client; where it cannot
    // be asked, the client is taken untold, its first line still checked.
    private bool Checked(RelayClient client)
    {
        if (client.Pid is not { } pid)
        {
            lock (_gate)
            {
                _unchecked = true;
                _batch.Tally("unchecked");
            }

            return true;
        }

        var (holder, _) = _world.Lease(_home);
        if (holder is not { IsInstance: true })
        {
            Refuse();
            return false;
        }

        switch (_world.Contained(holder, pid))
        {
            case true:
                return true;
            case false:
                lock (_gate) _notContained = true;
                Refuse();
                return false;
            default:
                lock (_gate)
                {
                    _unchecked = true;
                    _batch.Tally("unchecked");
                }

                return true;
        }
    }

    // Every frame checked by the grammar; three bad in a row close the connection, which ends the relay with the gap line.
    private async Task FramesAsync(RelayLines lines, CancellationToken ct)
    {
        var bad = 0;
        while (true)
        {
            var (line, ended) = await lines.ReadAsync(RelayChecks.FrameBytes, ct).ConfigureAwait(false);
            if (ended) break;
            var frame = line is null ? null : RelayChecks.Frame(line, WorkEventPlugins.Adapters);
            lock (_gate)
            {
                if (_done.Task.IsCompleted) return;
                if (frame is null)
                {
                    _batch.Tally("dropped");
                    if (++bad == BadInARow) break;
                    continue;
                }

                bad = 0;
                _batch.Frame(frame);
                if (_batch.Opened && !_gathering)
                {
                    _gathering = true;
                    _ = GatherAsync(ct);
                }
            }
        }

        End();
    }

    private void End()
    {
        lock (_gate)
        {
            // Once the batch is decided, the read cancelled to stop it is no loss: it must not add a gap to what is printed.
            if (_done.Task.IsCompleted) return;
            _batch.End();
        }

        Finish(true);
    }

    private void Refuse()
    {
        lock (_gate)
        {
            _refused = true;
            _batch.Tally("refused");
        }
    }

    private async Task GatherAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_world.Gather, _world.Clock, ct).ConfigureAwait(false);
            Finish(true);
        }
        catch (OperationCanceledException)
        {
            // Ended already.
        }
    }

    // The bound: with no client connected, the lease read once each bound; an instance holding the home, or a doubt, ends the relay
    // saying why no events reached it, and a closed Daoris costs one read and no wake.
    private async Task BoundAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_world.Bound, _world.Clock, ct).ConfigureAwait(false);
                lock (_gate)
                {
                    if (_connected) return;
                }

                var (holder, doubt) = _world.Lease(_home);
                if (holder is not { IsInstance: true } && !doubt) continue;
                lock (_gate)
                {
                    var reason = _notContained ? "not-contained" : _refused ? "refused" : "no-client";
                    _batch.End((run, seq) => RelaySaid.NoEvents(reason, run, seq));
                }

                Finish(true);
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // Ended another way.
        }
    }

    private async Task SessionAsync(int pid, CancellationToken ct)
    {
        try
        {
            await _world.SessionExit(pid, ct).ConfigureAwait(false);
            Finish(false);
        }
        catch (OperationCanceledException)
        {
            // Ended another way.
        }
    }

    private const uint QueryLimited = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>
/// Lines read from a client's stream, each to a bound: a longer one is read no further and answered as none, its rest skipped to its end,
/// so nothing past the bound is ever held.
/// </summary>
internal sealed class RelayLines(Stream stream)
{
    private readonly byte[] _buffer = new byte[8192];
    private int _at;
    private int _length;

    /// <summary>The next line, null where it ran past <paramref name="bound"/> bytes; <c>Ended</c> where the stream ended first.</summary>
    public async Task<(string? Line, bool Ended)> ReadAsync(int bound, CancellationToken ct)
    {
        var line = new MemoryStream();
        var over = false;
        while (true)
        {
            if (_at == _length)
            {
                _length = await stream.ReadAsync(_buffer, ct).ConfigureAwait(false);
                _at = 0;
                if (_length == 0) return (null, true);
            }

            var end = Array.IndexOf(_buffer, (byte)'\n', _at, _length - _at);
            var upTo = end < 0 ? _length : end;
            if (!over)
            {
                line.Write(_buffer, _at, upTo - _at);
                if (line.Length > bound) over = true;
            }

            _at = end < 0 ? _length : end + 1;
            if (end < 0) continue;
            if (over) return (null, false);
            var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length);
            return (text.EndsWith('\r') ? text[..^1] : text, false);
        }
    }
}
