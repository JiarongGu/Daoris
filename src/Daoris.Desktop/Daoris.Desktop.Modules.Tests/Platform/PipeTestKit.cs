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

/// <summary>A terminal's end of the running Daoris's pipe (RPC1a), as RPC1b's client will hold it: lines of JSON each way.</summary>
internal sealed class PipeClient : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly NamedPipeClientStream _stream;
    private readonly StreamReader _reader;
    private readonly List<JsonElement> _notifications = [];

    private PipeClient(NamedPipeClientStream stream)
    {
        _stream = stream;
        _reader = new StreamReader(stream, Utf8);
    }

    public static async Task<PipeClient> ConnectAsync(string name)
    {
        var stream = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await stream.ConnectAsync(5000);
        return new PipeClient(stream);
    }

    public async Task<JsonElement> HelloAsync(string home, int protocol = 1)
    {
        var id = await SendAsync(InstancePipe.Module, "HELLO", new { protocol, home, client = "tests", version = "0" });
        return await AnswerAsync(id);
    }

    public async Task<JsonElement> ResultAsync(string id)
    {
        var answer = await AnswerAsync(await SendAsync(InstancePipe.Module, "RESULT", new { id }));
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        return answer.GetProperty("data");
    }

    public async Task<string> SendAsync(string module, string type, object? payload = null, string? id = null)
    {
        id ??= Guid.NewGuid().ToString();
        await SendLineAsync(JsonSerializer.Serialize(new { id, module, type, payload }, Wire));
        return id;
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
            var line = await _reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
            return line is null ? null : JsonDocument.Parse(line).RootElement.Clone();
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The answer to <paramref name="id"/>, keeping what was told on the way.</summary>
    public async Task<JsonElement> AnswerAsync(string id)
    {
        while (true)
        {
            var line = await ReadAsync() ?? throw new InvalidOperationException($"the pipe closed before it answered {id}");
            if (line.GetProperty("category").GetString() == "notification")
            {
                _notifications.Add(line);
                continue;
            }

            if (line.GetProperty("id").GetString() == id) return line;
        }
    }

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
