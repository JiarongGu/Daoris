using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The window's half of a terminal's hold of a conversation, the page's
/// `bridge/hold.ts`: which conversations a terminal holds, and the person taking one back. Both are the window's alone: a terminal lets
/// go of its own hold by the pipe's <c>LET_GO</c>, and the window is never a holder.
/// </summary>
public sealed class HoldRoutes(DriverLoop loop)
{
    // What terminals hold, read whole as the `SESSION_HOLDS` event tells it, for a window that opens or reloads while one holds. None where
    // this shell serves no pipe.
    [DriverRoute("SESSION_HOLDS", PipeClass.Window)]
    [Owner("SESSION_HOLDS", None = OwnerNone.NamesNothing)]
    private Task<object?> SessionHoldsAsync(NoPayload _, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(loop.Pipe?.Holding.Now() ?? PipeHoldsNow.None);

    // *Let go of the terminal* on a held conversation's head: the hold ends, told to its terminal, and the conversation goes on in Daoris.
    // Answered `{ done, why?, message? }`, a refusal as data: `window-only` for the terminal's door (the census refuses it first),
    // `not-held` where no terminal holds it now.
    [DriverRoute("SESSION_LET_GO", PipeClass.Window)]
    [Owner("SESSION_LET_GO", Session = "id")]
    private Task<object?> SessionLetGoAsync(SessionIdPress press, CancellationToken cancellationToken)
    {
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return Task.FromResult<object?>(new LetGo(false, "window-only", "Only Daoris's window takes a conversation back from a terminal."));
        }

        return Task.FromResult<object?>(loop.Pipe?.Holding.TakeBack(press.Id) == true
            ? new LetGo(true, null, null)
            : new LetGo(false, "not-held", "No terminal holds this conversation now."));
    }

    private sealed record LetGo(bool Done, string? Why, string? Message);
}
