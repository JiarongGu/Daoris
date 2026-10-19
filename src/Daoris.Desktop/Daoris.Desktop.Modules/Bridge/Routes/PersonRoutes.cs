using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The person's door in the window, the page's `bridge/person.ts` (PERSONDOOR1f, RPC1c; D156 points 2 and 4, the person-door
/// design §2.2 and §4.2): the person key the shell holds for its host, which the page keeps in memory, and the presses a terminal
/// sent over the pipe that wait for the person's <b>Confirm</b>. Every route here is the window's alone: the pipe's census refuses
/// each to a terminal, which can ask and never answer.
/// </summary>
public sealed class PersonRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    // D156 point 2: the key the shell minted for the host it started, answered to the page alone (the census classes it the window's,
    // so the pipe never answers it), which keeps it in memory and sends it on every write. The shell mints none before PERSONDOOR1g,
    // and holds none for a host it adopted, so today it answers none and the page writes as it did.
    [DriverRoute("PERSON_KEY")]
    private Task<object?> PersonKeyAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new PersonKey(null));

    // RPC1c: what waits for the window, read whole as the `CONFIRMATIONS` event tells it, for a window that opens or reloads while one
    // waits. None where this shell serves no pipe.
    [DriverRoute("PERSON_CONFIRMATIONS")]
    private Task<object?> PersonConfirmationsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(_loop.Pipe?.Confirmations.Now() ?? new PipeConfirmationsNow(0, []));

    // RPC1c: the person's Confirm or Refuse on a terminal's press, each once. One no longer waiting (answered in another window, its two
    // minutes over, taken back by its terminal or by the close) is answered as data, `gone`, for the card to say where it was pressed
    // (the UI/UX design's R3). The confirmed press runs on the pipe, in its terminal's name, and publishes what it changes as it runs.
    [DriverRoute("PERSON_CONFIRM")]
    private Task<object?> PersonConfirmAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var confirm = PayloadHelper.GetRequiredValue<bool>(request.Payload, "confirm");
        // The census refuses this to a terminal before it is dispatched; a press that came in by the pipe anyway answers nothing.
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return Task.FromResult<object?>(new PersonConfirmRefused(false, "window-only", "Only Daoris's window answers a confirmation."));
        }

        return Task.FromResult<object?>(_loop.Pipe?.Confirmations.Answer(id, confirm) == true
            ? new PersonConfirmed(true, confirm)
            : new PersonConfirmRefused(false, "gone",
                "This no longer waits: it was answered in another window, its two minutes passed, or its terminal took it back."));
    }

    private sealed record PersonKey(string? Key);

    private sealed record PersonConfirmed(bool Answered, bool Confirmed);

    private sealed record PersonConfirmRefused(bool Answered, string Why, string Message);
}
