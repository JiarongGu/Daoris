using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A person's press from its reservation to its ending: its grounds read,
/// its record written, its card shown, and once the window answers, run or ended. It reaches the pipe's state (<see cref="PipeState"/>)
/// under its gate and the waiting records (<see cref="PipeWaitingKeep"/>) under theirs, never both at once.
/// </summary>
internal sealed class PipeAsking(InstancePipe pipe)
{
    private readonly PipeState _state = pipe.State;
    private readonly PipeWaitingKeep _keep = pipe.Keep;
    private readonly MachineLog _log = pipe.Log;
    private readonly TimeProvider _clock = pipe.Clock;
    private readonly CancellationTokenSource _lifetime = pipe.Lifetime;

    private PipeConfirmations Confirmations => pipe.Confirmations;

    /// <summary>
    /// A person's press on its reservation: its grounds read outside every lock, bounded, and cancelled where a
    /// withdrawal or a close ended it meanwhile. Then the reservation is claimed, and one ended meanwhile asks nothing more. One that does
    /// not read, or reads that its setting is already as it asks, lets the place go and is answered so, journaled, nothing asked.
    /// Otherwise its card is shown and it runs once the person confirms it with its grounds still holding.
    /// </summary>
    public async Task AskThenRunAsync(PipeReserved reserved, PipeGroundRow row, IReadOnlyList<PipeConfirmationField> fields, string? why)
    {
        var (request, binding, mine, connection, place) = (reserved.Request, reserved.Binding, reserved.Line, reserved.Connection, reserved.Place);
        var id = request.Id;
        IReadOnlyList<PipeReading> asked;
        PressViewed? viewed;
        try
        {
            using var reading = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, reserved.Reading.Token);
            asked = await row.ReadAsync(pipe.Grounds, request.Payload, _clock, reading.Token).ConfigureAwait(false);
            viewed = await PressViews.ReadAsync(pipe.Grounds, request.Module, request.Type, request.Payload, reading.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!Claimed(reserved)) return;
            Confirmations.Let(place);
            _log.Info("pipe.grounds", ("state", "unread"), ("route", $"{request.Module}.{request.Type}"), ("error", error.GetType().Name));
            EndReserved(reserved, pipe.Refuse(request, InstancePipe.UnreadGrounds,
                "Daoris could not read what this press rests on just now, so it was not asked; nothing was kept. Send it again."));
            return;
        }

        // A withdrawal or a close that ended it while its grounds were read already answered it: nothing more is asked.
        if (!Claimed(reserved)) return;
        if (row.Already(asked, request.Payload))
        {
            Confirmations.Let(place);
            EndReserved(reserved, pipe.Refuse(request, InstancePipe.Already, "This is already set as this asks; nothing was asked."));
            return;
        }

        // The route's view refuses in the route's own words what the route would refuse, so the person never sees it (D261).
        if ((row.Refusal(asked) ?? viewed?.Refusal) is { } words)
        {
            Confirmations.Let(place);
            EndReserved(reserved, pipe.Refuse(request, InstancePipe.NotAllowed, words));
            return;
        }

        if (pipe.Holds(request.Module, request.Type))
        {
            Confirmations.Let(place);
            EndReserved(reserved, pipe.RefuseStopping(request));
            return;
        }

        // Its record is written before the window is asked and the terminal told, named by the confirmation's own
        // id; a press carrying a key keeps no payload and no grounds in it. One that cannot be written is not asked.
        var keyed = PipeCensus.Keyed(request.Module, request.Type);
        var record = new PipeWaitingRecord(
            Guid.NewGuid().ToString("N"), id, binding, request.Module, request.Type,
            keyed ? null : request.Payload is { ValueKind: not JsonValueKind.Undefined } sent ? sent.GetRawText() : "",
            keyed ? [] : asked, reserved.Asker, reserved.Asked, null, keyed, PipeWaitingRecord.Waiting);
        if (!_keep.Keep(record))
        {
            Confirmations.Let(place);
            EndReserved(reserved, pipe.Refuse(request, InstancePipe.Unrecorded,
                "Daoris could not keep this press while it waits for the person, so it was not asked; nothing was kept. Send it again."));
            return;
        }

        // The person's press waits for the window, which shows it naming this terminal, and runs on Confirm alone,
        // its grounds read again then. The id is bound before the window is asked, so the same id sent again meanwhile joins this
        // one and asks nothing. Its terminal heard its id before the read; a line that ended meanwhile waits on it no more.
        bool waitedOn;
        lock (_state.Gate)
        {
            waitedOn = _state.Connections.Contains(connection) && _state.Owners.GetValueOrDefault(id) == connection;
        }

        var payload = request.Payload;
        var shown = Confirmations.Ask(
            place, request.Module, request.Type, fields, reserved.Asker, why,
            judge: row.Grounds.Count == 0 ? null : ct => JudgeAsync(row, payload, asked, ct), checkWithin: row.Grounds.Count == 0 ? null : row.Bound,
            @as: new PipeAsAsked(record.Confirmation, record.Asked, Keyed: keyed, WaitedOn: waitedOn, Hold: reserved.Hold, View: viewed?.View));
        lock (_state.Gate)
        {
            _state.WaitingFor[id] = shown.Shown.Id;
            _state.Lines[id] = mine;
        }

        // A close that began after the claim keeps it as any press waiting: its record kept and its line answered. The close
        // alone: a drain answers no waiting line, and the close it ends in keeps this one when it begins.
        if (pipe.Stopped) pipe.KeepAtClose(id, shown.Shown.Id, mine);
        await ConfirmThenRunAsync(request, id, binding, mine, shown.Ended, record.Confirmation).ConfigureAwait(false);
    }

    /// <summary>The reservation taken from those the pipe holds, once: false where a withdrawal or a close ended it first.</summary>
    private bool Claimed(PipeReserved reserved)
    {
        lock (_state.Gate)
        {
            return _state.Reserved.TryGetValue(reserved.Request.Id, out var held) && ReferenceEquals(held, reserved) && _state.Reserved.Remove(reserved.Request.Id);
        }
    }

    /// <summary>
    /// A reservation ended before its card: its answer journaled where it can be, so <c>RESULT</c> and the same id sent
    /// again say it, taken out of what is under way, and its line answered.
    /// </summary>
    public void EndReserved(PipeReserved reserved, IpcResponse answer)
    {
        var (id, binding) = (reserved.Request.Id, reserved.Binding);
        var text = IpcJson.Serialize(answer);
        try
        {
            if (_state.Journal!.Begin(id, binding)) _state.Journal.End(id, binding, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Not kept: its terminal still hears it now.
        }

        lock (_state.Gate)
        {
            _state.Running.Remove(id);
            _state.Owners.Remove(id);
        }

        reserved.Line.TrySetResult(text);
    }

    /// <summary>The grounds read again at the Confirm, outside every lock, and compared with the ask's: a failed read is unread.</summary>
    public async Task<PipeJudgement> JudgeAsync(PipeGroundRow row, JsonElement? payload, IReadOnlyList<PipeReading> asked, CancellationToken ct)
    {
        IReadOnlyList<PipeReading> now;
        try
        {
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            now = await row.ReadAsync(pipe.Grounds, payload, _clock, lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _log.Info("pipe.grounds", ("state", "unread-at-confirm"), ("error", error.GetType().Name));
            return new PipeJudgement(PipeJudged.Unread);
        }

        return PipeGroundRow.Judge(asked, now) is { } change ? new PipeJudgement(PipeJudged.Changed, change) : PipeJudgement.Holding;
    }

    /// <summary>
    /// A person's press once the window has answered: run, once, where the person confirmed it and the instance is not
    /// closing; otherwise answered with how it ended and kept as its ending, so <c>RESULT</c> and the same id sent again say it.
    /// Its record says what happened at each step. Confirmed, it is rewritten <c>begun</c>, then the journal's
    /// first line, then it is removed, then the run; ended otherwise, it is rewritten <c>ended</c> with its answer, then the journal's line,
    /// then it is removed, even where that line failed.
    /// </summary>
    public async Task ConfirmThenRunAsync(
        IpcRequest request, string id, PipeBinding binding, TaskCompletionSource<string> answered, Task<PipeEnding> ended, string confirmation)
    {
        var ending = await ended.ConfigureAwait(false);
        bool byWithdraw;
        lock (_state.Gate)
        {
            _state.WaitingFor.Remove(id);
            _state.Lines.Remove(id);
            byWithdraw = _state.Withdrawn.Remove(id);
        }

        // Its record already says it began, written before the Confirm is taken. Its hold is judged there for
        // the last time, so it runs whatever began since, the update's applying or a close, which is then its to wait for.
        if (ending.End == PipeConfirmationEnd.Confirmed && _keep.Begun(confirmation))
        {
            await pipe.RunAsync(request, id, binding, answered, begun: () => _keep.Forget(confirmation)).ConfigureAwait(false);
            return;
        }

        // The answer its record is rewritten with as the ending is taken; one with none (a Confirm that met the close) is written now.
        var text = _keep.TakeEnding(confirmation);
        if (text is null)
        {
            text = IpcJson.Serialize(Ending(request, ending, byWithdraw));
            _keep.Rewrite(confirmation, PipeWaitingRecord.Ended, text);
        }
        try
        {
            if (_state.Journal!.Begin(id, binding)) _state.Journal.End(id, binding, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Not kept: the terminal still hears it now, and RESULT says it does not know, which is so.
        }

        // Removed even where the journal's line failed: the record already said it ended, so no restart asks it again.
        _keep.Forget(confirmation);
        lock (_state.Gate)
        {
            _state.Running.Remove(id);
            _state.Owners.Remove(id);
        }

        // The close may have answered its line already (`waiting`): this ending is still kept, and RESULT says it.
        answered.TrySetResult(text);
    }

    /// <summary>
    /// A press's ending written to its record before anyone is told it or it runs:
    /// <c>begun</c> for a Confirm that runs, <c>ended</c> with the answer its terminal will hear for any other, that answer kept for its
    /// line. Unrecorded where the record could not be written, and the press waits on.
    /// </summary>
    /// <remarks>
    /// A Confirm's hold is judged here for the last time, before <c>begun</c> is written: one the drain's hold or the close
    /// reached since the window's own check is held, nothing written, and waits on in its record for the restart. One written <c>begun</c>
    /// runs whatever begins after, as the person is told, and a close begun meanwhile is its to wait for (<see cref="ConfirmThenRunAsync"/>,
    /// <see cref="InstancePipe.KeepAtClose"/>): never told confirmed and then ended unrun.
    /// </remarks>
    public PipePersisted Persisted(PipeConfirmation card, PipeEnding ending)
    {
        string? id;
        bool byWithdraw;
        lock (_state.Gate)
        {
            id = _state.WaitingFor.FirstOrDefault(each => each.Value == card.Id).Key;
            byWithdraw = id is not null && _state.Withdrawn.Contains(id);
        }

        if (id is null) return PipePersisted.Written;
        if (ending.End == PipeConfirmationEnd.Confirmed)
        {
            if (pipe.Holds(card.Module, card.Route)) return PipePersisted.Held;
            _keep.MarkBegun(card.Id, true);
            if (_keep.Rewrite(card.Id, PipeWaitingRecord.Begun)) return PipePersisted.Written;
            _keep.MarkBegun(card.Id, false);
            return PipePersisted.Unrecorded;
        }

        var text = IpcJson.Serialize(Ending(new IpcRequest { Id = id, Module = card.Module, Type = card.Route }, ending, byWithdraw));
        if (!_keep.Rewrite(card.Id, PipeWaitingRecord.Ended, text)) return PipePersisted.Unrecorded;
        _keep.Ended(card.Id, text);
        return PipePersisted.Written;
    }

    /// <summary>The answer a press's terminal hears for an ending that runs nothing.</summary>
    private IpcResponse Ending(IpcRequest request, PipeEnding ending, bool byWithdraw)
    {
        var id = request.Id;
        return ending switch
        {
            { End: PipeConfirmationEnd.Refused } => IpcResponse.CreateError(id, InstancePipe.Refused, null,
                "The person refused this in Daoris's window. Nothing was done."),
            // Confirmed, and a ground it is asked on has moved; said with its reason's code and values.
            { End: PipeConfirmationEnd.Changed, Change: { } change } => IpcResponse.CreateError(id, InstancePipe.Changed,
                new Dictionary<string, string>(change.Values) { ["ground"] = change.Reason },
                $"The person confirmed this in Daoris's window, but {change.Sentence}. Nothing was done."),
            { End: PipeConfirmationEnd.Withdrawn } when byWithdraw => IpcResponse.CreateError(id, InstancePipe.Withdrawn, null,
                "This terminal took its request back before the person answered it. Nothing was done."),
            // A press carrying a key keeps no payload to wait on across the close.
            { End: PipeConfirmationEnd.Lost } => IpcResponse.CreateError(id, InstancePipe.Lost, null,
                "Daoris closed while this waited, and it carries a key, so it does not wait across a restart; nothing was done. Send it again once Daoris is back."),
            _ when pipe.Holds(request.Module, request.Type) => pipe.RefuseStopping(request),
            _ => IpcResponse.CreateError(id, IpcErrorCodes.OperationCancelled, null,
                "This terminal took its request back before the person answered it. Nothing was done."),
        };
    }
}
