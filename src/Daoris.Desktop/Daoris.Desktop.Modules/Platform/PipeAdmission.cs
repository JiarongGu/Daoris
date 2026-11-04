using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A request on a connection that said its hello, admitted or refused (the terminal RPC design §4.2, §6): the pipe's own answered
/// now, the census's class judged, the id bound to its route, payload and key, and a person's press judged by the standing confirmation
/// and then reserved for its card. It binds the id in the pipe's state (<see cref="PipeState"/>) under its gate.
/// </summary>
internal sealed class PipeAdmission(InstancePipe pipe)
{
    private readonly PipeState _state = pipe.State;
    private readonly MachineLog _log = pipe.Log;
    private readonly PipeCensus _census = pipe.Census;
    private readonly TimeProvider _clock = pipe.Clock;

    private PipeConfirmations Confirmations => pipe.Confirmations;

    /// <summary>
    /// A request on a connection that said its hello: answered now (the pipe's own, a refusal, an id answered before), or later,
    /// once the press it begins or joins answers. A press is admitted only where the connection has room for one more under way.
    /// </summary>
    public (string? Now, Task<string>? Later) Accept(IpcRequest request, string? key, PipeConnection connection)
    {
        if (string.Equals(request.Module, InstancePipe.Module, StringComparison.OrdinalIgnoreCase))
        {
            return (IpcJson.Serialize(request.Type switch
            {
                "HELLO" => pipe.Hello(request, connection),
                "ROUTES" => IpcResponse.CreateSuccess(request.Id, new { routes = _census.Routes.Select(Listed) }),
                "RESULT" => pipe.TakeBacks.Result(request),
                "WITHDRAW" => pipe.TakeBacks.Withdraw(request),
                _ => IpcResponse.CreateError(request.Id, IpcErrorCodes.NoRoute, null, $"the pipe has no route {request.Type}."),
            }), null);
        }

        // RPC1h (§6.2): a terminal's Ctrl+C, taken for its own press alone. Before the census, which names no kit module, and before
        // the stop's refusal, which a press under way outlives: a cancel is how a terminal ends one while the instance closes.
        if (string.Equals(request.Module, InstancePipe.RequestsModule, StringComparison.OrdinalIgnoreCase) && request.Type == "CANCEL")
        {
            return (null, pipe.TakeBacks.CancelAsync(request, key));
        }

        if (pipe.Closing) return (IpcJson.Serialize(pipe.RefuseStopping(request)), null);
        var @class = _census.Of(request.Module, request.Type, request.Payload);
        switch (@class)
        {
            case null:
                return (IpcJson.Serialize(pipe.Refuse(request, InstancePipe.Unclassed, "this route is not answered over the pipe.")), null);
            case PipeClass.Window:
                return (IpcJson.Serialize(pipe.Refuse(request, InstancePipe.WindowOnly, "only Daoris's window does this.")), null);
        }

        // DRAINHOLD1: while an update drains, a press that would add to what it waits on, or that its close would cut, is refused as the
        // update's; a read, a stop, a take-back or an answer goes on.
        if (pipe.Holds(request.Module, request.Type, @class)) return (IpcJson.Serialize(pipe.RefuseStopping(request)), null);

        if (request.Id is not { Length: > 0 and <= 128 } id)
        {
            return (IpcJson.Serialize(IpcResponse.CreateError(request.Id ?? "", IpcErrorCodes.InvalidPayloadValue, null, "a request names its id.")), null);
        }

        if (!InstancePipe.IsKey(key)) return (IpcJson.Serialize(pipe.Refuse(request, InstancePipe.KeyMissing, "a request carries a key of its own, 16 to 128 characters.")), null);
        if (!connection.HasRoom) return (IpcJson.Serialize(pipe.Refuse(request, InstancePipe.Busy, "this terminal has as much under way as Daoris takes from one; send it again once one answers.")), null);

        // SUPERVISOR1a-ii (D208 §2.2, the supervisor design §11 row 1a-ii): a role's act is judged by the judge's own role method, before
        // the open path and before any card, under the same lock and caller look, then by the holder's proof over this connection's
        // nonce, which this act uses up. Anything it does not cover is refused, logged, never asked.
        RoleJudgement? role = null;
        if (@class == PipeClass.Role)
        {
            role = pipe.StandingConfirmation.JudgeRole(request.Module, request.Type, request.Payload, connection.Client, connection.TakeNonce());
            if (!role.Covered)
            {
                // STEWARDROLE1a-i (D209 §5.2): a role's act's refusal is `role.refused`; the supervisor's routes keep their own line.
                if (request.Type == RoleRoutes.Act) _log.Info("role.refused", ("why", role.Why ?? ""), ("door", PipeDoor.Terminal));
                else _log.Info("supervisor.refused", ("why", role.Why ?? ""), ("door", PipeDoor.Terminal));
                return (IpcJson.Serialize(pipe.Refuse(request, role.Why ?? InstancePipe.Unclassed, RoleRefusal(role.Why))), null);
            }
        }

        // RPC1c: a person's press is shown to the window with every value it carries, so one the card could not show whole is not asked.
        IReadOnlyList<PipeConfirmationField>? fields = null;
        if (@class == PipeClass.Person)
        {
            if (PipeCensus.SecretRefusal(request.Type, request.Payload) is { } secret) return (IpcJson.Serialize(pipe.Refuse(request, InstancePipe.Shape, $"{Capitalized(secret)} Nothing was asked.")), null);
            var (shown, unshown) = PipeConfirmations.FieldsOf(request.Payload);
            if (unshown is not null) return (IpcJson.Serialize(pipe.Refuse(request, InstancePipe.Shape, $"{Capitalized(unshown)} Nothing was asked.")), null);
            fields = shown;
        }

        var binding = new PipeBinding(
            $"{request.Module}.{request.Type}",
            PipeBinding.Fingerprint(request.Payload is { ValueKind: not JsonValueKind.Undefined } payload ? payload.GetRawText() : ""),
            PipeBinding.Fingerprint(key!));
        Task<string> answer;
        TaskCompletionSource<string>? mine = null;
        lock (_state.Gate)
        {
            if (_state.Running.TryGetValue(id, out var running))
            {
                if (running.Binding != binding) return (IpcJson.Serialize(pipe.RefuseTaken(request)), null);
                // Sent again while it runs, by the key that sent it: the one press's answer, and its progress to this connection.
                _state.Owners[id] = connection;
                answer = running.Answer;
                // CONFIRMWAIT1a-i (§8.1): a terminal waits on its line for it again. The confirmations' lock is only ever taken inside
                // this one, never around it.
                if (_state.WaitingFor.TryGetValue(id, out var waiting)) Confirmations.WaitedOn(waiting, true);
            }
            else if (_state.Journal!.Find(id) is { } seen)
            {
                if (seen.Binding != binding) return (IpcJson.Serialize(pipe.RefuseTaken(request)), null);
                return (seen.State == PipeJournal.Seen.Answered
                    ? seen.Answer!
                    : IpcJson.Serialize(IpcResponse.CreateError(id, InstancePipe.Interrupted, null,
                        "Daoris stopped while this request was under way, so whether it was done is not known; it was not run again.")), null);
            }
            else
            {
                mine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                answer = mine.Task;
                _state.Running[id] = (binding, answer);
                _state.Owners[id] = connection;
            }
        }

        if (mine is null) return (null, answer);
        if (fields is null)
        {
            _ = pipe.RunAsync(request, id, binding, mine, role: role, client: connection.Client);
            return (null, answer);
        }

        // TERMTRUST1a (D201, the terminal-trust design §5 step 3): the person's standing confirmation is judged first, under its lock, and a
        // press it covers is admitted there, once: run as a confirmed press is, said to the log and the terminal, with no card. A revoke
        // after the admission does not recall it; a press it does not cover asks as before, saying why where the grant names it.
        var judged = pipe.StandingConfirmation.Judge(request.Module, request.Type, request.Payload, connection.Client);
        if (judged.Covered)
        {
            _log.Info(PipeConfirmations.LogEvent, ("state", "standing"), ("route", $"{request.Module}.{request.Type}"), ("door", PipeDoor.Terminal));
            _ = connection.WriteAsync(Told(InstancePipe.Standing, new { id }));
            _ = pipe.RunAsync(request, id, binding, mine, standing: true);
            return (null, answer);
        }

        var why = judged.Said ? judged.Why : null;

        // CONFIRMWAIT1a-i (the confirm-wait design §5.2): after the standing judgement, so a press it admits is never refused here, a
        // person's press whose route names no grounds is refused at once, no read made; then a place among the ten is held, told to no
        // window yet, so one past them is refused before any read. The grounds are read in the press's own task, outside every lock.
        if (_census.Targets.Row(request.Module, request.Type, request.Payload) is not { } row)
        {
            return (Settled(request, id, mine, pipe.Refuse(request, InstancePipe.Ungrounded,
                "Daoris does not know what this press rests on, so it cannot wait for the person; nothing was asked.")), null);
        }

        if (Confirmations.Hold() is not { } place)
        {
            return (Settled(request, id, mine, pipe.Refuse(request, InstancePipe.Full, FullSentence())), null);
        }

        // CONFIRMWAIT1 (the confirm-wait design §15.2 item 9): from here the press is a reservation, its id said to its terminal before its
        // grounds are read, so a withdrawal, a close or a lost line has a press to end; every ending from here is journaled.
        var reserved = new PipeReserved(request, binding, mine, connection, place, _clock.GetUtcNow());
        lock (_state.Gate)
        {
            _state.Reserved[id] = reserved;
        }

        _ = connection.WriteAsync(Waits(id, why, checking: true));
        _ = pipe.Asking.AskThenRunAsync(reserved, row, fields, why);
        return (null, answer);
    }

    /// <summary>The refusal of a press whose id was bound and never asked: its place let go, and what the terminal is answered.</summary>
    private string Settled(IpcRequest request, string id, TaskCompletionSource<string> mine, IpcResponse refusal)
    {
        var text = IpcJson.Serialize(refusal);
        lock (_state.Gate)
        {
            _state.Running.Remove(id);
            _state.Owners.Remove(id);
        }

        mine.SetResult(text);
        return text;
    }

    /// <summary>The bound's refusal (§9.3), naming how long the oldest has waited, so the asker can withdraw its own or the person answer.</summary>
    private string FullSentence()
    {
        var bound = Confirmations.Bound == PipeConfirmations.DefaultBound
            ? "Ten"
            : Confirmations.Bound.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var oldest = Confirmations.Oldest() is { } asked ? $", the oldest asked {Ago(_clock.GetUtcNow() - asked)}" : "";
        return $"{bound} presses already wait for the person's confirmation{oldest}; this one was not asked. Nothing was kept.";
    }

    /// <summary>A wait's length in the terminal's words: <c>2 hours ago</c>.</summary>
    private static string Ago(TimeSpan waited) => waited switch
    {
        { TotalMinutes: < 1 } => "just now",
        { TotalHours: < 1 } => Count((int)waited.TotalMinutes, "minute"),
        { TotalDays: < 1 } => Count((int)waited.TotalHours, "hour"),
        _ => Count((int)waited.TotalDays, "day"),
    };

    private static string Count(int count, string unit) => count == 1 ? $"1 {unit} ago" : $"{count} {unit}s ago";

    /// <summary>
    /// The notification that tells the terminal its press waits for the person in the window (RPC1c), naming its request; no time, since
    /// none ends it (CONFIRMWAIT1a-i). With <c>standing</c>, why the person's standing confirmation, on and naming the press, did not cover
    /// it (TERMTRUST1a, §5 step 5), a field an older terminal ignores.
    /// </summary>
    /// <param name="checking">
    /// That its grounds are being read before its card (CONFIRMWAIT1, §15.2 item 9): the terminal hears its id then, before any read.
    /// </param>
    private static string Waits(string id, string? standing, bool checking = false) => (standing, checking) switch
    {
        (null, false) => Told(InstancePipe.Confirming, new { id }),
        (null, true) => Told(InstancePipe.Confirming, new { id, checking }),
        (_, false) => Told(InstancePipe.Confirming, new { id, standing }),
        _ => Told(InstancePipe.Confirming, new { id, standing, checking }),
    };

    /// <summary>A notification of the pipe's own to the terminal that sent a press.</summary>
    private static string Told(string type, object payload) => JsonSerializer.Serialize(
        new
        {
            category = IpcCategories.Notification,
            id = Guid.NewGuid().ToString("N"),
            payload = new[] { new { module = InstancePipe.Module, type, payload } },
            timestamp = DateTimeOffset.UtcNow,
        },
        IpcJson.Options);

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>A role's act refused at the door (SUPERVISOR1a-ii), in the terminal's words, by its code. Nothing was kept.</summary>
    private static string RoleRefusal(string? why) => why switch
    {
        SupervisorWhy.NotStaffed => "No terminal holds this role: the owner staffs it with the terminal among the roles. Nothing was done.",
        RoleWhy.NotRole => "That is the owner's to do; the role does not hold it. Nothing was done.",
        RoleWhy.NotGranted => "The role's acts as the owner set them do not name that act. Nothing was done.",
        RoleWhy.OwnerSet => "The owner acted on this themselves since Daoris started, so it is theirs until Daoris restarts. Nothing was done.",
        RoleWhy.HolderConfig =>
            "This act did not come from the role's terminal as Daoris started it, on Daoris's own configuration; its terminal is ended. Nothing was done.",
        SupervisorWhy.NotHolder => "This terminal did not prove the supervisor's key for this connection and this answer. Nothing was kept.",
        SupervisorWhy.NotStarted => "The role's terminal is not started since Daoris started: the owner starts it in the window. Nothing was kept.",
        SupervisorWhy.RoleChanged => "The role this answer names is not the one holding now: read the question again. Nothing was kept.",
        SupervisorWhy.Form => "The answer is not the supervisor's form: an id, the question, the generation, the proof, one choice and 1 to 5 reasons, each with its source. Nothing was kept.",
        StandingWhy.InAgentJob => "An agent Daoris runs does not answer as the supervisor. Nothing was kept.",
        StandingWhy.Untold => "Daoris could not tell this terminal is outside every agent it runs. Nothing was kept.",
        _ => "No appointment covers this. Nothing was kept.",
    };

    /// <summary>A route as <c>ROUTES</c> lists it: a route classed by its payload says its rule beside its class.</summary>
    private static object Listed(PipeRoute route) => new
    {
        module = route.Module,
        route = route.Route,
        @class = route.Class,
        when = route.When is { } rule ? new { field = rule.Field, values = rule.Values, @class = rule.Class } : null,
    };
}
