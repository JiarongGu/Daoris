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
    // so the pipe never answers it), which keeps it in memory and sends it on every write. This shell mints no key (minting it and
    // proving its host holds it are PERSONDOOR1g's) and holds none for a host it adopted, so it answers none and the page writes as
    // it did.
    [DriverRoute("PERSON_KEY")]
    private Task<object?> PersonKeyAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new PersonKey(null));

    // RPC1c: what waits for the window, read whole as the `CONFIRMATIONS` event tells it, for a window that opens or reloads while one
    // waits. None where this shell serves no pipe.
    [DriverRoute("PERSON_CONFIRMATIONS")]
    private Task<object?> PersonConfirmationsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(_loop.Pipe?.Confirmations.Now() ?? new PipeConfirmationsNow(0, []));

    // NOTIFY2j (D196 point 4): what waits at the service's own door, read whole as the `SERVICE_CONFIRMATIONS` event tells it, for a
    // window that opens or reloads while one waits; the person answers each at the service's door with the key, as before.
    [DriverRoute("SERVICE_CONFIRMATIONS")]
    private Task<object?> ServiceConfirmationsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(_loop.ServiceConfirmations.Now());

    // RPC1c: the person's Confirm or Refuse on a terminal's press, each once. One no longer waiting (answered in another window, taken
    // back by its terminal or by the close), or whose Confirm is already being judged, is answered as data, `gone`, for the card to say
    // where it was pressed (the UI/UX design's R3), or a toast where its card has gone with it. CONFIRMWAIT1a-i (the confirm-wait design
    // §5.2): a Confirm is answered once its grounds are judged, the card kept `checking` meanwhile: `{ answered: true }` where they held
    // and it runs; `{ answered: false, why: "changed", reason, values }` where one moved and nothing ran; `{ answered: false, why:
    // "unread" }` where they could not be read just now and it still waits; CONFIRMWAIT1a-ii (§7.2): `{ answered: false, why: "draining" }`
    // while Daoris closes or an update drains it, nothing taken and the press kept across the restart; CONFIRMWAIT1 (§15.2 item 8):
    // `{ answered: false, why: "unrecorded" }` where its record could not say the answer, nothing taken. The page waits for it as long as
    // the press's `checkWithin`.
    // The confirmed press runs on the pipe, in its terminal's name, and publishes what it changes as it runs.
    [DriverRoute("PERSON_CONFIRM")]
    private async Task<object?> PersonConfirmAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var confirm = PayloadHelper.GetRequiredValue<bool>(request.Payload, "confirm");
        // The census refuses this to a terminal before it is dispatched; a press that came in by the pipe anyway answers nothing.
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return new PersonConfirmRefused(false, "window-only", "Only Daoris's window answers a confirmation.");
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
            // CONFIRMWAIT1a-ii (§7.2): an update drains Daoris, or it closes; the press is kept across the restart, and nothing was taken.
            { Outcome: PipeAnswered.Draining } => new PersonConfirmRefused(
                false, "draining", "Daoris is about to restart; this still waits."),
            // CONFIRMWAIT1 (§15.2 item 8): the answer could not be written to the press's record, so nothing was taken or run.
            { Outcome: PipeAnswered.Unrecorded } => new PersonConfirmRefused(
                false, "unrecorded", "Daoris could not keep your answer just now; nothing was done, and it still waits."),
            _ => Gone(),
        };

        static PersonConfirmRefused Gone() => new(false, "gone",
            "This no longer waits: it was answered in another window, or its terminal took it back.");
    }

    // CONFIRMSEEN1: the window's word that a terminal's press is drawn in front of the person, in a window that is in front: kept once as
    // when it was shown, which every window is told by `CONFIRMATIONS`, and it moves no clock (CONFIRMWAIT1a-i). One no longer waiting is
    // answered as data, `gone`. It runs nothing and moves no record, so it declares no change.
    [DriverRoute("PERSON_CONFIRM_SHOWN")]
    private Task<object?> PersonConfirmShownAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return Task.FromResult<object?>(new PersonShownRefused(false, "window-only", "Only Daoris's window says what it shows."));
        }

        return Task.FromResult<object?>(_loop.Pipe?.Confirmations.Show(id) == true
            ? new PersonShown(true)
            : new PersonShownRefused(false, "gone",
                "This no longer waits: it was answered in another window, or its terminal took it back."));
    }

    // TERMTRUST1a (D201 point 5, the terminal-trust design §4.1): the person's standing confirmation, granted or revoked in the window
    // alone. `presses`, the names ticked, `[]` to turn it off; answered `{ done, why?, message? }`, a refusal as data: `window-only` for
    // the terminal's door (the census refuses it first), `not-offered` for a grant this build does not offer yet (D201's note on order),
    // `unknown-press`, `newer` for a record a newer Daoris wrote, and `unwritten` for a record that could not be written (a revoke still
    // stands for this run). It moves STATE's `standingConfirmation`.
    [DriverRoute("SET_STANDING_CONFIRMATION")]
    [Publishes("SET_STANDING_CONFIRMATION", StateScopes.Driver)]
    private Task<object?> SetStandingConfirmationAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return Task.FromResult<object?>(new StandingChange(false, "window-only", "Only Daoris's window grants or revokes the standing confirmation."));
        }

        var presses = request.Payload is { ValueKind: System.Text.Json.JsonValueKind.Object } payload
            && payload.TryGetProperty("presses", out var named) && named.ValueKind == System.Text.Json.JsonValueKind.Array
            ? [.. named.EnumerateArray().Select(each => each.ValueKind == System.Text.Json.JsonValueKind.String ? each.GetString() ?? "" : each.GetRawText())]
            : (IReadOnlyList<string>?)null;
        if (presses is null)
        {
            return Task.FromResult<object?>(new StandingChange(false, "presses", "The standing confirmation is set by the presses ticked, a list."));
        }

        return Task.FromResult<object?>(_loop.Pipe is { } pipe
            ? pipe.StandingConfirmation.Set(presses)
            : new StandingChange(false, "no-pipe", "This Daoris serves no terminal, so it has no standing confirmation."));
    }

    // SUPERVISOR1a-ii (D208 §2.3, §5), amended by STEWARDROLE1a-i (D209 §2.2, §2.3): the owner's appointment of a role, made and turned
    // off in the window alone. `{ role, scope, holder?, on, share?, setup? }`, `scope` `machine` or `repository:<name>` (the built
    // `repository` read as that scope for one release), `holder` `terminal` where none is named, `share` a later Appoint's choice while a
    // holder lives (`own`, or a living holder's id), `setup` the steward's own tick. With `on`, a new holder's tab is started first, by
    // the window, Daoris's own pinned harness with the key in its environment, and nothing is kept where it could not start; the key is
    // never answered. SUPERVISOR1e: `scope` may be `workspace:<name>` for the supervisor, and `window` its overrule window in minutes,
    // 1 to 60, five where none is named. Answered `{ done, why?, message? }`, a refusal as data (`window-only`, `not-offered`,
    // `unknown-role`, `unknown-repository`, `unknown-workspace`, `window`, `scope`, `choose-terminal`, `no-holder`, `revoked-holder`,
    // `holder-harness`, `newer`, `unwritten`, `unstarted`, `no-pipe`). The judge's mutation step ends first; then each decision kept
    // under a generation it ended goes stale, and a workspace's for a repository whose own appointment replaced it there.
    [DriverRoute("SET_APPOINTMENT")]
    [Publishes("SET_APPOINTMENT", StateScopes.Driver | StateScopes.Sessions)]
    private async Task<object?> SetAppointmentAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        if (PipeDoor.Now == PipeDoor.Terminal)
        {
            return new StandingChange(false, "window-only", "Only Daoris's window appoints a role or turns one off.");
        }

        var role = PayloadHelper.GetRequiredValue<string>(request.Payload, "role");
        // STEWARDROLE1a-i (D209 §2.3): `scope`; the built `{ role, repository, … }` form is read as `repository:<name>` for one release.
        var scope = PayloadHelper.GetOptionalValue<string>(request.Payload, "scope")
            ?? (PayloadHelper.GetOptionalValue<string>(request.Payload, "repository") is { Length: > 0 } repository ? RoleScopes.Repository(repository) : null);
        if (scope is null || !RoleScopes.Known(scope))
        {
            return new StandingChange(false, StandingConfirmation.Scope,
                "An appointment names its scope: `machine`, `repository:<name>` or `workspace:<name>`.");
        }

        var holder = PayloadHelper.GetOptionalValue<string>(request.Payload, "holder") ?? RoleHolders.Terminal;
        var on = PayloadHelper.GetRequiredValue<bool>(request.Payload, "on");
        var share = PayloadHelper.GetOptionalValue<string>(request.Payload, "share");
        var setup = PayloadHelper.GetOptionalValue<bool>(request.Payload, "setup");
        var window = PayloadHelper.GetOptionalValue<long?>(request.Payload, "window");
        return _loop.Pipe is { } pipe
            ? await SupervisorDoor.AppointAsync(_loop, pipe, new AppointRequest(role, scope, holder, on, share, setup, window), cancellationToken).ConfigureAwait(false)
            : new StandingChange(false, "no-pipe", "This Daoris serves no terminal, so it appoints no terminal holder.");
    }

    private sealed record PersonKey(string? Key);

    private sealed record PersonShown(bool Shown);

    private sealed record PersonShownRefused(bool Shown, string Why, string Message);

    private sealed record PersonConfirmed(bool Answered, bool Confirmed);

    private sealed record PersonConfirmRefused(bool Answered, string Why, string Message);

    private sealed record PersonConfirmChanged(bool Answered, string Why, string Message, string Reason, IReadOnlyDictionary<string, string> Values);
}
