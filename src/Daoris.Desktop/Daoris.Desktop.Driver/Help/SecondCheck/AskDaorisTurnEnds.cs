using System.Threading.Channels;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's turn ends handed to the second check's selection, which runs on a task of its own, one at a time: the turn's reader that
/// says a turn ended never waits for it, and a selection that fails or hangs holds no conversation, press or start. The start's catch-up
/// runs on the same task ahead of every end, so the two never write one conversation's ranges at once.
/// </summary>
public sealed class AskDaorisTurnEnds : IDisposable
{
    private readonly Channel<string> _ended = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="first">What runs ahead of every end, once: the start's catch-up.</param>
    public AskDaorisTurnEnds(Func<string, CancellationToken, Task> select, Action<string, Exception>? failed = null, Func<CancellationToken, Task>? first = null) =>
        _ = Task.Run(() => RunAsync(select, failed, first));

    /// <summary>Complete once the work handed to run first has run, failed or been stopped.</summary>
    public Task First => _first.Task;

    /// <summary>A turn of <paramref name="session"/> ended: its selection is queued, and this returns at once.</summary>
    public void Ended(string session) => _ended.Writer.TryWrite(session);

    private async Task RunAsync(Func<string, CancellationToken, Task> select, Action<string, Exception>? failed, Func<CancellationToken, Task>? first)
    {
        try
        {
            if (first is not null)
            {
                try
                {
                    await first(_stop.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    failed?.Invoke("", error);
                }
            }

            _first.TrySetResult();
            await foreach (var session in _ended.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                try
                {
                    await select(session, _stop.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    failed?.Invoke(session, error);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped with its process: a queued end is selected at the next start's catch-up.
            _first.TrySetResult();
        }
    }

    public void Dispose()
    {
        _ended.Writer.TryComplete();
        _stop.Cancel();
        _stop.Dispose();
    }
}
