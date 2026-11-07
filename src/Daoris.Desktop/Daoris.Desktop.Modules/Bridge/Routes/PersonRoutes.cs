using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The person's door in the window, the page's `bridge/person.ts`:
/// the person key the shell holds for its host, which the page keeps in memory, and the presses a terminal
/// sent over the pipe that wait for the person's <b>Confirm</b>. Every route here is the window's alone: the pipe's census refuses
/// each to a terminal, which can ask and never answer.
/// </summary>
public sealed class PersonRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    // The key the shell minted for the host it started, answered to the page alone (the census classes it the window's,
    // so the pipe never answers it), which keeps it in memory and sends it on every write. This shell mints no key
    // and holds none for a host it adopted, so it answers none and the page writes without one.
    [DriverRoute("PERSON_KEY", PipeClass.Window)]
    [Owner("PERSON_KEY", None = OwnerNone.NamesNothing)]
    private Task<object?> PersonKeyAsync(NoPayload _, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new PersonKey(null));

    // What waits for the window, read whole as the `CONFIRMATIONS` event tells it, for a window that opens or reloads while one
    // waits. None where this shell serves no pipe.
    [DriverRoute("PERSON_CONFIRMATIONS", PipeClass.Window)]
    [Owner("PERSON_CONFIRMATIONS", None = OwnerNone.NamesNothing)]
    private Task<object?> PersonConfirmationsAsync(NoPayload _, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(_loop.Pipe?.Confirmations.Now() ?? new PipeConfirmationsNow(0, []));

    // What waits at the service's own door, read whole as the `SERVICE_CONFIRMATIONS` event tells it, for a
    // window that opens or reloads while one waits; the person answers each at the service's door with the key, as before.
    [DriverRoute("SERVICE_CONFIRMATIONS", PipeClass.Window)]
    [Owner("SERVICE_CONFIRMATIONS", None = OwnerNone.NamesNothing)]
    private Task<object?> ServiceConfirmationsAsync(NoPayload _, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(_loop.ServiceConfirmations.Now());

    // The person's Confirm or Refuse on a terminal's press, each once. One no longer waiting (answered in another window, taken
    // back by its terminal or by the close), or whose Confirm is already being judged, is answered as data, `gone`, for the card to say
    // where it is pressed, or a toast where its card has gone with it. A Confirm is answered once its grounds are judged, the card kept `checking` meanwhile: `{ answered: true }` where they held
    // and it runs; `{ answered: false, why: "changed", reason, values }` where one moved and nothing ran; `{ answered: false, why:
    // "unread" }` where they could not be read just now and it still waits; `{ answered: false, why: "draining" }`
    // while Daoris closes or an update drains it, nothing taken and the press kept across the restart;
    // `{ answered: false, why: "unrecorded" }` where its record could not say the answer, nothing taken. The page waits for it as long as
    // the press's `checkWithin`.
    // The confirmed press runs on the pipe, in its terminal's name, and publishes what it changes as it runs.
    // `source: "service"` answers one waiting at the service's door instead, where the host makes
    // the request at the Confirm: `{ answered: true }` where its door took it, else the host's code as `why` (`not-made` with the door's
    // own sentence, `restarted`), `gone` where none, and `unreachable` where the service did not answer, nothing taken.
    [DriverRoute("PERSON_CONFIRM", PipeClass.Window)]
    [Owner("PERSON_CONFIRM", None = OwnerNone.ActsOnNothing)]
    private async Task<object?> PersonConfirmAsync(PersonConfirmPress press, CancellationToken cancellationToken)
    {
        var (id, confirm) = (press.Id, press.Confirm);
        // The census refuses this to a terminal before it is dispatched; a press that came in by the pipe anyway answers nothing.
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return new PersonConfirmRefused(false, "window-only", "Only Daoris's window answers a confirmation.");
        }

        if (press.Source == "service")
        {
            return await ServiceConfirmAsync(id, confirm, cancellationToken).ConfigureAwait(false);
        }

        if (_loop.Pipe is not { } pipe) return Gone();
        var answer = await pipe.Confirmations.AnswerAsync(id, confirm, cancellationToken).ConfigureAwait(false);
        return answer switch
        {
            { Outcome: PipeAnswered.Answered } => new PersonConfirmed(true, confirm),
            { Outcome: PipeAnswered.Changed, Change: { } change } => new PersonConfirmChanged(
                false, "changed", $"Not done: {change.Sentence}.", change.Reason, change.Values),
            { Outcome: PipeAnswered.Unread } => new PersonConfirmRefused(
                false, "unread", "Daoris could not read what this rests on just now; it still waits."),
            // An update drains Daoris, or it closes; the press is kept across the restart, and nothing is taken.
            { Outcome: PipeAnswered.Draining } => new PersonConfirmRefused(
                false, "draining", "Daoris is about to restart; this still waits."),
            // The answer could not be written to the press's record, so nothing is taken or run.
            { Outcome: PipeAnswered.Unrecorded } => new PersonConfirmRefused(
                false, "unrecorded", "Daoris could not keep your answer just now; nothing was done, and it still waits."),
            _ => Gone(),
        };

        static PersonConfirmRefused Gone() => new(false, "gone",
            "This no longer waits: it was answered in another window, or its terminal took it back.");
    }

    // One waiting at the service's door, answered there; a refusal is the host's sentence, by its code.
    private async Task<object> ServiceConfirmAsync(string id, bool confirm, CancellationToken cancellationToken)
    {
        const string Away = "Daoris's service did not answer just now; nothing was done, and this still waits.";
        if (_loop.Service is not { } service) return new PersonConfirmRefused(false, "unreachable", Away);

        ConfirmationAnswered answered;
        try
        {
            answered = await service.Confirmations.AnswerAsync(id, confirm, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException
            || (error is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new PersonConfirmRefused(false, "unreachable", Away);
        }

        return answered.Done
            ? new PersonConfirmed(true, confirm)
            : new PersonConfirmRefused(false, answered.Code ?? "gone", answered.Message);
    }

    // The window's word that a terminal's press is drawn in front of the person, in a window that is in front: kept once as
    // when it is shown, which every window is told by `CONFIRMATIONS`, and it moves no clock. One no longer waiting is
    // answered as data, `gone`. It runs nothing and moves no record, so it declares no change.
    [DriverRoute("PERSON_CONFIRM_SHOWN", PipeClass.Window)]
    [Owner("PERSON_CONFIRM_SHOWN", None = OwnerNone.ActsOnNothing)]
    private Task<object?> PersonConfirmShownAsync(PersonConfirmShown press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return Task.FromResult<object?>(new PersonShownRefused(false, "window-only", "Only Daoris's window says what it shows."));
        }

        return Task.FromResult<object?>(_loop.Pipe?.Confirmations.Show(id) == true
            ? new PersonShown(true)
            : new PersonShownRefused(false, "gone",
                "This no longer waits: it was answered in another window, or its terminal took it back."));
    }

    // The start of the terminal that holds a role the role door staffed with the terminal, in the window alone, since the
    // key passes from the window to the tab. `{ role, share? }`, `role` the role's catalogue id, `share` the choice while a holder lives
    // (`own`, or a living holder's id). A new holder's tab is started first, Daoris's own pinned harness with the key in its environment,
    // and nothing is kept where it could not start; the key is never answered. Answered `{ done, why?, message? }`, a refusal as data
    // (`window-only`, `unknown-role`, `not-terminal`, `choose-terminal`, `no-holder`, `revoked-holder`, `holder-harness`, `unstarted`,
    // `no-pipe`). The judge's step ends first; then each decision kept under a hold it replaced goes stale. Turning a role's terminal off
    // is the role door's: its holder changed, or the tab closed.
    [DriverRoute("START_ROLE_TERMINAL", PipeClass.Window)]
    [Owner("START_ROLE_TERMINAL", None = OwnerNone.RoleTerminal)]
    [Publishes("START_ROLE_TERMINAL", StateScopes.Driver | StateScopes.Sessions)]
    private async Task<object?> StartRoleTerminalAsync(RoleTerminalPress press, CancellationToken cancellationToken)
    {
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return new StandingChange(false, "window-only", "Only Daoris's window starts a role's terminal.");
        }

        return _loop.Pipe is { } pipe
            ? await SupervisorDoor.StartAsync(_loop, pipe, new HoldRequest(press.Role, press.Share), cancellationToken).ConfigureAwait(false)
            : new StandingChange(false, "no-pipe", "This Daoris serves no terminal, so it starts no role's terminal.");
    }

    private sealed record PersonKey(string? Key);

    private sealed record PersonShown(bool Shown);

    private sealed record PersonShownRefused(bool Shown, string Why, string Message);

    private sealed record PersonConfirmed(bool Answered, bool Confirmed);

    private sealed record PersonConfirmRefused(bool Answered, string Why, string Message);

    private sealed record PersonConfirmChanged(bool Answered, string Why, string Message, string Reason, IReadOnlyDictionary<string, string> Values);
}
