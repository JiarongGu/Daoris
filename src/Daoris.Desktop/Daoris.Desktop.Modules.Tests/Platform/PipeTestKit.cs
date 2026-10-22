using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A module whose every route waits until the test lets it, and counts how often it ran (RPC1a): a press the pipe's tests hold
/// under way. <c>PROGRESS</c> reports half its work first, as a route reports through the kit's tracker.
/// </summary>
internal sealed class HeldModule(string name) : ModuleBase
{
    private int _ran;

    public override string ModuleName => name;

    public int Ran => Volatile.Read(ref _ran);

    public TaskCompletionSource Let { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        var ran = Interlocked.Increment(ref _ran);
        if (request.Type == "PROGRESS") context.Report(new IpcProgress(1, 2, "steps"), new IpcLabel("half way"));
        Started.TrySetResult();
        await Let.Task.WaitAsync(cancellationToken);
        return new { Done = true, Said = $"pressed {request.Type} ({ran})" };
    }
}

/// <summary>
/// A clock that stands still until the test moves it (RPC1c): a timer it made fires only once <see cref="Advance"/> passes its
/// time, so a confirmation's two minutes are the test's to spend, never the machine's load.
/// </summary>
internal sealed class HeldClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<HeldTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new HeldTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="by"/>, firing each timer whose time it reached, once.</summary>
    public void Advance(TimeSpan by)
    {
        List<HeldTimer> due;
        lock (_gate)
        {
            _now += by;
            due = [.. _timers.Where(timer => timer.Due is { } at && at <= _now)];
            foreach (var timer in due) timer.Due = null;
        }

        foreach (var timer in due) timer.Fire();
    }

    /// <summary>
    /// Moves the clock on by <paramref name="by"/> and fires nothing: each timer it reached is held late, as a loaded pool runs one
    /// (NOTIFY2a), and fires at the next <see cref="Advance"/>.
    /// </summary>
    public void Pass(TimeSpan by)
    {
        lock (_gate) _now += by;
    }

    private sealed class HeldTimer(HeldClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + dueTime;
                if (!clock._timers.Contains(this)) clock._timers.Add(this);
            }

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (clock._gate) clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>A terminal's end of the running Daoris's pipe (RPC1a), as RPC1b's client will hold it: lines of JSON each way.</summary>
internal sealed class PipeClient : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly NamedPipeClientStream _stream;
    private readonly StreamReader _reader;
    private readonly List<JsonElement> _notifications = [];
    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _answers = new(StringComparer.Ordinal);

    private PipeClient(NamedPipeClientStream stream)
    {
        _stream = stream;
        _reader = new StreamReader(stream, Utf8);
    }

    /// <summary>
    /// Connected as the driver's client connects (PIPEOWNER1): past the ACL, then the pipe's owner held to this user's SIDs, never
    /// <see cref="PipeOptions.CurrentUserOnly"/>, which an elevated run refuses the user's own pipe by.
    /// </summary>
    public static async Task<PipeClient> ConnectAsync(string name)
    {
        var stream = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await stream.ConnectAsync(5000);
        if (OperatingSystem.IsWindows()) Assert.True(Daoris.Driver.InstanceClient.OwnedBy(stream, Daoris.Driver.InstanceClient.ThisUser()));
        return new PipeClient(stream);
    }

    public async Task<JsonElement> HelloAsync(string home, int protocol = 1)
    {
        var id = await SendAsync(InstancePipe.Module, "HELLO", new { protocol, home, client = "tests", version = "0" });
        return await AnswerAsync(id);
    }

    /// <summary><c>RESULT</c> for <paramref name="id"/>, with the key it was sent with (this client's, unless one is named), answered.</summary>
    public async Task<JsonElement> ResultAsync(string id, string? key = null)
    {
        var answer = await ResultAnswerAsync(id, key);
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        return answer.GetProperty("data");
    }

    /// <summary><c>RESULT</c>'s whole answer, a refusal included.</summary>
    public async Task<JsonElement> ResultAnswerAsync(string id, string? key = null) =>
        await AnswerAsync(await SendAsync(InstancePipe.Module, "RESULT", new { id, key = key ?? KeyOf(id) }));

    /// <summary>
    /// One request, as RPC1b's client sends it: the kit's envelope with a fresh id, and beside it the key only its sender holds,
    /// the one an id sent again carries again (this client's for that id, unless one is named).
    /// </summary>
    public async Task<string> SendAsync(string module, string type, object? payload = null, string? id = null, string? key = null)
    {
        id ??= Guid.NewGuid().ToString();
        key ??= _keys.TryGetValue(id, out var kept) ? kept : Guid.NewGuid().ToString("N");
        _keys[id] = key;
        await SendLineAsync(JsonSerializer.Serialize(new { id, module, type, payload, key }, Wire));
        return id;
    }

    /// <summary>The key this client sent <paramref name="id"/> with, or null where it sent none.</summary>
    public string? KeyOf(string id) => _keys.GetValueOrDefault(id);

    /// <summary>The next line as text, or null once the far end has closed: for a test that counts what came.</summary>
    public async Task<string?> ReadLineAsync()
    {
        try
        {
            return await _reader.ReadLineAsync().WaitAsync(Poll.Patience);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task SendLineAsync(string line, bool end = true)
    {
        var bytes = Utf8.GetBytes(end ? line + "\n" : line);
        try
        {
            await _stream.WriteAsync(bytes);
            await _stream.FlushAsync();
        }
        catch (IOException)
        {
            // The far end closed first: what it answered is what is read next.
        }
    }

    /// <summary>The next line, or null once the far end has closed.</summary>
    public async Task<JsonElement?> ReadAsync()
    {
        try
        {
            var line = await _reader.ReadLineAsync().WaitAsync(Poll.Patience);
            return line is null ? null : JsonDocument.Parse(line).RootElement.Clone();
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The answer to <paramref name="id"/>, keeping what was told on the way and the answers to other ids, which come in any order.</summary>
    public async Task<JsonElement> AnswerAsync(string id)
    {
        if (_answers.Remove(id, out var kept)) return kept;
        while (true)
        {
            var line = await ReadAsync() ?? throw new InvalidOperationException($"the pipe closed before it answered {id}");
            if (line.GetProperty("category").GetString() == "notification")
            {
                _notifications.Add(line);
                continue;
            }

            var answered = line.GetProperty("id").GetString()!;
            if (answered == id) return line;
            _answers[answered] = line;
        }
    }

    /// <summary>Every notification kept while an answer was awaited, in the order told (TERMTRUST1a: what a covered press was told).</summary>
    public IReadOnlyList<JsonElement> Told => _notifications;

    /// <summary>The first notification told, waiting up to <paramref name="within"/>; null where none came.</summary>
    public async Task<JsonElement?> NotificationAsync(TimeSpan within)
    {
        if (_notifications.Count > 0) return _notifications[0];
        try
        {
            var line = await _reader.ReadLineAsync().WaitAsync(within);
            if (line is null) return null;
            var read = JsonDocument.Parse(line).RootElement.Clone();
            if (read.GetProperty("category").GetString() == "notification") return read;
            throw new InvalidOperationException($"an answer came where a notification was awaited: {line}");
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _reader.Dispose();
        await _stream.DisposeAsync();
    }
}
