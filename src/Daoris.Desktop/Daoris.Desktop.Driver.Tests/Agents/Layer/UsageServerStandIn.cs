using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Lyntai.Processes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>How the stand-in answers <c>initialize</c>: at once, only after a pause (any later line arriving first ending it), or with an error.</summary>
internal enum UsageServerMode
{
    Answers,
    WithholdsInitialize,
    RefusesInitialize,
}

/// <summary>
/// One usage server for both reads (the agent-lyntai design §9.2, 1c): Claude Code's control requests or Codex's app server, answering its
/// usage request with a fixture's body, line by line in the order it hears them. A line heard while it withholds <c>initialize</c>'s answer
/// ends it, so a read that writes ahead of the protocol's order reads nothing.
/// </summary>
internal sealed class UsageServerStandIn
{
    private readonly Channel<string> _in = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _out = Channel.CreateUnbounded<string>();
    private readonly List<string> _heard = [];
    private readonly bool _claude;
    private readonly string _body;
    private readonly UsageServerMode _mode;
    private int _withholding;

    public UsageServerStandIn(bool claude, string body, UsageServerMode mode = UsageServerMode.Answers)
    {
        _claude = claude;
        // One line on the wire, every number as the fixture wrote it.
        using (var document = JsonDocument.Parse(body)) _body = JsonSerializer.Serialize(document.RootElement);
        _mode = mode;
        _ = Task.Run(LoopAsync);
    }

    public IReadOnlyList<string> Heard
    {
        get
        {
            lock (_heard) return [.. _heard];
        }
    }

    public void Hear(string line) => _in.Writer.TryWrite(line);

    public void Close() => _in.Writer.TryComplete();

    public ChannelReader<string> Said => _out.Reader;

    private async Task LoopAsync()
    {
        await foreach (var line in _in.Reader.ReadAllAsync())
        {
            lock (_heard) _heard.Add(line);
            if (Volatile.Read(ref _withholding) == 1) break;
            var initialize = IsInitialize(line);
            if (initialize && _mode == UsageServerMode.WithholdsInitialize)
            {
                Volatile.Write(ref _withholding, 1);
                _ = Task.Delay(150).ContinueWith(_ =>
                {
                    Volatile.Write(ref _withholding, 0);
                    _out.Writer.TryWrite(Initialized(ok: true));
                }, TaskScheduler.Default);
                continue;
            }

            if (initialize)
            {
                _out.Writer.TryWrite(Initialized(ok: _mode != UsageServerMode.RefusesInitialize));
            }
            else if (IsUsage(line))
            {
                _out.Writer.TryWrite(_claude
                    ? $$$"""{"type":"control_response","response":{"subtype":"success","request_id":"usage","response":{{{_body}}}}}"""
                    : $$$"""{"id":2,"result":{{{_body}}}}""");
            }
        }

        _out.Writer.TryComplete();
    }

    private string Initialized(bool ok) => (_claude, ok) switch
    {
        (true, true) => """{"type":"control_response","response":{"subtype":"success","request_id":"init","response":{}}}""",
        (true, false) => """{"type":"control_response","response":{"subtype":"error","request_id":"init","error":"refused"}}""",
        (false, true) => """{"id":1,"result":{"userAgent":"stand-in"}}""",
        _ => """{"id":1,"error":{"code":-32603,"message":"refused"}}""",
    };

    private bool IsInitialize(string line) => _claude ? Names(line, "request_id", "init") : Names(line, "method", "initialize");

    private bool IsUsage(string line) => _claude ? Names(line, "request_id", "usage") : Names(line, "method", "account/rateLimits/read");

    private static bool Names(string line, string name, string value)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String && found.GetString() == value;
    }
}

/// <summary>Today's read's two streams onto the stand-in: what it writes is heard, what the stand-in says is read.</summary>
internal sealed class UsageServerStreams(UsageServerStandIn server)
{
    public TextReader Incoming { get; } = new Reader(server);

    public TextWriter Outgoing { get; } = new Writer(server);

    private sealed class Reader(UsageServerStandIn server) : TextReader
    {
        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (await server.Said.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (server.Said.TryRead(out var line)) return line;
            }

            return null;
        }

        public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();
    }

    private sealed class Writer(UsageServerStandIn server) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            if (value is not null) server.Hear(value);
        }

        public override Task WriteLineAsync(string? value)
        {
            WriteLine(value);
            return Task.CompletedTask;
        }

        public override Task FlushAsync() => Task.CompletedTask;
    }
}

/// <summary>The layer's runner onto the stand-in: each spawn as the runner was asked it, the stdin lines heard as they arrive.</summary>
internal sealed class UsageServerRunner(UsageServerStandIn server) : IProcessRunner
{
    private readonly List<UsageSpawned> _spawns = [];

    public IReadOnlyList<UsageSpawned> Spawns
    {
        get
        {
            lock (_spawns) return [.. _spawns];
        }
    }

    public bool CommandExists(string command) => true;

    public Task<ProcessResult> RunAsync(
        string command, IReadOnlyList<string> args, string? stdin = null, TimeSpan? inactivityTimeout = null, TimeSpan? maxDuration = null,
        string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken ct = default)
    {
        lock (_spawns) _spawns.Add(new(command, args, workingDirectory, environment));
        return Task.FromResult(new ProcessResult(1, "", "a usage read probes no version"));
    }

    public IAsyncEnumerable<string> StreamLinesAsync(
        string command, IReadOnlyList<string> args, string? stdin = null, TimeSpan? inactivityTimeout = null, TimeSpan? maxDuration = null,
        string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null, CancellationToken ct = default) =>
        throw new NotSupportedException("a usage read exchanges its lines");

    public async IAsyncEnumerable<string> ExchangeLinesAsync(
        string command, IReadOnlyList<string> args, IAsyncEnumerable<string> stdinLines, TimeSpan? inactivityTimeout = null,
        TimeSpan? maxDuration = null, string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environment = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        lock (_spawns) _spawns.Add(new(command, args, workingDirectory, environment));
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var line in stdinLines.WithCancellation(ct).ConfigureAwait(false)) server.Hear(line);
            }
            catch (OperationCanceledException)
            {
                // the read ended its own stdin
            }
            finally
            {
                server.Close();
            }
        }, CancellationToken.None);
        await foreach (var line in server.Said.ReadAllAsync(ct).ConfigureAwait(false)) yield return line;
    }
}

/// <summary>One spawn the layer's runner was asked.</summary>
internal sealed record UsageSpawned(
    string Command, IReadOnlyList<string> Args, string? WorkingDirectory, IReadOnlyDictionary<string, string?>? Environment);
