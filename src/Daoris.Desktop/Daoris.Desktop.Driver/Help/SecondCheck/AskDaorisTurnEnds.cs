using System.Threading.Channels;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's turn ends handed to the second check's selection, which runs on a task of its own, one at a time: the turn's reader that
/// says a turn ended never waits for it, and a selection that fails or hangs holds no conversation, press or start.
/// </summary>
public sealed class AskDaorisTurnEnds : IDisposable
{
    private readonly Channel<string> _ended = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();

    public AskDaorisTurnEnds(Func<string, CancellationToken, Task> select, Action<string, Exception>? failed = null) =>
        _ = Task.Run(() => RunAsync(select, failed));

    /// <summary>A turn of <paramref name="session"/> ended: its selection is queued, and this returns at once.</summary>
    public void Ended(string session) => _ended.Writer.TryWrite(session);

    private async Task RunAsync(Func<string, CancellationToken, Task> select, Action<string, Exception>? failed)
    {
        try
        {
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
        }
    }

    public void Dispose()
    {
        _ended.Writer.TryComplete();
        _stop.Cancel();
        _stop.Dispose();
    }
}
