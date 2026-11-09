using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The Agent Client Protocol's frames over a pair of text streams: one line a frame, requests kept until their answer is read, and the
/// one writer every frame goes through. It knows nothing of what a frame means; the session reads the frames it hands up.
/// </summary>
internal sealed class AcpWire(TextWriter outgoing, Action<string> say, Func<bool> typedFailures)
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();

    /// <summary>
    /// What a request's answer does on the reader's thread, before anything after it is read: a prompt's turn
    /// event, and the words behind it taken, keep the order the wire said them in.
    /// </summary>
    private readonly ConcurrentDictionary<int, Action<JsonElement>> _answered = new();

    private int _nextId;

    /// <summary>What <see cref="Fail"/> faulted the requests with, which a request made after it is faulted with too.</summary>
    private Exception? _failed;

    /// <summary>The lock every frame is written under: also what a judgement of the run's end is taken under.</summary>
    public SemaphoreSlim WriteLock { get; } = new(1, 1);

    /// <summary>
    /// Read frames until the stream ends: a line that is no frame is handed to <paramref name="raw"/> and the reading goes on, as does a
    /// frame <paramref name="dispatch"/> could not read, handed to <paramref name="unreadable"/>.
    /// </summary>
    public async Task ReadAsync(
        TextReader incoming, Func<JsonElement, Task> dispatch, Action<string> raw, Action<Exception, JsonElement> unreadable, CancellationToken ct)
    {
        while (await incoming.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonElement frame;
            try
            {
                using var document = JsonDocument.Parse(line);
                frame = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Not a frame. A real agent writes diagnostics to stderr, but stdout purity is its
                // promise and not this client's guarantee — so the line is shown rather than
                // dropped, and the run continues.
                raw(line);
                continue;
            }

            try
            {
                await dispatch(frame).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // 🔴 One frame this client cannot read is shown, and the reading goes on. A
                // throw here would end the reader, and its `finally` would then say the agent's
                // stream had ended — a false sentence over a lost exception.
                unreadable(error, frame);
            }
        }
    }

    /// <summary>An answer read: it completes its request, after what the request asked to be done with it, on the reader's thread.</summary>
    public void Complete(JsonElement id, JsonElement frame)
    {
        if (!id.TryGetInt32(out var key) || !_pending.TryRemove(key, out var waiting)) return;

        // A typed client's prompt answered with a terminal failure fails as a prompt answered with an error does.
        if (typedFailures() && _answered.ContainsKey(key) && frame.TryGetProperty("result", out var ended)
            && AcpProtocol.TypedFailure(ended) is { } failed)
        {
            frame = failed;
        }

        // What the answer does here, on the reader's thread, before the next frame is read.
        if (_answered.TryRemove(key, out var answered))
        {
            try
            {
                answered(frame);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                say($"[an answer could not be recorded: {failure.Message}]");
            }
        }

        if (frame.TryGetProperty("error", out var error))
        {
            // Read without trusting the shape: a throw here would leave the call it answers waiting. Its `data` is
            // kept beside its message: an adapter may say the words that matter there.
            waiting.TrySetException(AcpRefusal.Of(error));
            return;
        }

        waiting.TrySetResult(frame.TryGetProperty("result", out var result) ? result.Clone() : default);
    }

    /// <param name="sent">
    /// Told once the request is on the wire, before its answer is awaited, and always before <paramref name="answered"/>: by the
    /// reader, where the answer is read before the write returns.
    /// </param>
    /// <param name="answered">
    /// Told the answer's frame on the reader's thread, before the answer is handed back and before anything after it is
    /// read — never once the caller has stopped waiting for it.
    /// </param>
    /// <param name="admit">
    /// Asked under the write lock as the write begins: false where the run's door has shut, and the request is not written at all.
    /// </param>
    /// <exception cref="AcpTurnedAway"><paramref name="admit"/> says no: nothing is written.</exception>
    public async Task<JsonElement> RequestAsync(
        string method, object parameters, CancellationToken ct, Action? sent = null, Action<JsonElement>? answered = null,
        Func<bool>? admit = null)
    {
        var id = Interlocked.Increment(ref _nextId);
        var waiting = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        // 🔴 The answer can be read before the write's return runs `sent` (a carry-on's turn can be kept before its note): whichever
        // comes first tells it, once, and the other waits for it to finish.
        var told = false;
        var telling = new object();
        void Tell()
        {
            lock (telling)
            {
                if (told) return;
                told = true;
                sent?.Invoke();
            }
        }

        if (answered is not null)
        {
            _answered[id] = frame =>
            {
                try
                {
                    Tell();
                }
                finally
                {
                    answered(frame);
                }
            };
        }

        _pending[id] = waiting;

        // Registered, then looked for: `Fail` sets its error before it walks the requests, so a request made after the walk sees the error here.
        if (Volatile.Read(ref _failed) is { } failure)
        {
            _answered.TryRemove(id, out _);
            _pending.TryRemove(id, out _);
            throw failure;
        }

        try
        {
            await SendAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["method"] = method,
                    ["params"] = JsonSerializer.SerializeToNode(parameters),
                },
                admit).ConfigureAwait(false);
        }
        catch (AcpTurnedAway)
        {
            _answered.TryRemove(id, out _);
            _pending.TryRemove(id, out _);
            throw;
        }

        Tell();

        try
        {
            return await waiting.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A caller that stopped waiting is told nothing more about this request.
            _answered.TryRemove(id, out _);
            throw;
        }
    }

    /// <summary>A notification: a frame that asks for no answer.</summary>
    public Task NotifyAsync(string method, object parameters) => SendAsync(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["method"] = method,
        ["params"] = JsonSerializer.SerializeToNode(parameters),
    });

    /// <summary>
    /// One frame, one line. Serialized under a lock because the answer to an agent's request is
    /// written from the read pump while the caller may be writing a request of its own, and two
    /// interleaved lines are two frames nobody can parse.
    /// </summary>
    /// <remarks>
    /// Asynchronous throughout, deliberately. The first version blocked on the write and took the
    /// lock synchronously; a cancellation callback then re-entered it on the very thread that held
    /// it, and the whole run deadlocked. `SemaphoreSlim` is not reentrant, and the fix that lasts is
    /// to have no path that can re-enter rather than a lock that tolerates it.
    /// </remarks>
    public async Task SendAsync(JsonNode frame, Func<bool>? admit = null)
    {
        await WriteLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // A prompt is admitted under the lock its write holds, so the run's door shuts between two writes, never in one.
            if (admit?.Invoke() == false) throw new AcpTurnedAway();
            await outgoing.WriteLineAsync(frame.ToJsonString()).ConfigureAwait(false);
            await outgoing.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    /// <summary>Fault everything still awaiting an answer that will never come.</summary>
    public void Fail(Exception error)
    {
        Interlocked.CompareExchange(ref _failed, error, null);
        _answered.Clear();
        foreach (var key in _pending.Keys)
        {
            if (_pending.TryRemove(key, out var waiting)) waiting.TrySetException(error);
        }
    }
}
