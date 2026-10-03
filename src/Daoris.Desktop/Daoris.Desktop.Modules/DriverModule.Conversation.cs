using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A conversation, the page's `bridge/conversation.ts` (MOD5): its record a page at a time (D76 §2), its
/// start (D49 §3), the person's messages with their files, finishing it, stopping a turn, its options and
/// its queue (CONV4, AGT6b).
/// </summary>
public sealed partial class DriverModule
{
    // A session's conversation (D76 §2): the newest page, an earlier one (`before`), or only
    // what is newer (`after`) — how a page opens a session after a restart, loads earlier
    // turns, and closes a gap in the live events. From the record under the home, so it
    // answers whether or not this app was running when the session spoke.
    [DriverRoute("SESSION_HISTORY")]
    private object? SessionHistory(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var page = Number(request, "after") is { } after
            ? _loop.Events.After(id, after)
            : _loop.Events.Page(id, Number(request, "before"), (int)(Number(request, "limit") ?? SessionEvents.PageLimit));
        return new { Session = id, Events = page.Events.ToArray(), page.Earlier, page.Latest, page.Opening, page.FirstFailure };
    }

    // A conversation in a repository (D49 §3). The record is the service's and the lock is the
    // ledger's; what only this side can do is put a harness behind it — a process on this
    // machine, which never leaves it (D46 §7).
    [DriverRoute("START_CHAT")]
    private async Task<object?> StartChatAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var chat = _loop.Chat ?? throw NotReady();

        var config = DriverConfig.Load(_loop.ConfigPath);
        // A blank adapter is none named, and the machine's own is the answer. This route took a
        // blank one as a name (REV3 CLEAN1); the page happens never to send one.
        var adapter = Optional(request, "adapter") ?? config.Adapter;

        var start = await chat.StartAsync(
            repository, adapter, config,
            // The end of a conversation is news the page wants without asking: the drawer is
            // probably open, and a record that moved silently reads as one that hung.
            onEnded: (session, state) => DriverLoop.Ended(_events, session, state),
            // The per-session picker (D49 §4). Absent takes the workspace's default, then the
            // machine's, then the harness's own configuration home.
            profile: Optional(request, "profile"),
            // The per-conversation tree choice (D51). Absent falls back to the repository's
            // standing opt-in, which the runner reads from the same config.
            ownTree: Flag(request, "ownTree"),
            ct: cancellationToken);

        _loop.Nudge();
        return new { start.SessionId, start.Message };
    }

    // The person's words to a session, whatever its state (MSG1d, D137 §5.3): `{ sent, reaches, why }`. A running session
    // hears them at its door as before (SESS3, D136, CONV4a); one that parked or ended has them kept on its record and the
    // loop nudged, so the same session goes on with them (MSG1b); words said as one winds up are held until its record ends,
    // never refused. What never goes on is `sent: false` with its code. 🔴 A session running here that takes no input at
    // all is REFUSED instead, in the driver's words (INT4h): false would tell a stale page it ended when it is running.
    [DriverRoute("SESSION_INPUT")]
    private async Task<object?> SessionInputAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var text = PayloadHelper.GetRequiredValue<string>(request.Payload, "text");
        // What the person attached, kept for its session before the words go (CONV4c).
        var files = request.Payload is { } payload ? FilesOf(payload) : [];
        // Where the person is (HELP1b), which a conversation's agent is handed ahead of the words; absent for most.
        var said = await _loop.Words.SayAsync(id, text, files, Optional(request, "preface"), door: "screen", cancellationToken)
            .ConfigureAwait(false) ?? throw NotReady();
        // Words a running session took are kept on its ask now (DRIFT1a2); words kept on a record are kept there once a
        // session takes them (MSG1a's taken door), so they are never posted twice.
        if (said.Running) _loop.Words.KeepOnAsk(id, text);
        return new { said.Sent, said.Reaches, said.Why };
    }

    // A go-ahead a parked session asked, answered on the session's own page (KNOWUSE1a2, D135 §2): one press where the ask's
    // page and the box took two. The go-ahead first, on its ask, so the conversation the answer resumes is handed it as the
    // driver takes the park up (KNOWUSE1a's resumed prompt); then the park, with the person's words through the box's own
    // judgement (MSG1d), or, with none, the park's blank answer. A refused go-ahead answers nothing else, in the service's
    // sentence. `{ message, sent, reaches, why }`: the go-ahead's sentence, then what became of the park, as `SESSION_INPUT`
    // says it.
    [DriverRoute("SESSION_GO_AHEAD")]
    private async Task<object?> SessionGoAheadAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var ask = PayloadHelper.GetRequiredValue<string>(request.Payload, "ask");
        var approved = PayloadHelper.GetRequiredValue<bool>(request.Payload, "approved");
        var number = Number(request, "number") is { } named and > 0 and <= int.MaxValue
            ? (int)named
            : throw new DriverException("a go-ahead is named by its number on its ask, a whole number from 1; nothing was answered.");
        var words = Optional(request, "words")?.Trim() is { Length: > 0 } said ? said : null;
        var service = _loop.Service ?? throw NotReady();

        var (answered, message) = await service.AnswerGoAheadAsync(ask, number, approved, words, cancellationToken)
            .ConfigureAwait(false);
        if (!answered) throw new DriverException(message);

        var park = words is null
            ? await _loop.Words.AnswerParkAsync(id, cancellationToken).ConfigureAwait(false)
            : await _loop.Words.SayAsync(id, words, [], preface: null, door: "screen", cancellationToken).ConfigureAwait(false)
              ?? throw NotReady();
        // Words a running session took are kept on its ask, as the box's are (DRIFT1a2): the park went on before the press.
        if (park.Running && words is not null) _loop.Words.KeepOnAsk(id, words);
        return new { Message = message, park.Sent, park.Reaches, park.Why };
    }

    // Finishing a conversation rather than cutting it off: the harness gets end-of-input, says
    // what it was going to say, and exits on its own. `STOP_SESSION` is the other verb. Refused
    // for a session that takes no input, for the same reason as a message.
    [DriverRoute("END_CHAT")]
    private object? EndChat(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        if (_loop.Processes.RefusesInput(id) is { } why) throw new DriverException(why);
        // Through the conversation, which knows its door: on the protocol door, the turns asked
        // for, then `session/close`, then the end of input (CONV3b).
        return new { Ended = _loop.Chat?.Finish(id) ?? _loop.Processes.CloseInput(id) };
    }

    // Stopping the turn and keeping the conversation (CONV4a) — the third verb, beside finishing
    // and stopping the session. What was waiting comes back, so the page can hand it to the
    // person rather than lose it. Refused for a session that takes no input, as a message is,
    // and for a door that carries only text, in the driver's words: it has no turn to stop.
    [DriverRoute("CANCEL_TURN")]
    private async Task<object?> CancelTurnAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // A driven session's stop sends what is held NOW (SESS3): the turn stops, nothing is
        // withdrawn, and the person's words are its next prompt. With nothing held it stops nothing.
        if (_loop.Processes.InboxOf(id) is { } inbox)
        {
            var now = await inbox.SendNowAsync().ConfigureAwait(false);
            return new { now.Cancelled, Withdrawn = Array.Empty<object>() };
        }

        if (_loop.Processes.RefusesInput(id) is { } why) throw new DriverException(why);
        var stop = _loop.Chat is { } chat ? await chat.CancelTurnAsync(id).ConfigureAwait(false) : TurnStop.Nothing;
        return new { stop.Cancelled, Withdrawn = stop.Withdrawn.Select(Said).ToArray() };
    }

    // A conversation's model and effort, as its agent offers them on the protocol door (AGT6b, D98):
    // the page asks once, and takes every change after that as `SESSION_OPTIONS_CHANGED`. Nothing here
    // holding it, or a door that carries none, is an empty list — never a refusal, since a composer
    // asks of every conversation it shows.
    [DriverRoute("SESSION_OPTIONS")]
    private object? SessionOptions(IpcRequest request)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return OptionsAnswer(id, _loop.Chat?.Options(id) ?? []);
    }

    // The person's change to one of them (AGT6b): `session/set_config_option` on the conversation's
    // session. The driver refuses the mode, an option never offered and a conversation it does not
    // hold, in its own words; the agent refuses a value it does not take, in its.
    [DriverRoute("SET_SESSION_OPTION")]
    private async Task<object?> SetSessionOptionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var option = PayloadHelper.GetRequiredValue<string>(request.Payload, "option");
        var value = PayloadHelper.GetRequiredValue<string>(request.Payload, "value");
        var chat = _loop.Chat ?? throw NotReady();
        var after = await chat.SetOptionAsync(id, option, value, cancellationToken).ConfigureAwait(false);
        return OptionsAnswer(id, after);
    }

    // Where a conversation's turns stand (CONV4a): whether one is in flight, and what is waiting.
    // A page that just opened it asks once, and takes every change after that as `SESSION_QUEUED`.
    [DriverRoute("SESSION_QUEUE")]
    private async Task<object?> SessionQueueAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // `Listening` says a running driven session's inbox is open (SESS3), on the protocol door or, where its run can go
        // on in its own conversation, the native door (MSG1b). Whether the page offers a box is `reaches` and `why`'s: what
        // a word said now would do (MSG1d, D137 §5.3), and why nothing takes one, which is when it draws the line instead.
        var inbox = _loop.Processes.InboxOf(id);
        var queue = inbox?.State ?? _loop.Chat?.Queue(id) ?? ChatQueue.Idle;
        var reach = await _loop.Words.ReachAsync(id, cancellationToken).ConfigureAwait(false);
        return new
        {
            Session = id, Queued = queue.Queued.Select(Said).ToArray(), queue.Taking, queue.Opening,
            Listening = inbox is not null,
            // When its last turn ended here (RAIL2), the page's "moved" for a live chat.
            LastTurn = queue.LastTurnEnded,
            reach.Reaches,
            reach.Why,
        };
    }

    /// <summary>
    /// A conversation's options as the page reads them (AGT6b), the answer and the live event alike: each
    /// option's id, the agent's name for it, its category, its value now, and the values it takes — the
    /// agent's words, carried as they were given.
    /// </summary>
    public static object OptionsAnswer(string session, IReadOnlyList<AcpConfigOption> options) => new
    {
        Session = session,
        Options = options.Select(option => new
        {
            option.Id,
            option.Name,
            option.Category,
            option.Current,
            Choices = option.Choices.Select(choice => new { choice.Value, choice.Name, choice.Description }).ToArray(),
        }).ToArray(),
    };

    /// <summary>
    /// A message's attached files as the page sent them (CONV4c): each a name and its bytes as base64,
    /// the shape a quest's uploads take. Bytes that are not base64 are refused in a sentence — kept,
    /// they would be a file nobody sent.
    /// </summary>
    public static IReadOnlyList<ChatUpload> FilesOf(JsonElement payload)
    {
        if (!payload.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array) return [];

        var uploads = new List<ChatUpload>();
        foreach (var file in files.EnumerateArray())
        {
            var name = file.ValueKind == JsonValueKind.Object && file.TryGetProperty("name", out var n)
                && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            var content = file.ValueKind == JsonValueKind.Object && file.TryGetProperty("content", out var c)
                && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            try
            {
                uploads.Add(new ChatUpload(name, Convert.FromBase64String(content ?? throw new FormatException())));
            }
            catch (FormatException)
            {
                throw new DriverException($"`{name}` did not arrive as a file's bytes, so it was not attached. Attach it again.");
            }
        }

        return uploads;
    }

    /// <summary>A message as the page is told it: the words, and the names of its files — never where they are kept.</summary>
    private static object Said(ChatMessage message) =>
        new { message.Text, Files = message.Files.Select(file => file.Name).ToArray() };
}
