using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What a terminal asks of a press it sent, each by the key that sent it (the terminal RPC design §6.1, §6.2, the confirm-wait design
/// §4.2, §4.3): the kit's <c>CANCEL</c>, the pipe's <c>RESULT</c> and its <c>WITHDRAW</c>. Each reads and marks the pipe's state
/// (<see cref="PipeState"/>) under its gate, and asks the confirmations outside it but where it says.
/// </summary>
internal sealed class PipeTakeBacks(InstancePipe pipe)
{
    private readonly PipeState _state = pipe.State;
    private readonly MachineLog _log = pipe.Log;
    private readonly IMessageDispatcher _dispatcher = pipe.Dispatcher;
    private readonly CancellationTokenSource _lifetime = pipe.Lifetime;

    private PipeConfirmations Confirmations => pipe.Confirmations;

    /// <summary>
    /// The kit's <c>CANCEL { requestId }</c> from a terminal (RPC1h, §6.2), for a press this pipe began and with the key that sent it:
    /// passed to the dispatcher's own <c>SHENORA.REQUESTS</c> module, which aborts the token the press runs under, so the press ends as
    /// a cancelled one and <c>RESULT</c> says so. A press that has ended answers <c>cancelled: false</c> and nothing runs; an id this pipe
    /// did not begin (a window's, an id nobody sent) or one begun with another key is refused as an id bound elsewhere is, and
    /// nothing is cancelled. The cancel carries its own key, a secret only its sender holds, as a press does.
    /// </summary>
    public async Task<string> CancelAsync(IpcRequest request, string? key)
    {
        var target = InstancePipe.Field(request, "requestId");
        if (string.IsNullOrEmpty(target))
        {
            return IpcJson.Serialize(IpcResponse.CreateError(request.Id, IpcErrorCodes.MissingPayloadValue, null, "CANCEL names the request's id (requestId)."));
        }

        if (!InstancePipe.IsKey(key)) return IpcJson.Serialize(pipe.Refuse(request, InstancePipe.KeyMissing, "a request carries a key of its own, 16 to 128 characters."));
        var holder = PipeBinding.Fingerprint(key!);
        string? waiting = null;
        lock (_state.Gate)
        {
            if (_state.Running.TryGetValue(target, out var running))
            {
                if (running.Binding.Key != holder) return IpcJson.Serialize(pipe.RefuseTaken(request));
                waiting = _state.WaitingFor.GetValueOrDefault(target);
            }
            else if (_state.Journal?.Find(target) is { } seen && seen.Binding.Key == holder)
            {
                return IpcJson.Serialize(IpcResponse.CreateSuccess(request.Id, new { cancelled = false, state = "ended" }));
            }
            else
            {
                return IpcJson.Serialize(pipe.RefuseTaken(request));
            }
        }

        _log.Info("pipe.cancelled", ("request", target));
        // RPC1c: a press still waiting for the window is taken back from it, its card gone and nothing run. One the person answered
        // meanwhile is under way, and the dispatcher's cancel reaches it as any press's does.
        if (waiting is not null && Confirmations.Withdraw(waiting) == PipeWithdrawal.Withdrawn)
        {
            return IpcJson.Serialize(IpcResponse.CreateSuccess(request.Id, new { cancelled = true, state = "withdrawn" }));
        }

        var answer = await _dispatcher.DispatchAsync(request, _lifetime.Token).ConfigureAwait(false);
        return IpcJson.Serialize(answer);
    }

    /// <summary>
    /// <c>RESULT { id, key }</c>: how the press stands, told only to the key that sent it, whichever connection asks. <c>waiting</c> while
    /// its card waits for the person (CONFIRMWAIT1a-i, the confirm-wait design §4.2), with when it was asked, whether a terminal waits on
    /// its line for it, and when a window first showed it; <c>running</c> while its Confirm is judged and once it runs; then
    /// <c>answered</c> with its ending, or <c>unknown</c>.
    /// </summary>
    public IpcResponse Result(IpcRequest request)
    {
        var id = InstancePipe.Field(request, "id");
        if (string.IsNullOrEmpty(id)) return IpcResponse.CreateError(request.Id, IpcErrorCodes.MissingPayloadValue, null, "RESULT names the request's id.");
        var key = InstancePipe.Field(request, "key");
        if (!InstancePipe.IsKey(key)) return pipe.Refuse(request, InstancePipe.KeyMissing, "RESULT carries the key its request was sent with.");
        var holder = PipeBinding.Fingerprint(key!);
        lock (_state.Gate)
        {
            if (_state.Running.TryGetValue(id, out var running))
            {
                if (running.Binding.Key != holder) return pipe.RefuseTaken(request);
                // CONFIRMWAIT1 (§15.2 item 9): its grounds are being read before its card; it waits, checking.
                if (_state.Reserved.TryGetValue(id, out var reserved))
                {
                    return IpcResponse.CreateSuccess(request.Id, new
                    {
                        state = "waiting", asked = reserved.Asked, checking = true,
                        waitedOn = _state.Connections.Contains(reserved.Connection) && _state.Owners.GetValueOrDefault(id) == reserved.Connection,
                        shownAt = (DateTimeOffset?)null,
                    });
                }

                if (WaitingCard(id) is { } card)
                {
                    return IpcResponse.CreateSuccess(request.Id, new
                    {
                        state = "waiting", asked = card.Asked, waitedOn = card.WaitedOn, shownAt = card.ShownAt,
                    });
                }

                return IpcResponse.CreateSuccess(request.Id, new { state = "running" });
            }

            return _state.Journal!.Find(id) switch
            {
                { } seen when seen.Binding.Key != holder => pipe.RefuseTaken(request),
                { State: PipeJournal.Seen.Answered, Answer: { } answer } =>
                    IpcResponse.CreateSuccess(request.Id, new { state = "answered", answer = JsonDocument.Parse(answer).RootElement.Clone() }),
                // CONFIRMWAIT1 (the confirm-wait design §15.3 item 10): begun and never ended, it stopped while under way, as WITHDRAW says.
                { State: PipeJournal.Seen.Interrupted } => IpcResponse.CreateSuccess(request.Id, new
                {
                    state = "answered",
                    answer = JsonSerializer.SerializeToElement(InterruptedAnswer(id), IpcJson.Options),
                }),
                _ => IpcResponse.CreateSuccess(request.Id, new { state = "unknown" }),
            };
        }
    }

    /// <summary>The card of a press still waiting for the person and not being judged, by its request's id; null otherwise. Under the lock.</summary>
    private PipeConfirmation? WaitingCard(string id) =>
        _state.WaitingFor.TryGetValue(id, out var confirmation)
            ? Confirmations.Now().Waiting.FirstOrDefault(each => each.Id == confirmation && !each.Checking)
            : null;

    /// <summary>
    /// <c>WITHDRAW { id, key }</c> (CONFIRMWAIT1a-i, the confirm-wait design §4.3): the asker takes back a press of its own that still
    /// waits for the person, under the confirmations' lock, where a Confirm is taken too, so the first of the two wins. Answered
    /// <c>withdrawn</c>; <c>running</c> where the person confirmed it meanwhile, its grounds being judged or it running, and nothing is
    /// cancelled; <c>ended</c> with its answer; another key's, or an id the pipe does not know, refused <see cref="InstancePipe.IdTaken"/>.
    /// Never the kit's <c>CANCEL</c>, which reaches a confirmed press and aborts it.
    /// </summary>
    public IpcResponse Withdraw(IpcRequest request)
    {
        var id = InstancePipe.Field(request, "id");
        if (string.IsNullOrEmpty(id)) return IpcResponse.CreateError(request.Id, IpcErrorCodes.MissingPayloadValue, null, "WITHDRAW names the request's id.");
        var key = InstancePipe.Field(request, "key");
        if (!InstancePipe.IsKey(key)) return pipe.Refuse(request, InstancePipe.KeyMissing, "WITHDRAW carries the key its request was sent with.");
        var holder = PipeBinding.Fingerprint(key!);
        string? waiting = null;
        PipeReserved? reserved = null;
        lock (_state.Gate)
        {
            if (_state.Running.TryGetValue(id, out var running))
            {
                if (running.Binding.Key != holder) return pipe.RefuseTaken(request);
                // CONFIRMWAIT1 (§15.2 item 9): one whose grounds are still being read is taken back at once, before it has a card.
                if (!_state.Reserved.Remove(id, out reserved))
                {
                    waiting = _state.WaitingFor.GetValueOrDefault(id);
                    // Marked before the confirmations are asked, so the press's ending says it was this route's.
                    if (waiting is not null) _state.Withdrawn.Add(id);
                }
            }
            else if (_state.Journal?.Find(id) is { } seen && seen.Binding.Key == holder)
            {
                return IpcResponse.CreateSuccess(request.Id, new
                {
                    state = "ended",
                    answer = seen.State == PipeJournal.Seen.Answered && seen.Answer is { } answer
                        ? JsonDocument.Parse(answer).RootElement.Clone()
                        : JsonSerializer.SerializeToElement(InterruptedAnswer(id), IpcJson.Options),
                });
            }
            else
            {
                return pipe.RefuseTaken(request);
            }
        }

        if (reserved is not null)
        {
            reserved.Reading.Cancel();
            Confirmations.Let(reserved.Place);
            pipe.Asking.EndReserved(reserved, IpcResponse.CreateError(id, InstancePipe.Withdrawn, null,
                "This terminal took its request back before the person answered it. Nothing was done."));
            _log.Info("pipe.withdrawn", ("request", id));
            return IpcResponse.CreateSuccess(request.Id, new { state = "withdrawn" });
        }

        var withdrawal = waiting is null ? PipeWithdrawal.Checking : Confirmations.Withdraw(waiting);
        if (withdrawal == PipeWithdrawal.Withdrawn)
        {
            _log.Info("pipe.withdrawn", ("request", id));
            return IpcResponse.CreateSuccess(request.Id, new { state = "withdrawn" });
        }

        lock (_state.Gate)
        {
            _state.Withdrawn.Remove(id);
        }

        // CONFIRMWAIT1 (§15.2 item 8): its record could not say it was taken back, so it was not, and it still waits.
        if (withdrawal == PipeWithdrawal.Unrecorded)
        {
            return pipe.Refuse(request, InstancePipe.Unrecorded, "Daoris could not keep that this was taken back, so it still waits for the person; send WITHDRAW again.");
        }

        return IpcResponse.CreateSuccess(request.Id, new { state = "running" });
    }

    /// <summary>The answer of a press begun by an instance that stopped before it ended: whether it was done is not known.</summary>
    private static IpcResponse InterruptedAnswer(string id) => IpcResponse.CreateError(id, InstancePipe.Interrupted, null,
        "Daoris stopped while this request was under way, so whether it was done is not known; it was not run again.");
}
