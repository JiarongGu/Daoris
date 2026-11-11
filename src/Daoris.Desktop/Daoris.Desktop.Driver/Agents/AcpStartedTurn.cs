namespace Daoris.Driver;

/// <summary>
/// The turn a steer started where its turn had just ended: <c>codex-acp</c> reads no <c>idleBehavior</c>, so a steer that finds no live
/// turn answers <c>startedNewTurn</c> and runs the words as a turn no prompt owns. Its end is read as a turn its harness woke is
/// (<see cref="AcpWake"/>): the adapter's word, or its quiet where the adapter says none, as Codex does; then closed in the record as an
/// answered turn. The steer lasts until then, so no prompt goes into that turn, which Codex would reset.
/// </summary>
internal sealed class AcpStartedTurn(ITurnEndWiring wiring)
{
    /// <summary>
    /// Completes once the started turn has ended, once the stream has, or where nothing of it is heard within the wake's quiet.
    /// </summary>
    public async Task EndedAsync(CancellationToken ct)
    {
        var wake = wiring.Wake;
        if (!wake.Running)
        {
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var began = wake.WokeAsync(bounded.Token);
            await Task.WhenAny(began, wiring.Ended, Task.Delay(wiring.WakeQuiet, wiring.Clock, bounded.Token)).ConfigureAwait(false);
            await bounded.CancelAsync().ConfigureAwait(false);
            if (!wake.Running) return;
        }

        if (await wake.IdleAsync(wiring.Prompted, wiring.Ended, ct, wiring.WakeQuiet).ConfigureAwait(false)) wiring.EndWokenTurn();
    }
}
