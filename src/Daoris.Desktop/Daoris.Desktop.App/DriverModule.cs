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
internal sealed class DriverModule(IEventBus events, DriverLoop loop) : ModuleBase(events: events)
{
    public override string ModuleName => "DAORIS.DRIVER";

    protected override Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "STATE":
                return Task.FromResult<object?>(State());

            case "SET_DRIVABLE":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var drivable = PayloadHelper.GetRequiredValue<bool>(request.Payload, "drivable");
                Change(config => config.WithDrivable(repository, drivable));
                return Task.FromResult<object?>(State());
            }

            case "SET_HOLD":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var held = PayloadHelper.GetRequiredValue<bool>(request.Payload, "held");
                Change(config => config.WithHold(repository, held));
                return Task.FromResult<object?>(State());
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
                var tail = loop.Output.Tail(id, after);
                return Task.FromResult<object?>(new
                {
                    Session = id,
                    Lines = tail.Lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
                    tail.Sequence,
                    tail.Live,
                    tail.Dropped,
                });
            }

            case "STOP_SESSION":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // False is an answer, not an error: the session already finished, and its record says how.
                var stopped = loop.Processes.Stop(id);
                loop.Nudge();
                return Task.FromResult<object?>(new { Stopped = stopped });
            }

            // "Look now": a person who just published a quest should not watch a poll countdown.
            case "NUDGE":
                loop.Nudge();
                return Task.FromResult<object?>(null);

            default:
                throw UnknownType(request);
        }
    }

    private object State()
    {
        var config = DriverConfig.Load(loop.ConfigPath);
        return new
        {
            loop.ConfigPath,
            config.Drivable,
            config.Holds,
            config.Cap,
            config.Adapter,
            config.PollSeconds,
            Running = loop.Processes.Running,
        };
    }

    private void Change(Func<DriverConfig, DriverConfig> change)
    {
        change(DriverConfig.Load(loop.ConfigPath)).Save(loop.ConfigPath);
        loop.Nudge();
    }
}
