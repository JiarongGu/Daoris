using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The session-control surface's host half (D46 §6): the page asks, this answers — and every mutation
/// is an edit to `driver.json` plus a nudge, because the file is the person's standing choices and the
/// loop already re-reads it every tick. Stopping a session is the one control that touches a process,
/// through the shared registry, and the driver records the end as the person's.
/// </summary>
internal sealed class DriverModule : ModuleBase
{
    private readonly IEventBus _events;
    private readonly DriverLoop _loop;

    /// <remarks>
    /// The bus is held as well as handed to the base: this module both ANSWERS requests and, since
    /// D49 §3, raises one of its own — a conversation ending is news the page wants without asking.
    /// </remarks>
    public DriverModule(IEventBus events, DriverLoop loop) : base(events: events)
    {
        _events = events;
        _loop = loop;
    }

    public override string ModuleName => "DAORIS.DRIVER";

    protected override async Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "STATE":
                return State();

            case "SET_DRIVABLE":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var drivable = PayloadHelper.GetRequiredValue<bool>(request.Payload, "drivable");
                Change(config => config.WithDrivable(repository, drivable));
                return State();
            }

            case "SET_HOLD":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var held = PayloadHelper.GetRequiredValue<bool>(request.Payload, "held");
                Change(config => config.WithHold(repository, held));
                return State();
            }

            // The console's backlog (D49 §2): what this session has said, or what it has said since
            // the page last heard. Live lines arrive as `SESSION_OUTPUT` events; this is how a page
            // that just opened catches up, and how one that missed a batch closes the gap — the
            // sequence numbers are the driver's, so neither side has to remember the other.
            case "TAIL_SESSION":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // Absent means "everything you have": a page opening a drawer has seen nothing, and
                // making it say so explicitly would be ceremony with a wrong default available.
                var after = request.Payload is { } payload
                    && payload.TryGetProperty("after", out var seen)
                    && seen.ValueKind == JsonValueKind.Number
                        ? seen.GetInt64()
                        : 0;
                var tail = _loop.Output.Tail(id, after);
                return new
                {
                    Session = id,
                    Lines = tail.Lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
                    tail.Sequence,
                    tail.Live,
                    tail.Dropped,
                };
            }

            // A conversation in a repository (D49 §3). The record is the service's and the lock is the
            // ledger's; what only this side can do is put a harness behind it — a process on this
            // machine, which never leaves it (D46 §7).
            case "START_CHAT":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                if (_loop.Chat is not { } chat)
                {
                    throw new InvalidOperationException(
                        "the driver is still coming up — its service is not answering yet. A moment.");
                }

                var config = DriverConfig.Load(_loop.ConfigPath);
                var adapter = request.Payload is { } payload
                    && payload.TryGetProperty("adapter", out var named)
                    && named.ValueKind == JsonValueKind.String
                        ? named.GetString()!
                        : config.Adapter;

                var start = await chat.StartAsync(
                    repository, adapter, config,
                    // The end of a conversation is news the page wants without asking: the drawer is
                    // probably open, and a record that moved silently reads as one that hung.
                    onEnded: (session, state) =>
                        _events.EmitAsync("DAORIS", "SESSION_ENDED", new { Session = session, State = state }),
                    cancellationToken);

                _loop.Nudge();
                return new { start.SessionId, start.Message };
            }

            // The person's half of the turn-taking. False is an answer — the session ended while they
            // were typing — and never an error.
            case "SESSION_INPUT":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var text = PayloadHelper.GetRequiredValue<string>(request.Payload, "text");
                return new { Sent = _loop.Chat?.Say(id, text) ?? false };
            }

            // Finishing a conversation rather than cutting it off: the harness gets end-of-input, says
            // what it was going to say, and exits on its own. `STOP_SESSION` is the other verb.
            case "END_CHAT":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                return new { Ended = _loop.Processes.CloseInput(id) };
            }

            case "STOP_SESSION":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // False is an answer, not an error: the session already finished, and its record says how.
                var stopped = _loop.Processes.Stop(id);
                _loop.Nudge();
                return new { Stopped = stopped };
            }

            // "Look now": a person who just published a quest should not watch a poll countdown.
            case "NUDGE":
                _loop.Nudge();
                // The one verb with nothing to answer — and `await` keeps this method honestly async.
                await Task.CompletedTask;
                return null;

            default:
                throw UnknownType(request);
        }
    }

    private object State()
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        return new
        {
            _loop.ConfigPath,
            config.Drivable,
            config.Holds,
            config.Cap,
            config.Adapter,
            config.PollSeconds,
            Running = _loop.Processes.Running,
        };
    }

    private void Change(Func<DriverConfig, DriverConfig> change)
    {
        change(DriverConfig.Load(_loop.ConfigPath)).Save(_loop.ConfigPath);
        _loop.Nudge();
    }
}
