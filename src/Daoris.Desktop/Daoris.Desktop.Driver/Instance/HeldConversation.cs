using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One notification the running Daoris told a held connection: its module, its type and its payload.</summary>
public sealed record HeldNotice(string Module, string Type, JsonElement Payload);

/// <summary>
/// A terminal's connection that holds a conversation (RPC1g2, D238 point 3, the terminal-conversation design §3.2): the wire read by
/// one pump for as long as the connection lasts, so an answer reaches the request that waits for it and every other notification, the
/// held conversation's forwarded lines among them, reaches <see cref="Told"/> as it arrives, whatever the terminal is doing.
/// </summary>
public sealed class HeldConversation : IAsyncDisposable
{
    private readonly InstanceClient _client;
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly Dictionary<string, TaskCompletionSource<JsonElement?>> _waiting = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _lost = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _pump;

    /// <summary>Holds <paramref name="client"/>, whose hello was answered, and begins reading it; <paramref name="told"/> hears each notification.</summary>
    public HeldConversation(InstanceClient client, Action<HeldNotice> told)
    {
        _client = client;
        Told = told;
        _pump = Task.Run(PumpAsync);
    }

    /// <summary>Where each notification goes, in the order the pipe told them.</summary>
    public Action<HeldNotice> Told { get; set; }

    /// <summary>Completes when the connection has ended, from either side.</summary>
    public Task Lost => _lost.Task;

    /// <summary>The process serving the pipe, where Windows says.</summary>
    public int? Server => _client.Server;

    /// <summary>
    /// One request and its answer's whole envelope; null where the connection ended or nothing answered within <paramref name="within"/>.
    /// <paramref name="hold"/> sends <c>hold: true</c> beside the envelope, which a start that holds what it opens asks with.
    /// </summary>
    public async Task<JsonElement?> RequestAsync(
        string id, string module, string type, object? payload, string? key, bool hold, TimeSpan within, CancellationToken ct = default)
    {
        var waiting = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_waiting)
        {
            _waiting[id] = waiting;
        }

        try
        {
            var line = new Dictionary<string, object?> { ["id"] = id, ["module"] = module, ["type"] = type, ["payload"] = payload };
            if (key is not null) line["key"] = key;
            if (hold) line["hold"] = true;
            await _write.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _client.WriteLineAsync(JsonSerializer.Serialize(line), ct).ConfigureAwait(false);
            }
            finally
            {
                _write.Release();
            }

            return await waiting.Task.WaitAsync(within, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException or TimeoutException
                                          or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            lock (_waiting)
            {
                _waiting.Remove(id);
            }
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            while (await _client.ReadAsync(_stop.Token).ConfigureAwait(false) is { } envelope)
            {
                if (InstanceClient.Text(envelope, "category") == "notification")
                {
                    Tell(envelope);
                    continue;
                }

                if (InstanceClient.Text(envelope, "id") is not { } id) continue;
                TaskCompletionSource<JsonElement?>? waiting;
                lock (_waiting)
                {
                    _waiting.Remove(id, out waiting);
                }

                waiting?.TrySetResult(envelope);
            }
        }
        finally
        {
            _lost.TrySetResult();
            lock (_waiting)
            {
                foreach (var each in _waiting.Values) each.TrySetResult(null);
                _waiting.Clear();
            }
        }
    }

    private void Tell(JsonElement envelope)
    {
        if (!envelope.TryGetProperty("payload", out var batch) || batch.ValueKind != JsonValueKind.Array) return;
        foreach (var each in batch.EnumerateArray())
        {
            if (InstanceClient.Text(each, "module") is not { } module || InstanceClient.Text(each, "type") is not { } type) continue;
            Told(new HeldNotice(module, type, each.TryGetProperty("payload", out var payload) ? payload.Clone() : default));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        await _client.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _pump.ConfigureAwait(false);
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or IOException)
        {
            // Ending.
        }

        _stop.Dispose();
    }
}
