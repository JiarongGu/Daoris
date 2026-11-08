using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A terminal's hold of a conversation: the person's Confirm
/// in the window, once, binds one pipe connection to one conversation, and from then that connection's words in the verb's form, its
/// turn stops and its finish for that conversation run with no further card, and the pipe forwards it the conversation's console lines,
/// its events and its end.
/// </summary>
/// <remarks>
/// <para><b>How one is made.</b> A start sent with <c>hold: true</c> beside its envelope (<see cref="Opens"/>) is carded as every person's
/// press is, the card saying the hold too, never under the standing confirmation, and holds the conversation its answer names; the pipe's
/// <c>HOLD { session }</c> cards a hold of one Daoris already has. One connection holds one conversation and one conversation has one
/// holder: a second is refused <see cref="HeldElsewhere"/>, naming the terminal's process.</para>
/// <para><b>How one ends.</b> The terminal's <c>LET_GO</c>, the window's <c>SESSION_LET_GO</c>, the conversation's end, or the connection
/// closing, a restart included. Each end is told to the connection where it is still there (<see cref="HeldType"/>, <c>held: ended</c>,
/// its reason) after what is forwarded before it. The start and the end are logged <see cref="LogEvent"/>, by codes alone.</para>
/// <para>Holds live in the pipe's state (<see cref="PipeState.Holds"/>) under its gate; every window is told them whole at each change
/// (<see cref="Event"/>).</para>
/// </remarks>
public sealed class PipeHolding(InstancePipe pipe, IEventBus bus) : IDisposable
{
    /// <summary>The machine log's event for a hold's start (<c>state: held</c>) and its end (<c>state: ended</c>, <c>why</c>).</summary>
    public const string LogEvent = "person.hold";

    /// <summary>The event every window is told the holds by, and the window's read of them.</summary>
    public const string Event = "SESSION_HOLDS";

    /// <summary>The pipe's notification that a hold began (<c>held: held</c>) or ended (<c>held: ended</c>, <c>reason</c>).</summary>
    public const string HeldType = "HELD";

    /// <summary>The pipe's notification that forwarded lines were dropped, naming the last console sequence dropped.</summary>
    public const string GapType = "GAP";

    /// <summary>Another connection holds the conversation: <c>process</c> and <c>pid</c> name its terminal.</summary>
    public const string HeldElsewhere = "held";

    /// <summary>This connection holds another conversation already.</summary>
    public const string Holding = "holding";

    /// <summary><c>LET_GO</c> from a connection that holds nothing.</summary>
    public const string NotHeld = "not-held";

    /// <summary><c>HOLD</c> of a conversation Daoris does not have.</summary>
    public const string UnknownSession = "unknown-session";

    /// <summary>The ends' reasons.</summary>
    public const string LetGo = "let-go", Window = "window", Ended = "ended", Closed = "closed";

    private static readonly string[] Starts = ["START_CHAT", "SESSION_START_FROM", "SESSION_HAND_OVER"];
    private static readonly string[] Forwarded = ["SESSION_OUTPUT", "SESSION_EVENTS", DriverLoop.EndedType];

    private readonly PipeState _state = pipe.State;
    private readonly Dictionary<string, PipeConnection> _asking = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _heard = [];
    private long _seq;

    /// <summary>Hear the conversations' lines, events and ends, once, as the pipe is served.</summary>
    internal void Listen()
    {
        foreach (var type in Forwarded) _heard.Add(bus.Subscribe(StatePublisher.Module, type, Forward));
    }

    /// <summary>Whether <paramref name="route"/> of <paramref name="module"/> opens a conversation a <c>hold: true</c> holds.</summary>
    public static bool Opens(string? module, string? route) =>
        string.Equals(module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) && Starts.Contains(route, StringComparer.Ordinal);

    /// <summary>The conversation <paramref name="connection"/> holds, or null.</summary>
    internal string? HeldBy(PipeConnection connection)
    {
        lock (_state.Gate)
        {
            return _state.Holds.Values.FirstOrDefault(each => each.Connection == connection)?.Session;
        }
    }

    /// <summary>
    /// Whether the hold covers <paramref name="request"/>: on the connection that holds the conversation it names, its words in the verb's
    /// form (<c>said: true</c>, no <c>question</c>, <c>draft</c> or <c>preface</c>), a turn's stop in either form, or its finish.
    /// </summary>
    internal bool Covers(PipeConnection connection, IpcRequest request)
    {
        if (!string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase)) return false;
        if (InstancePipe.Field(request, "id") is not { } session) return false;
        lock (_state.Gate)
        {
            if (!_state.Holds.TryGetValue(session, out var hold) || hold.Connection != connection) return false;
        }

        return request.Type switch
        {
            "SESSION_INPUT" => Said(request.Payload),
            "CANCEL_TURN" or "END_CHAT" => true,
            _ => false,
        };
    }

    /// <summary>A start asked with <c>hold: true</c> on a connection that holds a conversation already: its refusal, before any card.</summary>
    internal IpcResponse? RefuseStart(IpcRequest request, PipeConnection connection) =>
        HeldBy(connection) is { } held ? RefuseHolding(request, held) : null;

    /// <summary>A start's answer, once it names the conversation it opened, holding that conversation for <paramref name="connection"/>.</summary>
    internal async Task<string> AfterOpenedAsync(Task<string> answer, PipeConnection connection)
    {
        var text = await answer.ConfigureAwait(false);
        if (Opened(text) is { } session) Bind(connection, session);
        return text;
    }

    /// <summary>
    /// The pipe's <c>HOLD { session }</c>: refused where the conversation is held, this connection holds another, or Daoris does not have
    /// it; else carded, and held once the person confirms it.
    /// </summary>
    internal async Task<string> AskAsync(IpcRequest request, PipeConnection connection)
    {
        if (pipe.Closing) return IpcJson.Serialize(pipe.RefuseStopping(request));
        if (InstancePipe.Field(request, "session") is not { Length: > 0 } session)
        {
            return IpcJson.Serialize(IpcResponse.CreateError(request.Id ?? "", IpcErrorCodes.InvalidPayloadValue, null, "HOLD names its conversation, `session`."));
        }

        if (Refusal(request, connection, session) is { } refused) return IpcJson.Serialize(refused);
        if (HeldBy(connection) == session) return IpcJson.Serialize(Answer(request, session));
        if (await HasAsync(session).ConfigureAwait(false) is { } unread) return IpcJson.Serialize(pipe.Refuse(request, unread,
            unread == UnknownSession ? "Daoris has no such conversation; nothing was asked." : "Daoris could not read this conversation just now; nothing was asked."));
        if (pipe.Confirmations.Hold() is not { } place)
        {
            return IpcJson.Serialize(pipe.Refuse(request, InstancePipe.Full, "As many presses as may wait for the person already do; this one was not asked."));
        }

        // Named before it is shown, so a stop or a closing connection that comes as it is entered finds it.
        var cardId = Guid.NewGuid().ToString("N");
        lock (_state.Gate)
        {
            _asking[cardId] = connection;
        }

        var (shown, ended) = pipe.Confirmations.Ask(
            place, InstancePipe.Module, "HOLD", [new PipeConfirmationField("/session", "string", session)], connection.Asker,
            @as: new PipeAsAsked(cardId, Hold: true));

        // A connection that closes while it is shown has nothing to hold for: its card is taken back. One still there is told it waits.
        if (!Open(connection)) pipe.Confirmations.Withdraw(shown.Id);
        else _ = connection.WriteAsync(Told(InstancePipe.Confirming, new { id = request.Id }));
        // A stop that began before the card was shown did not find it to end.
        if (pipe.Stopped) pipe.Confirmations.Lose(shown.Id);
        var ending = await ended.ConfigureAwait(false);
        lock (_state.Gate)
        {
            _asking.Remove(shown.Id);
        }

        return IpcJson.Serialize(ending.End switch
        {
            PipeConfirmationEnd.Confirmed => Bind(connection, session) ? Answer(request, session) : Refusal(request, connection, session)
                ?? pipe.Refuse(request, InstancePipe.Lost, "This terminal's connection ended before the hold began; nothing is held."),
            PipeConfirmationEnd.Refused => pipe.Refuse(request, InstancePipe.Refused, "The person refused this in Daoris's window. Nothing is held."),
            PipeConfirmationEnd.Withdrawn => pipe.Refuse(request, InstancePipe.Withdrawn, "This terminal's connection ended before the person answered. Nothing is held."),
            _ => pipe.Refuse(request, InstancePipe.Lost, "Daoris closed while this waited; nothing is held."),
        });
    }

    /// <summary>The pipe's <c>LET_GO</c>: this connection's hold ended, the conversation going on in Daoris.</summary>
    internal IpcResponse LetGoOf(IpcRequest request, PipeConnection connection)
    {
        PipeHold? hold;
        lock (_state.Gate)
        {
            hold = _state.Holds.Values.FirstOrDefault(each => each.Connection == connection);
            if (hold is not null) _state.Holds.Remove(hold.Session);
        }

        if (hold is null) return pipe.Refuse(request, NotHeld, "This terminal holds no conversation.");
        End(hold, LetGo);
        return IpcResponse.CreateSuccess(request.Id, new { held = false, session = hold.Session });
    }

    /// <summary>The window's <c>SESSION_LET_GO</c>: the person takes <paramref name="session"/> back; false where no terminal holds it.</summary>
    public bool TakeBack(string session)
    {
        PipeHold? hold;
        lock (_state.Gate)
        {
            if (_state.Holds.Remove(session, out hold) is false) return false;
        }

        End(hold!, Window);
        return true;
    }

    /// <summary>A connection ended: its hold ends, and a hold it waits on the person for is taken back.</summary>
    internal void ConnectionEnded(PipeConnection connection)
    {
        PipeHold[] holds;
        string[] cards;
        lock (_state.Gate)
        {
            holds = [.. _state.Holds.Values.Where(each => each.Connection == connection)];
            foreach (var hold in holds) _state.Holds.Remove(hold.Session);
            cards = [.. _asking.Where(each => each.Value == connection).Select(each => each.Key)];
        }

        foreach (var card in cards) pipe.Confirmations.Withdraw(card);
        foreach (var hold in holds) End(hold, Closed);
    }

    /// <summary>
    /// The stop's step for the holds still waiting for the person: each card ends <see cref="PipeConfirmationEnd.Lost"/>, so its line
    /// is answered at once. Such a card is no press under way (<see cref="PipeState.WaitingFor"/> names none), so the close's other
    /// step does not reach it, and its line would keep the drain waiting its whole bound.
    /// </summary>
    internal void CloseAsks()
    {
        string[] cards;
        lock (_state.Gate)
        {
            cards = [.. _asking.Keys];
        }

        foreach (var card in cards) pipe.Confirmations.Lose(card);
    }

    /// <summary>Every hold now, as the window reads them.</summary>
    public PipeHoldsNow Now()
    {
        lock (_state.Gate)
        {
            return new PipeHoldsNow(_seq, [.. _state.Holds.Values.Select(each => new PipeHeld(each.Session, each.Connection.Asker, each.Since))]);
        }
    }

    public void Dispose()
    {
        foreach (var heard in _heard) heard.Dispose();
        _heard.Clear();
    }

    /// <summary>A notification of the pipe's own, as the kit's envelope carries one.</summary>
    internal static string Told(string type, object payload) => Envelope(InstancePipe.Module, type, payload);

    private static string Envelope(string module, string type, object? payload) => JsonSerializer.Serialize(
        new
        {
            category = IpcCategories.Notification,
            id = Guid.NewGuid().ToString("N"),
            payload = new[] { new { module, type, payload } },
            timestamp = DateTimeOffset.UtcNow,
        },
        IpcJson.Options);

    /// <summary>Holds <paramref name="session"/> for <paramref name="connection"/>, once: false where either is taken or the connection has gone.</summary>
    private bool Bind(PipeConnection connection, string session)
    {
        lock (_state.Gate)
        {
            if (!_state.Connections.Contains(connection)) return false;
            if (_state.Holds.TryGetValue(session, out var held)) return held.Connection == connection;
            if (_state.Holds.Values.Any(each => each.Connection == connection)) return false;
            var forwarding = new PipeForwarding(connection, session, pipe.Limits.Forwarded);
            // Told first, so no line of the conversation reaches the terminal before it hears the hold began.
            forwarding.Tell(Told(HeldType, new { session, held = "held" }), null);
            _state.Holds[session] = new PipeHold(session, connection, pipe.Clock.GetUtcNow(), forwarding);
        }

        pipe.Log.Info(LogEvent, ("state", "held"), ("door", PipeDoor.Terminal));
        TellWindows();
        return true;
    }

    private void End(PipeHold hold, string reason)
    {
        pipe.Log.Info(LogEvent, ("state", "ended"), ("why", reason), ("door", PipeDoor.Terminal));
        hold.Forwarding.Close(reason == Closed ? null : Told(HeldType, new { session = hold.Session, held = "ended", reason }));
        TellWindows();
    }

    private void TellWindows()
    {
        PipeHoldsNow now;
        lock (_state.Gate)
        {
            _seq++;
            now = new PipeHoldsNow(_seq, [.. _state.Holds.Values.Select(each => new PipeHeld(each.Session, each.Connection.Asker, each.Since))]);
        }

        bus.Emit(StatePublisher.Module, Event, now);
    }

    /// <summary>A conversation's line, event or end, forwarded to the connection that holds it, and to nobody else; its end ends the hold.</summary>
    private Task Forward(EventMessage message)
    {
        var told = JsonSerializer.SerializeToElement(message.Payload, IpcJson.Options);
        if (Text(told, "session") is not { } session) return Task.CompletedTask;
        PipeHold? hold;
        lock (_state.Gate)
        {
            _state.Holds.TryGetValue(session, out hold);
        }

        if (hold is null) return Task.CompletedTask;
        hold.Forwarding.Tell(Envelope(message.Module, message.Type, told), LastSequence(told));
        if (message.Type != DriverLoop.EndedType) return Task.CompletedTask;
        lock (_state.Gate)
        {
            if (!_state.Holds.TryGetValue(session, out var now) || now != hold) return Task.CompletedTask;
            _state.Holds.Remove(session);
        }

        End(hold, Ended);
        return Task.CompletedTask;
    }

    private bool Open(PipeConnection connection)
    {
        lock (_state.Gate)
        {
            return _state.Connections.Contains(connection);
        }
    }

    /// <summary>Whether Daoris has <paramref name="session"/>: null where it does, else the refusal's code.</summary>
    private async Task<string?> HasAsync(string session)
    {
        if (pipe.Grounds is not { } world) return InstancePipe.UnreadGrounds;
        try
        {
            return await world.SessionAsync(session, pipe.Lifetime.Token).ConfigureAwait(false) is null ? UnknownSession : null;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return InstancePipe.UnreadGrounds;
        }
    }

    private IpcResponse? Refusal(IpcRequest request, PipeConnection connection, string session)
    {
        PipeHold? held;
        string? other;
        lock (_state.Gate)
        {
            _state.Holds.TryGetValue(session, out held);
            other = _state.Holds.Values.FirstOrDefault(each => each.Connection == connection && each.Session != session)?.Session;
        }

        if (held is not null && held.Connection != connection)
        {
            var asker = held.Connection.Asker;
            return pipe.Refuse(request, HeldElsewhere,
                $"Another terminal holds this conversation{(asker.Pid is { } pid ? $" ({asker.Process ?? "process"} {pid})" : "")}; it lets go first, or the person takes it back in Daoris's window.",
                ("process", asker.Process ?? ""), ("pid", asker.Pid?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""));
        }

        return other is null ? null : RefuseHolding(request, other);
    }

    private IpcResponse RefuseHolding(IpcRequest request, string held) =>
        pipe.Refuse(request, Holding, "This terminal holds another conversation already; it lets go of that one first.", ("session", held));

    private static IpcResponse Answer(IpcRequest request, string session) => IpcResponse.CreateSuccess(request.Id, new { held = true, session });

    /// <summary>The conversation a start's answer names, where it succeeded.</summary>
    private static string? Opened(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True
                && root.TryGetProperty("data", out var data) ? Text(data, "sessionId") : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Said(JsonElement? payload) =>
        payload is { ValueKind: JsonValueKind.Object } sent && sent.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.True
        && !Carries(sent, "question") && !Carries(sent, "draft") && !Carries(sent, "preface");

    private static bool Carries(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>The last console sequence a forwarded batch carries, or null.</summary>
    private static long? LastSequence(JsonElement told) =>
        told.ValueKind == JsonValueKind.Object && told.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array
        && lines.GetArrayLength() > 0 && lines[lines.GetArrayLength() - 1].TryGetProperty("sequence", out var sequence)
        && sequence.TryGetInt64(out var at) ? at : null;
}
