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
public sealed class DriverModule : ModuleBase
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

    /// <summary>
    /// Route, and let the driver's own refusals reach the person.
    /// </summary>
    /// <remarks>
    /// <b>An unhandled exception becomes a generic `UNKNOWN_ERROR` carrying only its TYPE</b>, so every
    /// sentence `DriverException` was written to deliver — "unknown adapter 'x' — one of: …", "that
    /// harness declares no installer" — used to be dropped on the floor and shown as a bare failure.
    /// Mapped once, here, rather than at each throw site: the driver library is where those sentences
    /// live, and a per-site wrapping is a list somebody eventually forgets to append to.
    /// </remarks>
    protected override async Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await RouteAsync(request, cancellationToken);
        }
        catch (DriverException error)
        {
            throw Refusals.Because(Refusals.DriverRefused, error.Message, ("message", error.Message));
        }
    }

    private async Task<object?> RouteAsync(IpcRequest request, CancellationToken cancellationToken)
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
                    throw Refusals.Because(
                        Refusals.DriverNotReady,
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
                    // The per-session picker (D49 §4). Absent takes the workspace's default, then the
                    // machine's, then the harness's own configuration home.
                    profile: Optional(request, "profile"),
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

            // This machine's harnesses, and the accounts they run as (D49 §4). Detection is free and
            // read-only — it asks each tool its own version and each profile's own login state — so
            // the page may ask whenever it likes; `refresh` is the person pressing "look again".
            case "HARNESSES":
            {
                var refresh = request.Payload is { } payload
                    && payload.TryGetProperty("refresh", out var again)
                    && again.ValueKind == JsonValueKind.True;

                var config = DriverConfig.Load(_loop.ConfigPath);
                var roster = await _loop.Harnesses.RosterAsync(config, refresh, cancellationToken);

                return new
                {
                    _loop.Harnesses.SettingsPath,
                    Adapter = config.Adapter,
                    Harnesses = roster.Select(report => new
                    {
                        Harness = report.Adapter,
                        report.Present,
                        report.Version,
                        report.Problem,
                        report.MachineDefault,
                        // The profile HOME is a machine path, and this bridge is the one surface
                        // allowed to carry one (D47 §4) — the page renders it so a person can find
                        // the directory they were told Daoris owns.
                        Profiles = report.Profiles.Select(profile => new
                        {
                            profile.Name,
                            profile.Home,
                            Login = profile.Login.ToString().ToLowerInvariant(),
                        }).ToArray(),
                    }).ToArray(),
                };
            }

            // The person's explicit action on a harness (D49 §4): its own installer, its own updater,
            // its own login flow. Never automatic, never mid-session, never unasked — and streamed
            // line by line through the console, because it is a process like any other.
            case "HARNESS_ACTION":
            {
                var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
                var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
                var config = DriverConfig.Load(_loop.ConfigPath);
                // A DriverException, so it travels the same way `Toolchain` already refuses an adapter
                // name it does not know: one mapping, one shape, and the driver's own sentence intact.
                var toolchain = _loop.Harnesses.Toolchain(harness)
                    ?? throw new DriverException(
                        $"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");

                var command = config.Commands.GetValueOrDefault(harness);
                var stream = Relay(harness, action);

                var code = action switch
                {
                    "install" => await HarnessActions.InstallAsync(toolchain, stream, cancellationToken),
                    "update" => await HarnessActions.UpdateAsync(toolchain, command, stream, cancellationToken),
                    "login" => await HarnessActions.LoginAsync(
                        toolchain, command,
                        HarnessSettings.ProfileHome(
                            _loop.Harnesses.Home, harness,
                            Optional(request, "profile")
                            ?? _loop.Harnesses.Settings.Resolve(harness, null, null)
                            ?? "default"),
                        stream, cancellationToken),
                    _ => throw Refusals.Because(
                        Refusals.HarnessActionUnknown,
                        $"unknown harness action '{action}' — one of: install, update, login",
                        ("action", action)),
                };

                // Whatever it did, what this machine HAS has probably changed — so the next question
                // asks the tool again rather than answering from before.
                await _loop.Harnesses.RosterAsync(config, refresh: true, cancellationToken);
                return new { Harness = harness, Action = action, ExitCode = code };
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

    /// <summary>An optional string on a request — absent and blank are the same answer: unstated.</summary>
    private static string? Optional(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>
    /// A harness action's output, relayed as it happens — the same console the session drawer already
    /// renders (D49 §2), because an install that printed nothing until it finished is indistinguishable
    /// from one that hung.
    /// </summary>
    /// <remarks>
    /// <para>Keyed by `harness:action` rather than a session id: it is not a session, has no record,
    /// and must never look like one. The page subscribes to it exactly as it subscribes to a
    /// session's.</para>
    ///
    /// <para>The sequence still counts, because the page's merge rule is "drop anything already
    /// seen" — every line arriving as number 0 would render as one line repeatedly overwritten. There
    /// is no backlog to catch up on here (nothing buffers a harness action), so the counter starts at
    /// 1 and only has to be monotonic.</para>
    /// </remarks>
    private Action<string> Relay(string harness, string action)
    {
        var sequence = 0L;
        return line => _events.EmitAsync("DAORIS", "SESSION_OUTPUT", new
        {
            Session = $"{harness}:{action}",
            Lines = new[] { new { Sequence = Interlocked.Increment(ref sequence), Text = line } },
        });
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
