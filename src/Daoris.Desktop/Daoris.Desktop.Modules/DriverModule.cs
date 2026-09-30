using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The session-control surface's host half (D46 §6): the page asks, this answers. A standing choice is
/// an edit to its file under the home (`driver.json`, `harnesses.json`, `permissions.json`,
/// `plugins.json`) plus a nudge, because the file is the truth and the loop re-reads it every tick —
/// the same files the terminal's verbs edit (D50). The controls that touch a process or a tree (stop,
/// a parked session's answer, a conversation, a merge or a discard) go through the shared registry and
/// the service's ledger, and the record says whose act it was. (REV3 corrected "every mutation is an
/// edit to `driver.json`", written when that was true.)
/// </summary>
public sealed class DriverModule : ModuleBase
{
    private readonly IEventBus _events;
    private readonly DriverLoop _loop;

    // The harness action running now, by `harness:action` — one at a time by construction, and the
    // two things a screen may do to it while it runs: answer the prompt it printed, or stop it.
    private readonly ConcurrentDictionary<string, HarnessRun> _actions = new();

    // Which `harness:action` holds the one slot, from its start until `RunActionAsync` ends it (REV3).
    private string? _acting;

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
    /// The folder holding the install's offers — Daoris's own example plugins, none installed until a press
    /// (PLUG9 d, D102). Null finds them beside the running application, else beside the home; a test names
    /// its own, since every test's home shares one parent.
    /// </summary>
    public string? Offers { get; init; }

    private string OffersFolder => Offers ?? PluginOffers.FolderFor(_loop.Home, AppContext.BaseDirectory);

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

            // Session trees (D51): the desktop's half of the standing opt-in, over the same file
            // `daoris driver trees <repo> on|off` edits — two editors, one truth (D50).
            case "SET_TREES":
            {
                var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
                var ownTree = PayloadHelper.GetRequiredValue<bool>(request.Payload, "ownTree");
                Change(config => config.WithTrees(repository, ownTree));
                return State();
            }

            // A repository's line, or a workspace's (WSR2), or either cleared with no branch — the same
            // file `daoris driver line` edits: one truth, two doors (D50).
            case "SET_LINE":
            {
                var branch = Optional(request, "branch");
                var repository = Optional(request, "repository");
                var workspace = Optional(request, "workspace");
                if ((repository is null) == (workspace is null))
                {
                    throw new DriverException("a line is set for a `repository` or a `workspace` — name one of them.");
                }

                Change(config => workspace is not null
                    ? config.WithWorkspaceLine(workspace, branch)
                    : config.WithLine(repository!, branch));
                return State();
            }

            // Every repository's line here and what said so (WSR2), for the screen. The checkouts are
            // the registry's, so this waits for the driver like the review does.
            case "LINES":
            {
                var service = _loop.Service ?? throw NotReady();
                var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
                var config = DriverConfig.Load(_loop.ConfigPath);
                var lines = await CanonicalLine.OfAsync(
                    config,
                    snapshot.Repositories
                        .OrderBy(known => known.Repository, StringComparer.Ordinal)
                        .Select(known => (known.Repository, (string?)known.Workspace, known.Root)),
                    cancellationToken).ConfigureAwait(false);
                return new
                {
                    Lines = lines.Select(line => new { line.Repository, line.Workspace, line.Branch, line.Source }).ToArray(),
                    // How each one's work lands (WSR1), from the same file and the same circles.
                    Landings = lines.Select(line => (line, landing: LandingRules.Choose(config, line.Repository, line.Workspace)))
                        .Select(pair => new
                        {
                            pair.line.Repository,
                            pair.line.Workspace,
                            pair.landing.Rule.Form,
                            pair.landing.Rule.Pattern,
                            pair.landing.Rule.Tidy,
                            pair.landing.Rule.Plugin,
                            pair.landing.Source,
                        })
                        .ToArray(),
                };
            }

            // Whether this machine interrupts the person at all (SURF5b). The same file
            // `daoris driver notify on|off` edits — one truth, two doors (D50).
            case "SET_NOTIFY":
            {
                var notify = PayloadHelper.GetRequiredValue<bool>(request.Payload, "notify");
                Change(config => config.WithNotify(notify));
                return State();
            }

            // How many failed sessions park a quest (DRV6/D58), and the person's way back in. Both
            // edit what `daoris driver strikes|retry` edits — one truth, two doors (D50).
            case "SET_STRIKES":
            {
                var strikes = PayloadHelper.GetRequiredValue<int>(request.Payload, "strikes");
                Change(config => config.WithStrikes(strikes));
                return State();
            }

            // Which harness answers asks with an intake session (INT4b), or null for none — the same
            // file `daoris driver intake <adapter>|off` edits: one truth, two doors (D50).
            case "SET_INTAKE":
            {
                var adapter = Optional(request, "adapter");
                Change(config => config.WithIntake(adapter));
                return State();
            }

            // The agent Ask Daoris runs on (HELP1, D89), or null for none — the same file
            // `daoris driver helper <adapter>|off` edits: one truth, two doors (D50).
            case "SET_HELPER":
            {
                var adapter = Optional(request, "adapter");
                Change(config => config.WithHelper(adapter));
                return State();
            }

            // The person's grant of a folder the driver is holding for the harness's trust (D73) — the
            // screen's half of `daoris agent trust`. Only a pair this machine's last tick held is
            // granted, in the file that tick read; the terminal is the door that names any folder.
            case "TRUST_FOLDER":
            {
                var folder = PayloadHelper.GetRequiredValue<string>(request.Payload, "folder");
                var trustFile = PayloadHelper.GetRequiredValue<string>(request.Payload, "trustFile");
                var hold = _loop.Trust.Holding(folder, trustFile)
                    ?? throw new DriverException(
                        $"the driver is not holding `{folder}` for this agent's trust, so there is nothing "
                        + "here to confirm — only a folder it is holding can be trusted from this screen. "
                        + "`daoris agent trust claude-code <folder> --yes` names any folder.");

                var grant = ClaudeTrust.Grant(hold.TrustFile, hold.Folder);
                _loop.Nudge();
                return new
                {
                    hold.Folder,
                    grant.Key,
                    grant.Changed,
                    grant.Verified,
                    Message = grant.Verified
                        ? $"Trusted `{hold.Folder}` for this agent: its own `permissions.allow` applies there "
                          + "now, and the driver looks again."
                        : $"Wrote the grant for `{hold.Folder}`, but reading `{hold.TrustFile}` back does not "
                          + "show it — a running Claude Code may have saved over it. If so, the driver will "
                          + "hold the same folder again.",
                };
            }

            case "RETRY_QUEST":
            {
                var quest = PayloadHelper.GetRequiredValue<string>(request.Payload, "quest");
                // Marked at the limit rather than erased, so the records still read true and the next
                // `strikes` failures park it again.
                Change(config => config.WithForgiven(quest, config.Strikes));
                return State();
            }

            // The console's backlog (D49 §2): what this session has said, or what it has said since
            // the page last heard. Live lines arrive as `SESSION_OUTPUT` events; this is how a page
            // that just opened catches up, and how one that missed a batch closes the gap — the
            // sequence numbers are the driver's, so neither side has to remember the other.
            // A session's conversation (D76 §2): the newest page, an earlier one (`before`), or only
            // what is newer (`after`) — how a page opens a session after a restart, loads earlier
            // turns, and closes a gap in the live events. From the record under the home, so it
            // answers whether or not this app was running when the session spoke.
            case "SESSION_HISTORY":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var page = Number(request, "after") is { } after
                    ? _loop.Events.After(id, after)
                    : _loop.Events.Page(id, Number(request, "before"), (int)(Number(request, "limit") ?? SessionEvents.PageLimit));
                return new { Session = id, Events = page.Events.ToArray(), page.Earlier, page.Latest, page.Opening, page.FirstFailure };
            }

            case "TAIL_SESSION":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // Absent means "everything you have": a page opening a drawer has seen nothing, and
                // making it say so explicitly would be ceremony with a wrong default available.
                var tail = _loop.Output.Tail(id, Number(request, "after") ?? 0);
                return new
                {
                    Session = id,
                    Lines = tail.Lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
                    tail.Sequence,
                    tail.Live,
                    tail.Dropped,
                };
            }

            // What a session runs beside itself (CONSOLE2c): each subagent and background task, with
            // the key its console is tailed by over `TAIL_SESSION`. Asked on open and again when a
            // `SESSION_STREAMS` event names the session.
            case "SESSION_STREAMS":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                return new
                {
                    Session = id,
                    Streams = _loop.Output.Streams(id)
                        .Select(stream => new { stream.Key, stream.Kind, stream.Name, stream.Live, stream.State, stream.CanStop })
                        .ToArray(),
                };
            }

            // Stopping one task a session runs, from its tab (CONSOLE3a): the harness's own stop, sent by
            // the session that runs it. Its stream ends when the wire says how. False is an answer — no
            // session here runs it, or its harness stopped nothing — and a key that is not one of this
            // session's tasks is refused in the driver's words, because the page never sends one.
            case "STOP_TASK":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var key = PayloadHelper.GetRequiredValue<string>(request.Payload, "key");
                var prefix = SessionOutput.Key(id, $"{SessionStreamKind.Task}/");
                if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || key.Length == prefix.Length)
                {
                    throw new DriverException($"`{key}` is not background work of session {id}: only a task can be stopped from its tab.");
                }

                var stopped = await _loop.Processes.StopTaskAsync(id, key[prefix.Length..], CancellationToken.None)
                    .ConfigureAwait(false);
                return new { Stopped = stopped ?? false };
            }

            // A conversation in a repository (D49 §3). The record is the service's and the lock is the
            // ledger's; what only this side can do is put a harness behind it — a process on this
            // machine, which never leaves it (D46 §7).
            case "START_CHAT":
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
                    onEnded: (session, state) =>
                        _events.EmitAsync("DAORIS", "SESSION_ENDED", new { Session = session, State = state }),
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

            // Ask Daoris's conversation (HELP1a, D89): the one this machine is running, carried on — one
            // per machine — or a new one in its room, written from the machine as the driver holds it now.
            case "START_HELP":
            {
                // Asked first: no service answer changes it, and it says what to do.
                var config = DriverConfig.Load(_loop.ConfigPath);
                if (config.HelperAdapter is not { Length: > 0 } helper)
                {
                    throw new DriverException(
                        "Ask Daoris has no agent to run on — name one under Settings → Daoris's own AI, or "
                        + "`daoris driver helper <agent>`. Its starters need none.");
                }

                var chat = _loop.Chat ?? throw NotReady();
                var service = _loop.Service ?? throw NotReady();
                var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
                var held = _loop.Processes.Running;
                if (snapshot.Active.FirstOrDefault(session =>
                        session.Repository == HelpRoom.Repository && held.Contains(session.Id, StringComparer.OrdinalIgnoreCase)) is { } running)
                {
                    return new { SessionId = running.Id, Message = $"Ask Daoris `{running.Id}` is carried on.", Running = true };
                }

                var lines = await CanonicalLine.OfAsync(
                    config,
                    snapshot.Repositories
                        .OrderBy(known => known.Repository, StringComparer.Ordinal)
                        .Select(known => (known.Repository, (string?)known.Workspace, known.Root)),
                    cancellationToken).ConfigureAwait(false);
                var roster = await _loop.Harnesses.RosterAsync(config, ct: cancellationToken).ConfigureAwait(false);
                var standing = await service.AsksAsync(cancellationToken).ConfigureAwait(false);
                var asks = standing.Count(ask => ask.State == "Proposed");
                // The asks by id too (HELP6), so a delete of one made by mistake can name it.
                var machine = HelpRoom.Describe(
                    config, snapshot, lines, roster, adapter => _loop.Harnesses.Toolchain(adapter)?.Product, asks, standing,
                    PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names),
                    // The install's own plugins (PLUG9 d), which the helper may propose installing by id.
                    PluginOffers.Load(OffersFolder, _loop.Home, AdapterSet.Built().Names));

                var start = await chat.StartHelpAsync(
                    helper, config, machine,
                    onEnded: (session, state) =>
                        _events.EmitAsync("DAORIS", "SESSION_ENDED", new { Session = session, State = state }),
                    ct: cancellationToken).ConfigureAwait(false);

                _loop.Nudge();
                return new { start.SessionId, start.Message, Running = false };
            }

            // Ask Daoris's proposals (HELP1c, D89): what one conversation proposed that waits for the
            // person, each judged with the route's own code first. One the route would refuse is never
            // shown: it is settled refused, and the agent hears why in the route's words.
            case "HELP_PROPOSALS":
            {
                var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
                var service = _loop.Service ?? throw NotReady();
                var pending = HelpProposals.Pending(_loop.Home, session);
                var (config, facts) = await HelpFactsAsync(service, pending, cancellationToken).ConfigureAwait(false);
                var shown = new List<object>();
                foreach (var proposal in pending)
                {
                    var plan = HelpProposals.Plan(proposal, config, facts);
                    if (plan.Refusal is { } refused)
                    {
                        HelpProposals.Settle(_loop.Home, proposal.Id, "refused", refused);
                        _loop.Chat?.Say(session, InPersonsWords($"Daoris did not show proposal `#{proposal.Id}` to the person — the route refuses it: {refused}"));
                        continue;
                    }

                    shown.Add(new
                    {
                        proposal.Id, proposal.Kind, plan.Describe, plan.Terminal, proposal.Why,
                        // What a plugin runs, as its manifest writes it (PLUG9), for the card to show before Apply.
                        Plugin = plan.Plugin is { } plugin
                            ? new
                            {
                                plugin.Id, plugin.Name, plugin.Version, plugin.Command, plugin.Points,
                                Harnesses = plugin.Harnesses.Select(part => new { part.Name, part.Command }).ToArray(),
                                Servers = plugin.Servers.Select(part => new { part.Name, part.Command }).ToArray(),
                                plugin.Copied, plugin.Problem,
                                // An offer's requirement lines (PLUG9 d), and an update's changes (PLUG9 c).
                                plugin.Needs, plugin.Replaced,
                                Changes = ChangesOf(plugin.Changes),
                            }
                            : null,
                    });
                }

                return new { Session = session, Proposals = shown.ToArray() };
            }

            // The person's Apply: made through the door the screen's own route uses (HELP6), judged again
            // first, since the machine may have moved since the card was drawn. The result goes back into
            // the conversation as the person's next message, so the agent knows (D89).
            case "HELP_APPLY":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var service = _loop.Service ?? throw NotReady();
                var proposal = HelpProposals.Find(_loop.Home, id)
                    ?? throw new DriverException($"there is no proposal `#{id}` on this machine.");
                if (proposal.State != "proposed") throw new DriverException($"proposal `#{id}` is already {proposal.State}.");

                var (config, facts) = await HelpFactsAsync(service, [proposal], cancellationToken).ConfigureAwait(false);
                var plan = HelpProposals.Plan(proposal, config, facts);
                // What it did, and an agent action's end once it comes, into the conversation that proposed it.
                void Say(string text)
                {
                    if (proposal.Session is { } session) _loop.Chat?.Say(session, InPersonsWords(text));
                }

                var applied = await HelpProposals.ApplyAsync(
                    _loop.Home, proposal, plan, HelpDoors(service), Say, cancellationToken).ConfigureAwait(false);
                if (proposal.Kind is "ask" or "delete") _loop.Nudge();

                return new
                {
                    Message = applied.Told,
                    applied.Applied,
                    // Where a go takes the person: the page navigates, as its starters' doors do (HELP6).
                    Go = applied.Go is { } place ? new { place.View, place.Domain, place.Part } : null,
                    // The action an update or a pin started, so the Agents screen follows its console and its end.
                    HarnessAction = applied.Applied && proposal.Kind == "agent"
                        ? new { Harness = proposal.Target!.Trim(), Action = proposal.Door }
                        : null,
                };
            }

            // The person's Not now: nothing changes, and the agent is told so.
            case "HELP_DISMISS":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var proposal = HelpProposals.Find(_loop.Home, id)
                    ?? throw new DriverException($"there is no proposal `#{id}` on this machine.");
                if (proposal.State != "proposed") throw new DriverException($"proposal `#{id}` is already {proposal.State}.");

                HelpProposals.Settle(_loop.Home, id, "dismissed", null);
                var told = $"Not now: the person did not apply `#{id}`.";
                if (proposal.Session is { } said) _loop.Chat?.Say(said, InPersonsWords(told));
                await Task.CompletedTask.ConfigureAwait(false);
                return new { Message = told };
            }

            // The person's half of the turn-taking. False is an answer — the session ended while they
            // were typing — and never an error. 🔴 A session that takes no input is REFUSED instead, in
            // the driver's words (INT4h): false would tell a stale page it ended when it is running.
            case "SESSION_INPUT":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var text = PayloadHelper.GetRequiredValue<string>(request.Payload, "text");
                // A driven session on the protocol door hears what the person adds (SESS3): held, and the
                // next prompt of its own session — never a line in its stream, which INT4i still refuses.
                // False once it has stopped taking any: it is ending, and its record will say so.
                if (_loop.Processes.InboxOf(id) is { } inbox) return new { Sent = inbox.Hold(new ChatMessage(text, [])) };
                if (_loop.Processes.RefusesInput(id) is { } why) throw new DriverException(why);
                // What the person attached, kept for this conversation before the message goes (CONV4c).
                var files = request.Payload is { } payload ? FilesOf(payload) : [];
                // Where the person is (HELP1b), which the agent is handed ahead of the words; absent for most.
                return new { Sent = _loop.Chat?.Say(id, text, files, Optional(request, "preface")) ?? false };
            }

            // Finishing a conversation rather than cutting it off: the harness gets end-of-input, says
            // what it was going to say, and exits on its own. `STOP_SESSION` is the other verb. Refused
            // for a session that takes no input, for the same reason as a message.
            case "END_CHAT":
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
            case "CANCEL_TURN":
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
            case "SESSION_OPTIONS":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                return OptionsAnswer(id, _loop.Chat?.Options(id) ?? []);
            }

            // The person's change to one of them (AGT6b): `session/set_config_option` on the conversation's
            // session. The driver refuses the mode, an option never offered and a conversation it does not
            // hold, in its own words; the agent refuses a value it does not take, in its.
            case "SET_SESSION_OPTION":
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
            case "SESSION_QUEUE":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // `Listening` says a driven session hears what the person adds (SESS3), which is when the
                // page offers it a box: never to one on the pipe door, where nothing could hear it.
                var inbox = _loop.Processes.InboxOf(id);
                var queue = inbox?.State ?? _loop.Chat?.Queue(id) ?? ChatQueue.Idle;
                return new
                {
                    Session = id, Queued = queue.Queued.Select(Said).ToArray(), queue.Taking, queue.Opening,
                    Listening = inbox is not null,
                    // When its last turn ended here (RAIL2), the page's "moved" for a live chat.
                    LastTurn = queue.LastTurnEnded,
                };
            }

            case "STOP_SESSION":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                // False is an answer, not an error: the session already finished, and its record says how.
                // 🔴 Unless the record says it still runs and nothing on this machine runs it — a chat
                // left behind by an application that closed, or a crash. Then the person's stop is how
                // it ends: pressing it used to change nothing and say nothing (2026-09-25).
                // Which of the three it was is part of the answer: the page says it, and an orphan's
                // ending is not the person's — its record says nothing ran it.
                var stopped = _loop.Processes.Stop(id);
                var orphan = !stopped
                    && _loop.Service is { } service
                    && (await Orphans.EndAsync(service, _loop.Processes, only: id, ct: cancellationToken)
                        .ConfigureAwait(false)).Count > 0;
                // 🔴 And when another Daoris process on this machine runs it — a terminal's chat — this
                // host can neither stop it nor call it ended: the record still says working (REV3).
                var elsewhere = !stopped && !orphan && _loop.Processes.AliveOnThisMachine(id);
                _loop.Nudge();
                return new { Stopped = stopped || orphan, Orphan = orphan, Elsewhere = elsewhere };
            }

            // The person's answer to a session parked at a checkpoint (D52 §4). It goes through the
            // DRIVER rather than straight to the service because the two halves must move together:
            // this machine lets the process go, and only then does the record say it ended.
            case "RESOLVE_SESSION":
                return await ResolveAsync(request, cancellationToken);

            // What a session actually did (SURF6, design §5). The evidence string says that work
            // happened; this says what it was. Desktop-only by construction, like the console: it is
            // read off a checkout, and only the machine holding one can answer at all.
            case "SESSION_DIFF":
                return await DiffAsync(request, cancellationToken);

            // What a person first said in each of these sessions (RAIL1): a conversation's identity, from
            // this machine's own record — never the session record, which travels (D47 §4).
            case "SESSION_OPENINGS":
            {
                var ids = new List<string>();
                if (request.Payload is { } payload && payload.TryGetProperty("ids", out var named)
                    && named.ValueKind == JsonValueKind.Array)
                {
                    ids.AddRange(named.EnumerateArray()
                        .Where(id => id.ValueKind == JsonValueKind.String)
                        .Select(id => id.GetString()!));
                }

                await Task.CompletedTask;
                return new { Openings = _loop.Events.Openings(ids) };
            }

            // What sessions said, searched (RAIL1): the person's words and the agent's, from this machine's
            // own record, bounded and saying so.
            case "SESSION_SEARCH":
            {
                var query = PayloadHelper.GetRequiredValue<string>(request.Payload, "q");
                await Task.CompletedTask;
                // Within one session, where the page names it (SESS1 S9): its words and its calls' titles.
                var found = Optional(request, "session") is { } one ? _loop.Events.Within(one, query) : _loop.Events.Search(query);
                return new
                {
                    Query = query,
                    Hits = found.Hits.Select(hit => new { hit.Session, hit.Seq, hit.Kind, hit.Snippet }).ToArray(),
                    found.Cut,
                };
            }

            // What a person may `@` in a conversation (CONV4d): the files in the tree the record names.
            // Desktop-only for the diff's reason — it is read off a checkout — and read-only.
            case "SESSION_FILES":
                return await FilesAsync(request, cancellationToken);

            // The two acts on a reviewed session (SURF6b, D51 rules 6–7). Both are the PERSON's —
            // nothing merges itself and nothing deletes itself — so both are their own route rather
            // than anything the diff route could do as a side effect.
            case "MERGE_SESSION_TREE":
            case "DISCARD_SESSION_TREE":
                return await ActOnTreeAsync(request, cancellationToken);

            // How a reviewed session's work lands (WSR1, D87): what a press WOULD do, said before it —
            // merge into the line, or the branch the rule names for this session — and the press, which
            // applies the repository's rule. `MERGE_SESSION_TREE` stays the merge alone.
            case "LANDING":
            case "LAND_SESSION_TREE":
                return await LandAsync(request, cancellationToken);

            // The clean-up (WSR3, D88): every session branch here with what it holds — then, on the
            // person's press, those the proof clears, and only those the page listed to go (`only`).
            case "SWEEP_PLAN":
            case "SWEEP":
                return await SweepAsync(request, cancellationToken);

            // A repository's landing rule or a workspace's, or either cleared with no form — the same file
            // `daoris driver landing` edits: one truth, two doors (D50).
            case "SET_LANDING":
            {
                var repository = Optional(request, "repository");
                var workspace = Optional(request, "workspace");
                if ((repository is null) == (workspace is null))
                {
                    throw new DriverException("a landing rule is set for a `repository` or a `workspace` — name one of them.");
                }

                var rule = Optional(request, "form") is { } form
                    ? new LandingRule(form, Optional(request, "pattern"), Flag(request, "tidy"), Optional(request, "plugin"))
                    : null;
                // The rule's plugin must be able to land work here, said as `daoris driver landing` says it (D100);
                // the shape is the file's own question, asked first by the edit below.
                if (rule is { Plugin: { } plugin } && LandingRules.Problem(rule) is null
                    && LandingRules.PluginProblem(plugin, PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names)) is { } problem)
                {
                    throw new DriverException(problem);
                }

                Change(config => workspace is not null
                    ? config.WithWorkspaceLanding(workspace, rule)
                    : config.WithLanding(repository!, rule));
                return State();
            }

            // This machine's harnesses, and the accounts they run as (D49 §4). Detection is free and
            // read-only — it asks each tool its own version and each profile's own login state — so
            // the page may ask whenever it likes; `refresh` is the person pressing "look again".
            case "HARNESSES":
            {
                var refresh = Flag(request, "refresh");

                var config = DriverConfig.Load(_loop.ConfigPath);
                var roster = await _loop.Harnesses.RosterAsync(config, refresh, cancellationToken);
                var settings = _loop.Harnesses.Settings;

                return new
                {
                    _loop.Harnesses.SettingsPath,
                    Adapter = config.Adapter,
                    Harnesses = roster.Select(report =>
                    {
                        // Asked once per harness: every field below reads the same two answers.
                        var toolchain = _loop.Harnesses.Toolchain(report.Adapter);
                        var pinned = settings.ResolveVersion(report.Adapter, null, null);
                        // The tool's own settings file under an account (AGT6, D98), where this build
                        // knows it: a door's is its owner's (AGT7), and null offers nothing.
                        var settingsFile = _loop.Harnesses.AccountToolchain(report.Adapter)?.SettingsFile;
                        return new
                        {
                            Harness = report.Adapter,
                            report.Present,
                            report.Version,
                            report.Problem,
                            report.MachineDefault,
                            // The managed toolchain (TOOL2/D57). `Pinned` is what the machine asked for
                            // and `Managed` is what is actually there — they differ exactly when a pin
                            // names a version nobody installed, which the surface must say rather than
                            // imply the pin is in force.
                            Pinned = pinned,
                            Managed = HarnessSettings.ManagedBinary(
                                _loop.Harnesses.Home, report.Adapter,
                                pinned,
                                toolchain?.Binary ?? []),
                            // Whether this harness CAN be pinned at all. A harness that declares neither
                            // a package nor a maker's channel (AGT2b) has no version for Daoris to fetch,
                            // and a surface offering the control anyway would be a button whose only
                            // outcome is a refusal.
                            Pinnable = toolchain is { } pinnable
                                && (pinnable.Package is { Length: > 0 } || pinnable.Channel is { Length: > 0 }),
                            // Whether this door can run a sign-in at all — the same rule: a harness that
                            // declares no login flow gets no "Sign in" whose only outcome is a refusal.
                            SignsIn = toolchain?.LoginArguments is { Count: > 0 },
                            // Which Update this door has (USE1a): "pin" moves the pin to the newest
                            // release, "tool" runs the tool's own updater, and null offers none — the
                            // same rule again, after Update on a pinned door answered only a refusal.
                            Updates = toolchain is null ? null : HarnessActions.UpdateOf(toolchain, pinned),
                            // 🔴 Which TOOL's account this entry runs as, and which door it holds a
                            // session over. Both were already declared and neither reached the page,
                            // which is why the surface listed `claude-code` and `claude-code-acp` as two
                            // things a person has to have opinions about. They are one tool and one
                            // account; the second is a way in. The page groups on these two fields.
                            AccountOf = toolchain?.AccountOf,
                            // What a person calls the tool, and whose it is (AGT1) — `dsh` meant nothing
                            // to the owner until it said.
                            toolchain?.Product,
                            toolchain?.Maker,
                            Wire = _loop.Harnesses.Wire(report.Adapter).ToString().ToLowerInvariant(),
                            // Whether a session on this door keeps a conversation (D76 §1) — what the page
                            // reads an empty record by, rather than guessing from the emptiness (CONV3b).
                            Structured = _loop.Harnesses.Structured(report.Adapter),
                            // The plugin this harness came from (D64), or null for one this build carries
                            // — shown beside it, so a person knows which folder to look in.
                            Plugin = _loop.Harnesses.Adapters.DeclaredBy(report.Adapter),
                            // The profile HOME is a machine path, and this bridge is the one surface
                            // allowed to carry one (D47 §4) — the page renders it so a person can find
                            // the directory they were told Daoris owns.
                            Profiles = report.Profiles.Select(profile => new
                            {
                                profile.Name,
                                profile.Home,
                                Login = profile.Login.ToString().ToLowerInvariant(),
                                // Who is signed in there, by the tool's own answer (D66 §3) — the name
                                // a person knows the account by, where the directory's is `account-2`.
                                profile.Account,
                                // An account that is a key, by its handle only (AGT3). Never the key.
                                profile.Key,
                                // The account's own model and effort, read fresh from the tool's file
                                // each time (AGT6): the file is the truth, and the terminal edits it too.
                                Settings = settingsFile is null
                                    ? null
                                    : AccountSettings(AgentSettings.Read(Path.Combine(profile.Home, settingsFile))),
                            }).ToArray(),
                            // What the tool itself offers for those two keys (AGT6, D98): its aliases and
                            // the efforts its settings keep. Null where Daoris does not know its settings,
                            // and the page then offers nothing and says so.
                            SettingsChoices = settingsFile is null
                                ? null
                                : new { AgentSettings.Models, AgentSettings.Efforts },
                            // Whether this agent takes an API key at all — the control is absent where
                            // it does not, by the rule `Pinnable` and `SignsIn` follow.
                            TakesKey = _loop.Harnesses.AccountToolchain(report.Adapter)?.KeyVariable is { Length: > 0 },
                            // 🔴 The account a person actually HAS — the tool's own configuration home —
                            // answered beside the profiles rather than left out, which read as "No
                            // accounts" to an owner who was logged in.
                            OwnLogin = report.OwnLogin.ToString().ToLowerInvariant(),
                            report.OwnAccount,
                            // Which circles run this harness as which account (D49 §4): the terminal
                            // could set it and the page could not even see it. A door's are its
                            // owner's (AGT7).
                            WorkspaceDefaults = settings.Workspaces
                                .Select(circle => (Workspace: circle.Key, Map: circle.Value,
                                    Owner: toolchain?.Owner(report.Adapter) ?? report.Adapter))
                                .Where(circle => circle.Map.TryGetValue(circle.Owner, out var chosen)
                                    && !string.IsNullOrWhiteSpace(chosen))
                                .OrderBy(circle => circle.Workspace, StringComparer.Ordinal)
                                .Select(circle => new { circle.Workspace, Profile = circle.Map[circle.Owner] })
                                .ToArray(),
                        };
                    }).ToArray(),
                };
            }

            // What a driven start in each workspace would run on, and where each part came from
            // (MAP1b): the driver's own `SelectAsync`, read through `WiringAsync`, so the panel cannot
            // show a start the loop would not make. The page names the circles it shows; this answers
            // for those and no others. Names and versions only — no home, no binary, no key. Named
            // for what it answers rather than "wiring", which the page already calls the remotes map.
            case "STARTS":
            {
                var config = DriverConfig.Load(_loop.ConfigPath);
                var named = new List<string>();
                if (request.Payload is { } payload
                    && payload.TryGetProperty("workspaces", out var circles)
                    && circles.ValueKind == JsonValueKind.Array)
                {
                    named.AddRange(circles.EnumerateArray()
                        .Where(circle => circle.ValueKind == JsonValueKind.String)
                        .Select(circle => circle.GetString()!)
                        .Where(circle => circle.Length > 0));
                }

                var starts = new List<object>();
                foreach (var workspace in named.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
                {
                    // The work: a driven session, which is also what a conversation started without a
                    // pick takes.
                    starts.Add(Start("work", workspace,
                        await _loop.Harnesses.WiringAsync(config.Adapter, config, workspace, cancellationToken)));

                    // The intake (INT4b, AGT6), once an agent is named for it: the same `SelectAsync`
                    // the intake takes for an ask in this circle. 🔴 An agent this build has no adapter
                    // for is a held row in the driver's own sentence — the one the intake would hold
                    // with — never a refusal of the whole answer, which would take the work's rows too.
                    if (config.IntakeAdapter is { Length: > 0 } intake)
                    {
                        StartWiring wiring;
                        try
                        {
                            wiring = await _loop.Harnesses.WiringAsync(intake, config, workspace, cancellationToken);
                        }
                        catch (DriverException error)
                        {
                            wiring = new StartWiring(
                                intake, intake, null, ChoiceFrom.Unset, null, ChoiceFrom.Unset, false, error.Message);
                        }

                        starts.Add(Start("intake", workspace, wiring));
                    }
                }

                return new { Adapter = config.Adapter, Starts = starts };

                object Start(string job, string workspace, StartWiring wiring) => new
                {
                    Job = job,
                    Workspace = workspace,
                    wiring.Adapter,
                    wiring.Owner,
                    // Asked defensively: a held row may be held BECAUSE the name resolves to nothing.
                    Known(wiring.Adapter)?.Product,
                    wiring.Profile,
                    ProfileFrom = wiring.ProfileFrom.ToString().ToLowerInvariant(),
                    wiring.Version,
                    VersionFrom = wiring.VersionFrom.ToString().ToLowerInvariant(),
                    wiring.Commanded,
                    wiring.Refusal,
                };

                HarnessToolchain? Known(string adapter)
                {
                    try
                    {
                        return _loop.Harnesses.Toolchain(adapter);
                    }
                    catch (DriverException)
                    {
                        return null;
                    }
                }
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
                var profile = Optional(request, "profile");
                // 🔴 Whose accounts an action on this door touches (AGT7): a door's accounts, defaults
                // and keys are its OWNER's. Its pin and its installer stay its own.
                var owner = toolchain.Owner(harness);

                // 🔴 An account that is an API key (AGT3, D67 §1). The key crosses this bridge once,
                // inward, and is answered only by its handle — here, on the roster, and in any event.
                if (action == "key-add")
                {
                    if (_loop.Harnesses.AccountToolchain(harness)?.KeyVariable is not { Length: > 0 })
                    {
                        throw new DriverException(
                            $"`{harness}` takes no API key from Daoris — sign in with its own login instead.");
                    }

                    var account = HarnessKeys.Add(
                        _loop.Harnesses.Home, owner, PayloadHelper.GetRequiredValue<string>(request.Payload, "key"));
                    await _loop.Harnesses.RosterAsync(config, refresh: true, cancellationToken);
                    return new
                    {
                        Harness = harness, Action = action, ExitCode = 0, Profile = account,
                        Key = HarnessKeys.Handle(HarnessKeys.Of(_loop.Harnesses.Home, owner, account)!),
                    };
                }

                // The file edits answer at once, with the exit code.
                int? edited = action switch
                {
                    "unpin" => Unpin(harness),
                    // 🔴 The credential profiles, from a SCREEN (DEPLOY3). They existed only as
                    // `daoris agent profile add|remove|default`, so the Machine view could list a
                    // profile and log into one and never make one — D50 violated in the direction
                    // nothing tests, since the rule is written "whatever a screen can set, a
                    // terminal can" and the converse had no check.
                    //
                    // A sign-in stays the tool's: adding one MAKES A DIRECTORY and nothing else,
                    // and what lands inside it is the harness's own.
                    "profile-add" => ProfileAdd(owner, request),
                    "profile-remove" => ProfileRemove(owner, request, stream),
                    "profile-default" => ProfileDefault(owner, request),
                    "install" or "update" or "login" or "login-new" or "pin" => null,
                    _ => throw Refusals.Because(
                        Refusals.HarnessActionUnknown,
                        $"unknown agent action '{action}' — one of: install, update, login, login-new, "
                        + "key-add, pin, unpin, profile-add, profile-remove, profile-default",
                        ("action", action)),
                };
                if (edited is { } code)
                {
                    // Whatever it did, what this machine HAS has probably changed — so the next question
                    // asks the tool again rather than answering from before.
                    await _loop.Harnesses.RosterAsync(config, refresh: true, cancellationToken);
                    return new { Harness = harness, Action = action, ExitCode = code };
                }

                // A pin's version is asked for before anything starts: a pin without one is a malformed call.
                var version = action == "pin" ? PayloadHelper.GetRequiredValue<string>(request.Payload, "version") : null;
                await StartProcessActionAsync(harness, action, profile, version, toolchain, command, stream, config);
                return new { Harness = harness, Action = action, Started = true };
            }

            // The two things a screen may do to a harness action while it runs (2026-09-23): answer
            // the prompt it printed — a login asks for the code the browser shows, and waits — and
            // stop it. Either is refused, naming the action, when nothing runs under that name: an
            // answer that went nowhere must not look delivered.
            case "HARNESS_INPUT":
            {
                Running(request).Send(PayloadHelper.GetRequiredValue<string>(request.Payload, "text"));
                return new { Sent = true };
            }

            case "HARNESS_CANCEL":
            {
                Running(request).Cancel();
                return new { Cancelled = true };
            }

            // An account's own model and effort (AGT6, D98): keys in the tool's own settings file under
            // that account, the same file `daoris agent settings` edits (D50). A key sent as null is
            // cleared and one left out is untouched. Never the tool's own configuration home, which the
            // roster says Daoris never touches, and never an account a setting would bring into being.
            case "SET_AGENT_SETTINGS":
            {
                var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
                var (owner, profile, read) = WriteAgentSettings(
                    harness, Optional(request, "profile"), () => (Edit(request, "model"), Edit(request, "effort"), PerModel(request)));
                return new { Harness = owner, Profile = profile, read.Model, read.Effort, PerModel = PerModelOf(read), read.Problem };
            }

            // This machine's plugins (D64): the catalogue as the driver reads it, each with what it
            // declares, what it speaks on, whether it is running, and why it contributes nothing when
            // it does not. Machine paths ride this bridge like every path here.
            case "PLUGINS":
            {
                await Task.CompletedTask;
                var catalog = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names);
                var running = new HashSet<string>(_loop.RunningPlugins, StringComparer.Ordinal);
                return new
                {
                    Folder = Path.Combine(_loop.Home, PluginCatalog.Folder),
                    // Where a new plugin may speak (PLUG8): the kit's points, which are the driver's.
                    Kit = new { Points = PluginKit.Points.Select(point => new { point.Name, point.Kind }).ToArray() },
                    Plugins = catalog.Plugins.Select(plugin => new
                    {
                        plugin.Manifest.Id,
                        plugin.Manifest.Name,
                        plugin.Manifest.Version,
                        plugin.Manifest.Description,
                        plugin.Enabled,
                        plugin.Problem,
                        Harnesses = plugin.Manifest.Harnesses.Select(h => h.Name).ToArray(),
                        Points = plugin.Manifest.Hooks?.Points ?? [],
                        Running = running.Contains(plugin.Manifest.Id),
                        plugin.Folder,
                        plugin.Data,
                        // Where it came from (PLUG9 c): what an Update re-reads, or why there is none.
                        Source = SourceOf(plugin.Folder),
                    }).ToArray(),
                    // Daoris's own plugins the install carries (PLUG9 d), installed only by a press.
                    OffersFolder,
                    Offers = PluginOffers.Load(OffersFolder, _loop.Home, AdapterSet.Built().Names).Select(offer => new
                    {
                        offer.Id,
                        offer.Manifest.Name,
                        offer.Manifest.Version,
                        offer.Manifest.Description,
                        offer.Problem,
                        Harnesses = offer.Manifest.Harnesses.Select(h => h.Name).ToArray(),
                        Points = offer.Manifest.Hooks?.Points ?? [],
                        Servers = offer.Manifest.Servers.Select(s => s.Name).ToArray(),
                        offer.Needs,
                        offer.Installed,
                    }).ToArray(),
                };
            }

            // The screen's half of `daoris plugin update <id>` (PLUG9 c, D50): without `apply`, what an update
            // would change, or why it cannot, for the row to show before the press; with it, the update.
            case "PLUGIN_UPDATE":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                if (request.Payload is { } payload && payload.TryGetProperty("apply", out var apply) && apply.ValueKind == JsonValueKind.True)
                {
                    var updated = await UpdatePluginAsync(id).ConfigureAwait(false);
                    return new { updated.Id, Applied = true, Refusal = (string?)null, Source = updated.Source.Said, updated.From, Changes = ChangesOf(updated.Changes) };
                }

                var (plan, refusal) = PluginInstall.PlanUpdate(_loop.Home, id, AdapterSet.Built().Names, OffersFolder);
                return new
                {
                    Id = plan?.Id ?? id,
                    Applied = false,
                    Refusal = refusal,
                    Source = plan?.Source.Said,
                    plan?.From,
                    Changes = ChangesOf(plan?.Changes ?? []),
                };
            }

            // The screen's Install beside one of the install's offers (PLUG9 d): `daoris plugin add --offer <id>`'s
            // copy, the offer recorded. 🔴 Nothing starts at the press; the loop starts it at its next look.
            case "PLUGIN_INSTALL":
            {
                await Task.CompletedTask;
                var offer = PayloadHelper.GetRequiredValue<string>(request.Payload, "offer");
                var added = PluginInstall.AddOffer(_loop.Home, OffersFolder, offer, AdapterSet.Built().Names);
                _loop.Nudge();
                return new { added.Id, added.Name, added.Version };
            }

            // The screen's half of `daoris plugin enable|disable|remove` (D50): a row in
            // `plugins.json`, or the install folder gone with the data folder named and kept.
            case "PLUGIN_ACTION":
            {
                await Task.CompletedTask;
                var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
                var entry = InstalledPlugin(id);

                switch (action)
                {
                    case "enable" or "disable":
                        SwitchPlugin(entry, action == "enable");
                        break;
                    case "remove":
                    {
                        // The install folder goes; what the plugin kept is NAMED and stays — it is
                        // the person's to throw away, the same judgement Forget makes for an account.
                        // 🔴 Its hook process first, then the folder moved aside WHOLE (REV3): on
                        // Windows a running hook holds its folder, and a recursive delete took every
                        // file it could before failing, stranding a plugin with no manifest.
                        await _loop.StopPluginAsync(entry.Manifest.Id).ConfigureAwait(false);
                        var aside = Path.Combine(
                            Path.GetDirectoryName(entry.Folder)!, $".removing-{entry.Manifest.Id}-{Guid.NewGuid():N}");
                        try
                        {
                            Directory.Move(entry.Folder, aside);
                        }
                        catch (Exception held) when (held is IOException or UnauthorizedAccessException)
                        {
                            throw Refusals.Because(
                                Refusals.PluginBusy,
                                $"`{entry.Manifest.Id}` was not removed: something on this machine still has a file in "
                                + "its folder open. Close it, or wait for the plugin's own process to end, and remove it "
                                + "again. Nothing was taken.",
                                ("id", entry.Manifest.Id));
                        }

                        // Aside is a dot-folder, which the catalogue never reads, so a delete that
                        // cannot finish leaves nothing that looks like a plugin.
                        try { Directory.Delete(aside, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                        PluginState.Enable(_loop.Home, entry.Manifest.Id);
                        break;
                    }
                    default:
                        throw Refusals.Because(
                            Refusals.PluginActionUnknown,
                            $"unknown plugin action '{action}' — one of: enable, disable, remove",
                            ("action", action));
                }

                // The loop reconciles its hook processes against the catalogue each tick; asked to
                // look now, so a plugin switched off stops before the person has finished reading.
                _loop.Nudge();
                return new
                {
                    Id = entry.Manifest.Id,
                    Action = action,
                    Data = Directory.Exists(entry.Data) ? entry.Data : null,
                };
            }

            // The plugin kit's screen half (PLUG8, D101): `daoris-driver plugins new` from a form. It writes
            // a plugin's folder into the one the person named, a plugins repository's typically, and
            // installs nothing: making a plugin is work, reviewed before `daoris plugin add`.
            case "PLUGIN_NEW":
            {
                await Task.CompletedTask;
                var points = request.Payload is { } payload && payload.TryGetProperty("points", out var named)
                    && named.ValueKind == JsonValueKind.Array
                        ? named.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
                        : [];
                var plan = PluginKit.Plan(
                    PayloadHelper.GetRequiredValue<string>(request.Payload, "id"), points,
                    PayloadHelper.GetRequiredValue<string>(request.Payload, "folder"));
                var files = PluginKit.Write(plan);
                return new { plan.Id, plan.Folder, plan.Points, Files = files };
            }

            // `daoris-driver plugins try` from a button: an installed plugin by its id, or a folder by
            // its path, started as the driver would and every answer read by the driver's own reader.
            // It may take as long as the driver waits at the points tried — two minutes for a landing.
            case "PLUGIN_TRY":
            {
                var options = new TrialOptions(Point: Optional(request, "point"));
                var trial = Optional(request, "id") is { } id
                    ? await PluginKit.TryInstalledAsync(_loop.Home, id, options, cancellationToken)
                    : Optional(request, "folder") is { } folder
                        ? await PluginKit.TryFolderAsync(folder, options, cancellationToken)
                        : throw new DriverException("a try needs an installed plugin's id or a folder.");
                return new
                {
                    trial.Plugin,
                    trial.Folder,
                    trial.Command,
                    trial.Passed,
                    trial.Summary,
                    Steps = trial.Steps.Select(step => new { step.Name, step.Ok, step.Sentence }).ToArray(),
                    trial.Said,
                };
            }

            // What an agent Daoris starts may do (PERM1, D72): the defaults with their reasons, and
            // every scope the machine's file holds — the file `daoris agent rules` edits. A path
            // under the home rides this bridge like every path here.
            case "RULES":
            {
                await Task.CompletedTask;
                return Rules(PermissionRules.Load(_loop.Home));
            }

            // The screen's half of `daoris agent rules allow|ask|deny|remove|default` (D50): an edit to
            // the same file, answered with the state after it. A refusal is the driver's own sentence.
            case "RULE_ACTION":
            {
                await Task.CompletedTask;
                var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
                var file = PermissionRules.Load(_loop.Home);
                file = action switch
                {
                    "add" => PermissionRules.Add(
                        file, ScopeOf(request), Optional(request, "name"),
                        Optional(request, "list") switch
                        {
                            "allow" => RuleList.Allow,
                            "ask" => RuleList.Ask,
                            "deny" => RuleList.Deny,
                            var other => throw new DriverException($"a rule goes in `allow`, `ask` or `deny`, not `{other}`."),
                        },
                        PayloadHelper.GetRequiredValue<string>(request.Payload, "rule")),
                    "remove" => PermissionRules.Remove(
                        file, ScopeOf(request), Optional(request, "name"),
                        PayloadHelper.GetRequiredValue<string>(request.Payload, "rule")),
                    "default" => PermissionRules.SwitchDefault(
                        file, PayloadHelper.GetRequiredValue<string>(request.Payload, "id"),
                        PayloadHelper.GetRequiredValue<bool>(request.Payload, "on")),
                    _ => throw new DriverException($"unknown rule action '{action}' — one of: add, remove, default."),
                };
                PermissionRules.Save(_loop.Home, file);
                return Rules(file);
            }

            // The screen's half of `daoris agent rules accept|decline` (PERM2, D74): the person's answer
            // to an agent's proposal. 🔴 The only way a widening an agent proposed ever applies.
            case "RULE_PROPOSAL":
            {
                await Task.CompletedTask;
                RuleProposals.Answer(
                    _loop.Home,
                    PayloadHelper.GetRequiredValue<string>(request.Payload, "id"),
                    PayloadHelper.GetRequiredValue<bool>(request.Payload, "accept"),
                    Optional(request, "note"),
                    DateTimeOffset.UtcNow);
                return Rules(PermissionRules.Load(_loop.Home));
            }

            // What sessions consumed (TOOL3/D57 §4) — measured before it is managed.
            //
            // 🔴 Over this bridge and nowhere else. Per-account usage names a credential profile, and
            // a profile name is already served only over loopback (`ToSession`), so this inherits
            // that boundary rather than arguing for its own. There is no HTTP route onto it.
            case "USAGE":
            {
                await Task.CompletedTask;
                var sessions = _loop.Usage.Sessions;
                return new
                {
                    // Newest first: a person looking at this is asking about recent work.
                    Sessions = sessions
                        .OrderByDescending(entry => entry.When)
                        .Select(entry => new
                        {
                            entry.Session,
                            entry.Repository,
                            entry.Harness,
                            entry.Profile,
                            entry.Used,
                            entry.Size,
                            entry.When,
                        })
                        .ToArray(),
                    Accounts = _loop.Usage.ByAccount().Select(account => new
                    {
                        account.Harness,
                        account.Profile,
                        account.Sessions,
                        account.Used,
                    }).ToArray(),
                };
            }

            // "Look now": a person who just published a quest should not watch a poll countdown.
            case "NUDGE":
                _loop.Nudge();
                // The one verb with nothing to answer — and `await` keeps this method honestly async.
                await Task.CompletedTask;
                return null;

            // *Sync now* (SYNC6b): the tick's own pass for one circle, through the loop's own set —
            // `daoris-driver sync` is the other door to the same pass (D50). What it says comes back
            // in the driver's words; a circle with no remote is its refusal, mapped above.
            case "SYNC_NOW":
            {
                var workspace = RemoteTarget.Workspace(
                    PayloadHelper.GetRequiredValue<string>(request.Payload, "workspace"));
                var pass = _loop.SyncNowAsync(workspace, cancellationToken)
                    ?? throw NotReady();
                var report = await pass;
                return new { Workspace = workspace, report.Problem, report.Notes };
            }

            default:
                throw UnknownType(request);
        }
    }

    /// <summary>
    /// One session's landed work, as a diff (SURF6).
    /// </summary>
    /// <remarks>
    /// <para><b>Read-only, and that is the whole of this route.</b> It runs `git diff` in the tree the
    /// record names and returns what git said. Nothing here writes, merges or removes anything — the
    /// acts that do are the person's and are their own routes, so a surface that only shows the work
    /// cannot accidentally change it.</para>
    ///
    /// <para><b>Unreviewable is INFORMATION, not a failure</b> (D48 §6's class): a record mirrored
    /// from another machine names no tree here, a record made before the base commit was written has
    /// no range, and a tree that has been discarded is gone. None of those is a fault, and each has a
    /// different sentence, so the page can say which.</para>
    /// </remarks>
    private async Task<object?> DiffAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var service = _loop.Service ?? throw NotReady();

        var (tree, baseCommit) = await service.SessionGroundAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to diff. That is what a record looks like when it travelled from the machine "
                + "that did the work.",
                ("session", id));
        }

        if (string.IsNullOrWhiteSpace(baseCommit))
        {
            throw Refusals.Because(
                Refusals.SessionNoBase,
                "this session's record does not say which commit its tree stood at when it began, so "
                + "there is no range to measure. Records made before Daoris started writing that down "
                + "keep their evidence line and cannot gain a diff.",
                ("session", id));
        }

        var diff = await WorkingTree.DiffAsync(tree, baseCommit, cancellationToken);
        if (diff is null)
        {
            throw Refusals.Because(
                Refusals.SessionRangeUnreadable,
                "git could not read that range where the session ran — the tree has moved, been "
                + "discarded, or no longer holds the commit it started from.",
                ("session", id));
        }

        return new
        {
            Session = id,
            diff.Base,
            diff.Truncated,
            Files = diff.Files.Select(file => new
            {
                file.Path,
                file.Status,
                file.Added,
                file.Removed,
                file.Patch,
            }).ToArray(),
        };
    }

    /// <summary>
    /// The files in one session's tree, for the composer's `@` (CONV4d).
    /// </summary>
    /// <remarks>
    /// <b>Unlisted is INFORMATION</b>, in the review's class: a record from another machine names no
    /// tree here, and a tree that is gone or not a repository of its own has nothing git will answer
    /// for. One code for both, because the person's next move is the same — type the path, which both
    /// doors expand as typed (`docs/2026-09-25-message-content-evidence.md`).
    /// </remarks>
    private async Task<object?> FilesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        var files = string.IsNullOrWhiteSpace(tree)
            ? null
            : await WorkingTree.FilesAsync(tree, cancellationToken);
        if (files is null)
        {
            throw Refusals.Because(
                Refusals.SessionTreeUnlisted,
                "git cannot list this session's tree here — its record names no tree on this machine, or "
                + "the tree is gone or not a repository of its own. A path typed after @ still reaches the agent.",
                ("session", id));
        }

        return new { Session = id, files.Files, files.Unlisted };
    }

    /// <summary>
    /// Merge a session's tree into the canonical line, or discard it (SURF6b, D51 rules 6–7).
    /// </summary>
    /// <remarks>
    /// <para><b>A refusal here is an ANSWER, not an error</b> — the checkout is busy, the tree holds
    /// work nobody merged, there is nothing to merge. Each comes back as `{ done: false, message }`
    /// with the sentence the tree layer wrote, exactly as `START_CHAT` does, because the person's next
    /// move is different for each and a code would flatten them into one.</para>
    ///
    /// <para><b>Discard needs `force` said out loud.</b> The unforced call is what produces the
    /// refusal that names what would be lost, so the page asks, shows that sentence, and only then
    /// sends `force`. Destroying work is never a side effect of tidying (D51 rule 7).</para>
    /// </remarks>
    private async Task<object?> ActOnTreeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var merging = request.Type == "MERGE_SESSION_TREE";
        var force = Flag(request, "force");

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to merge or discard.",
                ("session", id));
        }

        // The same home the loop derives for the chat runner and the watch: the directory holding
        // `driver.json`. Derived rather than stored twice, so one answer cannot drift from the other.
        var trees = new SessionTrees(_loop.Home);
        if (merging)
        {
            var merged = await trees.MergeAsync(tree, cancellationToken);
            // The rail's states do not change, but the tree's mergeability does — and the review the
            // person is looking at was computed before this.
            _loop.Nudge();
            return new { Session = id, Done = merged.Merged, merged.Message };
        }

        var removal = await trees.RemoveAsync(tree, force, cancellationToken);
        _loop.Nudge();
        return new { Session = id, Done = removal.Removed, removal.Message };
    }

    /// <summary>
    /// The clean-up's list, or its press (WSR3, D88), over every repository with a checkout here. A tree a
    /// session still running or waiting names is kept whatever it holds.
    /// </summary>
    private async Task<object?> SweepAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var registry = await service.RegistryAsync(cancellationToken);
        var inUse = (await service.ActiveSessionsAsync(cancellationToken))
            .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repositories = registry
            .Where(row => !string.IsNullOrWhiteSpace(row.Root))
            .OrderBy(row => row.Repository, StringComparer.Ordinal)
            .Select(row => (row.Repository, (string?)row.Workspace, row.Root))
            .ToList();
        var trees = new SessionTrees(_loop.Home);

        object Row(SweepItem item) => new
        {
            item.Repository, item.Workspace, item.Branch, HasTree = item.Tree is not null,
            item.Kind, item.Commits, item.Where, item.Detail, item.Removable,
        };

        if (request.Type == "SWEEP_PLAN")
        {
            return new { Branches = (await trees.SweepPlanAsync(repositories, inUse, cancellationToken)).Select(Row).ToArray() };
        }

        HashSet<string>? only = null;
        if (request.Payload is { } payload && payload.TryGetProperty("only", out var named) && named.ValueKind == JsonValueKind.Array)
        {
            only = named.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        }

        var results = await trees.SweepAsync(repositories, inUse, only, cancellationToken);
        _loop.Nudge();
        return new
        {
            Results = results.Select(result => new { Branch = Row(result.Item), result.Removed, result.Message }).ToArray(),
            Removed = results.Count(result => result.Removed),
        };
    }

    /// <summary>
    /// A reviewed session's landing (WSR1, D87): the plan before the press, or the press itself.
    /// </summary>
    /// <remarks>
    /// The pattern is expanded from the session's quest and its title, or for a conversation from the
    /// session and what the person first said — this machine's own record, never the session record's.
    /// A refusal is an answer, as the merge door's are.
    /// </remarks>
    private async Task<object?> LandAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing here to land.",
                ("session", id));
        }

        var questId = await service.SessionQuestAsync(id, cancellationToken);
        // Named for the chain's first quest (WSR5): a chain lands from its last step.
        var subject = await LandingRules.SubjectAsync(
            id, questId, quest => service.FindQuestAsync(quest, cancellationToken),
            _loop.Events.Openings([id]).GetValueOrDefault(id));
        // A rule's plugin says its lines on the console under its name, as a hook's do (D64 §4, D100).
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line)));

        if (request.Type == "LANDING")
        {
            var plan = await trees.PlanAsync(tree, subject, cancellationToken);
            return new { Session = id, plan.Form, plan.Target, plan.Source, plan.Plugin, plan.Problem };
        }

        var landed = await trees.LandAsync(tree, subject, cancellationToken);
        // Kept where the conversation is kept, so the landing and the plugin's word outlast the press (D100).
        if (landed.Landed) _loop.Events.Keep(id, LandingRules.Note(landed), line => _loop.Output.Append(id, line));
        _loop.Nudge();
        return new
        {
            Session = id, Done = landed.Landed, landed.Message, landed.Branch,
            Plugin = landed.Plugin is { } said
                ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed }
                : null,
        };
    }

    /// <summary>
    /// The three moves a person may make on a parked session — `completed`, `declined`, `stopped`,
    /// each carrying what they want the record to say (design §4).
    /// </summary>
    /// <remarks>
    /// <para><b>Narrowed here, not re-judged.</b> The ledger allows a fourth from `awaiting-person`
    /// — back to `working` — and that one is the DRIVER's observation, not a button: a person
    /// resumes a conversation by answering it, which is the composer's job. Narrowing the person's
    /// verbs is a surface rule and belongs on the surface; everything about whether the move is
    /// legal at all stays the ledger's (D36), and its refusal reaches the person verbatim.</para>
    ///
    /// <para><b>The process goes first.</b> A record that says `completed` while this machine still
    /// holds the process is exactly the lie the observed lifecycle exists to prevent — so the
    /// process is let go, and the record moves after. `Stop` answering false is not an error: the
    /// common case is a session parked with nothing of ours still running.</para>
    ///
    /// <para><b>Declining needs a reason</b>, the same rule the quest door already holds and for the
    /// same reason: the note is the part whoever reads the record can act on.</para>
    ///
    /// <para><b>A move with no note still writes one.</b> The store keeps the previous note when a
    /// move carries none (deliberately — a later move must not erase what an earlier one recorded),
    /// so a session finished at a checkpoint would otherwise read <i>reached completed</i> beside
    /// the analysis it was parked with, which says the opposite of what happened. The stamped
    /// sentence also carries the one fact the state cannot: `completed` normally means the session
    /// closed its own quest, and this one means a person decided it was done.</para>
    /// </remarks>
    private async Task<object?> ResolveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var state = PayloadHelper.GetRequiredValue<string>(request.Payload, "state");
        var note = Optional(request, "note");

        if (state is not ("completed" or "declined" or "stopped"))
        {
            throw Refusals.Because(
                Refusals.SessionMoveNotYours, PersonMoves,
                ("state", state), ("moves", "completed, declined, stopped"));
        }

        if (state == "declined" && note is null)
        {
            throw Refusals.Because(Refusals.SessionDeclineNeedsReason, DeclineNeedsReason);
        }

        var service = _loop.Service ?? throw NotReady();

        // False is the common case, not a failure: a parked session usually has no process here.
        _loop.Processes.Stop(id);
        var message = await service.AdvanceAsync(
            id, state, note ?? ByThePerson(state), ct: cancellationToken);
        _loop.Nudge();

        return new { Session = id, State = state, Message = message };
    }

    private const string PersonMoves =
        "A person may finish, decline or stop a parked session — not move it anywhere else. "
        + "Answering it so it carries on is a message, not a move.";

    private const string DeclineNeedsReason =
        "Declining needs a reason: it is the part whoever reads this record can act on.";

    /// <summary>What the record says when the person wrote nothing — never translated: it is data.</summary>
    private static string ByThePerson(string state) => state switch
    {
        "completed" => "The person finished this at a checkpoint.",
        "stopped" => "The person stopped this at a checkpoint.",
        _ => "The person moved this at a checkpoint.",
    };

    /// <summary>
    /// The rules as the page reads them (PERM1). 🔴 No null on the wire where the page tells anything
    /// by it: the bridge leaves a null out, so the machine's scope simply carries no <c>name</c>, and
    /// a file read cleanly carries no <c>problem</c>.
    /// </summary>
    private object Rules(PermissionFile file) => new
    {
        Path = PermissionRules.PathOf(_loop.Home),
        file.Problem,
        Defaults = PermissionRules.Defaults.Select(shipped => new
        {
            shipped.Id,
            List = shipped.List.ToString().ToLowerInvariant(),
            shipped.Rules,
            shipped.Why,
            // The tools a hook default judges (PERM3) — null for a rule default, which the bridge omits.
            shipped.Hook,
            On = !file.DefaultsOff.Contains(shipped.Id, StringComparer.Ordinal),
        }).ToArray(),
        Scopes = new[] { (Scope: "machine", Name: (string?)null, Lists: file.Machine) }
            .Concat(file.Workspaces.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (Scope: "workspace", Name: (string?)p.Key, Lists: p.Value)))
            .Concat(file.Repositories.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (Scope: "repository", Name: (string?)p.Key, Lists: p.Value)))
            .Select(held => new { held.Scope, held.Name, held.Lists.Allow, held.Lists.Ask, held.Lists.Deny })
            .ToArray(),
        // What agents proposed about these rules (PERM2, D74), newest first. Structured rather than a
        // sentence, so the page says it in the person's language. 🔴 Not the folder the session ran in:
        // it is a machine path, and the page is told who proposed, never where they stood.
        Proposals = RuleProposals.Load(_loop.Home).Select(proposal => new
        {
            proposal.Id,
            State = proposal.State.ToString().ToLowerInvariant(),
            proposal.Change.Action,
            Scope = proposal.Change.Scope.ToString().ToLowerInvariant(),
            proposal.Change.Name,
            List = proposal.Change.List?.ToString().ToLowerInvariant(),
            proposal.Change.Rule,
            proposal.Change.Default,
            proposal.Change.On,
            proposal.Why,
            proposal.Session,
            proposal.Ask,
            Proposed = proposal.Proposed.ToString("O"),
            Settled = proposal.Settled?.ToString("O"),
            proposal.SettledBy,
            proposal.Note,
        }).ToArray(),
    };

    private static RuleScope ScopeOf(IpcRequest request) => Optional(request, "scope") switch
    {
        null or "machine" => RuleScope.Machine,
        "workspace" => RuleScope.Workspace,
        "repository" => RuleScope.Repository,
        var other => throw new DriverException($"a rule reaches the `machine`, a `workspace` or a `repository`, not `{other}`."),
    };

    // What the page may leave out, read one way (REV3 CLEAN1). The routes read optional values by hand,
    // and differently: one took an empty `adapter` as a name where this takes it as absent.

    /// <summary>A string the page may send, or null — blank is absent.</summary>
    private static string? Optional(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>
    /// One setting's change as the page sent it (AGT6): a text sets it, null clears it, and a key left out
    /// is no change at all — the three a person can mean, kept apart.
    /// </summary>
    private static AgentSettingEdit? Edit(IpcRequest request, string name) =>
        request.Payload is { } payload && payload.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Null => new AgentSettingEdit(null),
                JsonValueKind.String => new AgentSettingEdit(value.GetString()),
                _ => throw new DriverException($"`{name}` is set with a name, or cleared with null."),
            }
            : null;

    /// <summary>An effort per model as the page sent it: the model's name, then a value or null.</summary>
    private static IReadOnlyDictionary<string, AgentSettingEdit>? PerModel(IpcRequest request)
    {
        if (request.Payload is not { } payload || !payload.TryGetProperty("perModel", out var models)
            || models.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return models.EnumerateObject().ToDictionary(
            model => model.Name,
            model => model.Value.ValueKind switch
            {
                JsonValueKind.Null => new AgentSettingEdit(null),
                JsonValueKind.String => new AgentSettingEdit(model.Value.GetString()),
                _ => throw new DriverException($"the effort for `{model.Name}` is set with a name, or cleared with null."),
            },
            StringComparer.Ordinal);
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

    /// <summary>An account's settings as the page reads them (AGT6): the two keys, each model's own effort, and why not.</summary>
    private static object AccountSettings(AgentSettingsRead read) =>
        new { read.Model, read.Effort, PerModel = PerModelOf(read), read.Problem };

    private static object[] PerModelOf(AgentSettingsRead read) =>
        [.. read.PerModel.Select(entry => (object)new { entry.Model, entry.Effort })];

    /// <summary>A flag the page may send: true only when it says true.</summary>
    private static bool Flag(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    /// <summary>A whole number the page may send, or null.</summary>
    private static long? Number(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

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
        return line => DriverLoop.EmitOutput(
            _events, $"{harness}:{action}", [new ConsoleLine(Interlocked.Increment(ref sequence), line)]);
    }

    /// <summary>
    /// A process action on a harness, answered once it has started — <c>HARNESS_ACTION</c>'s own start,
    /// and the one Ask Daoris's Apply of an update or a pin calls too (HELP6), so the two cannot differ.
    /// </summary>
    /// <remarks>
    /// 🔴 A process action is answered when the process has STARTED, and its end is news
    /// (HARNESS_ENDED) — the same way a conversation's ending is (D49 §3). It waits on a network, or on
    /// a person in a browser, and a request that waited with it timed out on the bridge at thirty seconds
    /// while the login ran on: the page closed its panel, the row said nothing had changed, and the
    /// process kept waiting for a browser nobody was told about (measured on the installed shell,
    /// 2026-09-23). While it runs the page may answer it or stop it (HARNESS_INPUT, HARNESS_CANCEL).
    /// </remarks>
    /// <param name="ended">Told the exit code and any problem once the process ends, after the news goes out.</param>
    private async Task StartProcessActionAsync(
        string harness, string action, string? profile, string? version, HarnessToolchain toolchain,
        IReadOnlyList<string>? command, Action<string> stream, DriverConfig config, Action<int, string?>? ended = null)
    {
        var owner = toolchain.Owner(harness);
        var key = $"{harness}:{action}";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<HarnessRun> track = run =>
        {
            _actions[key] = run;
            started.TrySetResult();
        };
        // 🔴 Signing in to ANOTHER account (D66 §3): the account is made by the sign-in, not named
        // before it. It opens under the next free `account-N` — nobody knows whose it is yet — and the
        // tool's own answer names who, on the roster, once it ends.
        var fresh = action == "login-new"
            ? HarnessSettings.NextAccount(_loop.Harnesses.Home, owner)
            : null;
        var profileHome = HarnessSettings.ProfileHome(
            _loop.Harnesses.Home, owner,
            fresh ?? profile ?? _loop.Harnesses.Settings.Resolve(owner, null, null) ?? "default");
        Func<Task<int>> run = action switch
        {
            "install" => () => HarnessActions.InstallAsync(toolchain, stream, CancellationToken.None, track),
            "update" => () => UpdateAsync(harness, toolchain, command, stream, CancellationToken.None, track),
            "login" => () => HarnessActions.LoginAsync(toolchain, command, profileHome, stream, CancellationToken.None, track),
            "login-new" => () => SignInAsync(harness, fresh!, toolchain, command, profileHome, stream, config, track),
            // The managed toolchain (TOOL2/D57) — the desktop's half of `daoris agent pin|unpin`, over
            // the same file.
            _ => () => PinAsync(harness, toolchain, stream, version!, CancellationToken.None, track),
        };

        // 🔴 One at a time on this machine, claimed as the action starts and released by
        // `RunActionAsync` however it ends (REV3). The page re-enabled its buttons once the request
        // answered `started`, and a second login under the same name took the first's place in
        // `_actions`: the first could no longer be answered or stopped, and its end removed the second's
        // entry. Claimed last, so nothing above can throw with it held.
        if (Interlocked.CompareExchange(ref _acting, key, null) is { } busy)
        {
            throw Refusals.Because(
                Refusals.HarnessActionBusy,
                $"{busy} is still running — wait for it to end, or stop it, before starting another.",
                ("running", busy));
        }

        var work = RunActionAsync(key, harness, action, fresh ?? profile, run, started.Task, config, ended);

        await Task.WhenAny(started.Task, work);
        // A refusal before the process started — no installer, no login flow, a binary that did not
        // start — travels as a refusal, exactly as it did when the request waited.
        if (work.IsCompleted) await work;
    }

    /// <summary>
    /// A process action to its end, and the end announced — after the request that started it has
    /// been answered. A failure before the process started is the caller's to refuse; one after it
    /// is news like any other end, because nobody awaits this any more.
    /// </summary>
    private async Task RunActionAsync(
        string key, string harness, string action, string? profile, Func<Task<int>> run, Task started,
        DriverConfig config, Action<int, string?>? ended = null)
    {
        int code;
        try
        {
            code = await run();
        }
        catch (Exception error) when (started.IsCompletedSuccessfully)
        {
            await AnnounceAsync(harness, action, profile, -1, error.Message, config);
            Tell(-1, error.Message);
            return;
        }
        finally
        {
            _actions.TryRemove(key, out _);
            // The slot goes with the action, however it ended — before it started included.
            Volatile.Write(ref _acting, null);
        }

        await AnnounceAsync(harness, action, profile, code, null, config);
        Tell(code, null);

        // Whoever asked to hear the end (HELP6: Ask Daoris's conversation) hears it after the news; a
        // listener that fails costs its own view, never the end the screen was told.
        void Tell(int exit, string? problem)
        {
            try
            {
                ended?.Invoke(exit, problem);
            }
            catch (Exception)
            {
                // The news already went out.
            }
        }
    }

    private async Task AnnounceAsync(
        string harness, string action, string? profile, int code, string? problem, DriverConfig config)
    {
        // Whatever it did, what this machine HAS has probably changed — so the next question asks
        // the tool again rather than answering from before, and the news arrives after the roster.
        IReadOnlyList<HarnessReport> roster = [];
        try
        {
            roster = await _loop.Harnesses.RosterAsync(config, refresh: true, CancellationToken.None);
        }
        catch (Exception)
        {
            // The roster is asked again on the page's next question; the end is still news.
        }

        var signingIn = action is "login" or "login-new" && profile is not null;
        await _events.EmitAsync("DAORIS", "HARNESS_ENDED", new
        {
            Harness = harness, Action = action, Profile = profile, ExitCode = code, Problem = problem,
            // Who signed in (D66 §3) — the tool's own answer, from the roster just read, so the
            // sentence a person hears names the account the way they know it.
            Account = signingIn
                ? roster.FirstOrDefault(report => report.Adapter == harness)?.Profiles
                    .FirstOrDefault(each => each.Name == profile)?.Account
                : null,
            // Whether a sign-in to another account left one behind: it does only when it finished.
            Kept = action == "login-new" && profile is not null
                ? Directory.Exists(HarnessSettings.ProfileHome(
                    _loop.Harnesses.Home, _loop.Harnesses.Toolchain(harness)?.Owner(harness) ?? harness, profile))
                : (bool?)null,
        });
    }

    /// <summary>
    /// Sign in to another account (D66 §3): the tool's own login flow into a fresh directory, kept
    /// only when the sign-in finished.
    /// </summary>
    /// <remarks>
    /// "Finished" is the tool's exit code and then the tool's own word, asked of that one home: a
    /// zero exit whose home still reports signed OUT signed nobody in. An answer it cannot give is
    /// kept, by the rule every unknown login state follows (SES3) — the person watched the sign-in
    /// and can remove it. Otherwise the directory existed only for this sign-in, and goes: the list
    /// is exactly what it was before the press, whether the tool failed, was stopped, or never
    /// started.
    /// </remarks>
    private async Task<int> SignInAsync(
        string harness, string fresh, HarnessToolchain toolchain, IReadOnlyList<string>? command,
        string profileHome, Action<string> stream, DriverConfig config, Action<HarnessRun> track)
    {
        var code = -1;
        try
        {
            code = await HarnessActions.LoginAsync(
                toolchain, command, profileHome, stream, CancellationToken.None, track);
            return code;
        }
        finally
        {
            var (login, account) = code == 0
                ? await _loop.Harnesses.LoginOfAsync(harness, config, fresh, CancellationToken.None)
                : (LoginState.Out, null);

            if (code != 0 || login == LoginState.Out)
            {
                try
                {
                    HarnessSettings.RemoveProfile(_loop.Harnesses.Home, toolchain.Owner(harness), fresh);
                    stream("nothing was signed in, so nothing was kept — the account opened for it is gone again.");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    stream($"nothing was signed in, and {profileHome} could not be removed — {error.Message}");
                }
            }
            else
            {
                stream(account is { Length: > 0 }
                    ? $"signed in as {account} — this machine lists it as `{fresh}`."
                    : $"signed in — `{harness}` did not say who, so this machine lists it as `{fresh}`.");
            }
        }
    }

    /// <summary>The action running under `harness:action`, or the refusal that names it.</summary>
    private HarnessRun Running(IpcRequest request)
    {
        var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        return _actions.TryGetValue($"{harness}:{action}", out var run)
            ? run
            : throw Refusals.Because(
                Refusals.HarnessActionIdle,
                $"nothing is running for `{harness}` {action} — it finished, or was never started.",
                ("harness", harness), ("action", action));
    }

    /// <summary>
    /// Install a version into the directory Daoris owns, and pin to it — <b>in that order</b>
    /// (TOOL2/D57).
    /// </summary>
    /// <remarks>
    /// 🔴 The pin is written only after the install succeeded. A pin naming a version that is not
    /// there refuses every spawn, so writing it first would turn a failed download into a machine
    /// that cannot start a session.
    /// </remarks>
    private async Task<int> PinAsync(
        string harness, HarnessToolchain toolchain, Action<string> stream, string version,
        CancellationToken ct, Action<HarnessRun> started)
    {
        var code = await HarnessActions.PinAsync(
            toolchain, _loop.Harnesses.Home, harness, version, stream, ct, started);

        if (code == 0) _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath);
        return code;
    }

    /// <summary>
    /// Update a door (USE1a): a pinned one moves its pin to the newest release, written the way
    /// <see cref="PinAsync"/> writes one — only after that version is installed — and an unpinned one
    /// runs its own updater. The machine's pin, the one the roster shows.
    /// </summary>
    private Task<int> UpdateAsync(
        string harness, HarnessToolchain toolchain, IReadOnlyList<string>? command, Action<string> stream,
        CancellationToken ct, Action<HarnessRun> started) =>
        HarnessActions.UpdateAsync(
            toolchain, command, _loop.Harnesses.Home, harness,
            _loop.Harnesses.Settings.ResolveVersion(harness, null, null),
            version => _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath),
            stream, ct, started);

    /// <summary>
    /// An account's own model and effort, written — <c>SET_AGENT_SETTINGS</c>'s own write, and the one Ask
    /// Daoris's Apply makes too (HELP6). Never the tool's own configuration home, and never an account a
    /// setting would bring into being.
    /// </summary>
    /// <param name="edits">The keys to change, read only once the account is known to be one Daoris keeps.</param>
    private (string Owner, string Profile, AgentSettingsRead Read) WriteAgentSettings(
        string harness, string? profile,
        Func<(AgentSettingEdit? Model, AgentSettingEdit? Effort, IReadOnlyDictionary<string, AgentSettingEdit>? PerModel)> edits)
    {
        var toolchain = _loop.Harnesses.Toolchain(harness)
            ?? throw new DriverException(
                $"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
        var owner = toolchain.Owner(harness);
        var file = _loop.Harnesses.AccountToolchain(harness)?.SettingsFile
            ?? throw new DriverException(
                $"`{owner}` keeps its settings in files of its own that Daoris does not know the shape of, "
                + "so Daoris offers none — set its model with the tool itself.");
        var named = profile
            ?? throw new DriverException(
                $"name the account these settings are for — `{owner}`'s own configuration home is the tool's, "
                + "and Daoris never touches it.");
        var accounts = HarnessSettings.Profiles(_loop.Harnesses.Home, owner);
        if (!accounts.Contains(named, StringComparer.Ordinal))
        {
            throw new DriverException(
                $"`{owner}` has no account `{named}` on this machine — accounts that exist: "
                + (accounts.Count > 0 ? string.Join(", ", accounts) : "(none)"));
        }

        var (model, effort, perModel) = edits();
        var read = AgentSettings.Write(
            Path.Combine(HarnessSettings.ProfileHome(_loop.Harnesses.Home, owner, named), file), model, effort, perModel);
        return (owner, named, read);
    }

    /// <summary>
    /// The doors Ask Daoris's Apply goes through (HELP6): each the code a screen's own route runs, so an
    /// Apply is what the screen would have done. <paramref name="service"/> is the loop's, or null before
    /// it is up — when an ask or a delete is the cold-start sentence.
    /// </summary>
    public IHelpDoors HelpDoors(ServiceClient? service) => new ScreenDoors(this, service);

    private sealed class ScreenDoors(DriverModule module, ServiceClient? service) : IHelpDoors
    {
        // SET_DRIVABLE, SET_LINE, SET_LANDING…: an edit to the driver's file, then a nudge.
        public void Change(Func<DriverConfig, DriverConfig> edit) => module.Change(edit);

        // The ask composer's door, the local host's `POST /api/asks`.
        public Task<AskAnswer> AskAsync(string workspace, string sentence, CancellationToken ct) =>
            (service ?? throw NotReady()).AskAsync(workspace, sentence, [], [], null, ct);

        // The quest drawer's Delete: the local host's `DELETE /api/quests/{id}`.
        public Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct) =>
            (service ?? throw NotReady()).DeleteQuestAsync(id, ct);

        // The ask's record's Delete: the local host's `DELETE /api/asks/{id}`.
        public Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct) =>
            (service ?? throw NotReady()).DeleteAskAsync(id, ct);

        // HARNESS_ACTION's own start, streamed under the same key and ended with the same news.
        public Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct)
        {
            var config = DriverConfig.Load(module._loop.ConfigPath);
            var toolchain = module._loop.Harnesses.Toolchain(harness)
                ?? throw new DriverException($"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
            return module.StartProcessActionAsync(
                harness, action, profile: null, version, toolchain, config.Commands.GetValueOrDefault(harness),
                module.Relay(harness, action), config, ended);
        }

        // SET_AGENT_SETTINGS's own write.
        public AgentSettingsRead SetAgentSettings(string harness, string account, AgentSettingEdit? model, AgentSettingEdit? effort) =>
            module.WriteAgentSettings(harness, account, () => (model, effort, null)).Read;

        // `daoris plugin add`'s copy, the driver's twin (PLUG9); the loop is asked to look, and it starts
        // what the plugin runs at that look, as it does any plugin. Nothing runs here.
        public void AddPlugin(string folder)
        {
            PluginInstall.Add(module._loop.Home, folder, AdapterSet.Built().Names);
            module._loop.Nudge();
        }

        // PLUGIN_ACTION's own enable|disable: the same lookup, the same row, the same nudge.
        public void SwitchPlugin(string id, bool on)
        {
            module.SwitchPlugin(module.InstalledPlugin(id), on);
            module._loop.Nudge();
        }

        // PLUGIN_INSTALL's own copy of one of the install's offers (PLUG9 d); nothing runs here.
        public void AddOffer(string id)
        {
            PluginInstall.AddOffer(module._loop.Home, module.OffersFolder, id, AdapterSet.Built().Names);
            module._loop.Nudge();
        }

        // PLUGIN_UPDATE's own apply (PLUG9 c): the hook stopped, the folder swapped, the loop asked to look.
        public async Task UpdatePluginAsync(string id, CancellationToken ct) =>
            await module.UpdatePluginAsync(id).ConfigureAwait(false);
    }

    /// <summary>Where an installed plugin came from, for its row: a folder, an offer, none recorded, or a record that does not read.</summary>
    private static object SourceOf(string installFolder)
    {
        var (source, problem) = PluginSource.Read(installFolder);
        return new
        {
            Kind = problem is not null ? "unread" : source is null ? "none" : source.Offer is not null ? "offer" : "folder",
            Folder = source?.Folder,
            Offer = source?.Offer,
            Problem = problem,
        };
    }

    private static object[] ChangesOf(IReadOnlyList<PluginChange> changes) =>
        [.. changes.Select(change => (object)new { change.What, change.Was, change.Now })];

    /// <summary>
    /// An update, as <c>PLUGIN_UPDATE</c> and Ask Daoris's update door make it (PLUG9 c): judged, the plugin's
    /// hook stopped first (on Windows a running process holds its folder), the install folder swapped, and
    /// the loop asked to look, which starts the new one as it does any plugin.
    /// </summary>
    private async Task<PluginUpdatePlan> UpdatePluginAsync(string id)
    {
        var reserved = AdapterSet.Built().Names;
        var (plan, refusal) = PluginInstall.PlanUpdate(_loop.Home, id, reserved, OffersFolder);
        if (plan is null) throw new DriverException($"{refusal} Nothing was replaced.");
        await _loop.StopPluginAsync(plan.Id).ConfigureAwait(false);
        var updated = PluginInstall.Update(_loop.Home, plan.Id, reserved, OffersFolder);
        _loop.Nudge();
        return updated;
    }

    /// <summary>An installed plugin by its id, or the Plugins screen's refusal (<c>PLUGIN_ACTION</c>, PLUG9's door).</summary>
    private PluginEntry InstalledPlugin(string id) =>
        PluginCatalog.Load(_loop.Home).Plugins
            .FirstOrDefault(p => string.Equals(p.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw Refusals.Because(Refusals.PluginUnknown, $"no plugin `{id}` on this machine.", ("id", id));

    /// <summary>
    /// A plugin switched on or off: a row in <c>plugins.json</c>. <c>PLUGIN_ACTION</c> and Ask Daoris's plugin
    /// door (PLUG9) both call this, so the screen and the card cannot drift.
    /// </summary>
    private void SwitchPlugin(PluginEntry entry, bool on)
    {
        if (on) PluginState.Enable(_loop.Home, entry.Manifest.Id);
        else PluginState.Disable(_loop.Home, entry.Manifest.Id);
    }

    /// <summary>What every route that needs the loop's service says before it answers (REV3 CLEAN1: five wrote it).</summary>
    private static Exception NotReady() => Refusals.Because(
        Refusals.DriverNotReady, "the driver is still coming up — its service is not answering yet. A moment.");

    /// <summary>
    /// Make a credential profile under a name the caller chose: a directory, and nothing else (DEPLOY3).
    /// </summary>
    /// <remarks>
    /// Idempotent, exactly as the CLI verb is — asking for one that exists is an answer, not a
    /// failure. The page makes accounts by signing in (<c>login-new</c>, D66 §3); this is the
    /// bridge's half of <c>daoris agent profile add</c>, for a name a person picks before signing in.
    /// </remarks>
    private int ProfileAdd(string harness, IpcRequest request)
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_loop.Harnesses.Home, harness, Named(request)));
        return 0;
    }

    /// <summary>
    /// Remove an account — 🔴 <b>its directory with it, credentials included</b> (D66 §3).
    /// </summary>
    /// <remarks>
    /// <para>This amends SES3's "removing one deletes nothing", on the owner's word: Forget did not
    /// delete the account. The old rule un-pointed it and kept any directory the tool would not
    /// call signed out, so a removed account stayed listed and signed in — the leftover the person
    /// pressed the button to be rid of. The page asks twice before it sends this; the terminal twin
    /// is <c>daoris agent profile remove</c>.</para>
    ///
    /// <para>Only ever a profile: the tool's own configuration home is not under Daoris's directory,
    /// and no name reaches it.</para>
    ///
    /// <para>A delete that fails — a running session holding a file open in it — says so and leaves
    /// the wiring as it was, because the account is still there to point at.</para>
    /// </remarks>
    private int ProfileRemove(string harness, IpcRequest request, Action<string> stream)
    {
        var profile = Named(request);
        var directory = HarnessSettings.ProfileHome(_loop.Harnesses.Home, harness, profile);
        try
        {
            stream(HarnessSettings.RemoveProfile(_loop.Harnesses.Home, harness, profile)
                ? $"removed {directory} — the account and its sign-in are gone from this machine."
                : $"`{profile}` is not on this machine — there was nothing to remove.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            stream($"could not remove {directory} — {error.Message} A session running as this "
                + "account may hold a file open in it; stop it and remove again. Part of it may already be gone.");
            return 1;
        }

        var settings = _loop.Harnesses.Settings;
        if (settings.Defaults.TryGetValue(harness, out var machine) && machine == profile)
        {
            settings = settings.WithDefault(harness, null);
        }

        foreach (var workspace in settings.Workspaces.Keys.ToList())
        {
            if (settings.Workspaces[workspace].TryGetValue(harness, out var held) && held == profile)
            {
                settings = settings.WithWorkspaceDefault(workspace, harness, null);
            }
        }

        settings.Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    /// <summary>
    /// Which profile this harness runs as — the machine's, or one workspace's (D49 §4). 🔴 No profile
    /// named CLEARS it: "use the tool's own home again" is a choice a person makes, not an argument
    /// they forgot, and the file's own rule is that absence means the harness's own home.
    /// </summary>
    private int ProfileDefault(string harness, IpcRequest request)
    {
        var profile = Optional(request, "profile");
        var workspace = Optional(request, "workspace");
        var settings = _loop.Harnesses.Settings;

        settings = workspace is { Length: > 0 }
            ? settings.WithWorkspaceDefault(workspace, harness, profile)
            : settings.WithDefault(harness, profile);

        settings.Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    /// <summary>The profile a profile verb is about. Absent is a refusal, never a guess.</summary>
    private static string Named(IpcRequest request) =>
        Optional(request, "profile") is { Length: > 0 } profile
            ? profile
            : throw Refusals.Because(
                Refusals.HarnessProfileNeeded,
                "that action needs a profile name.",
                ("action", "profile"));

    /// <summary>Back to `PATH`. Nothing is deleted — re-pinning that version needs no download.</summary>
    private int Unpin(string harness)
    {
        _loop.Harnesses.Settings.WithVersion(harness, null).Save(_loop.Harnesses.SettingsPath);
        return 0;
    }

    private object State()
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        return new
        {
            _loop.ConfigPath,
            // Where this machine's Daoris lives (D63), and what establishing it did this start — a
            // machine-local path, answered only over this bridge, like every path here.
            _loop.Home,
            _loop.HomeNotice,
            _loop.HostNotice,
            config.Drivable,
            config.Holds,
            config.Trees,
            config.Cap,
            config.Adapter,
            config.PollSeconds,
            config.Notify,
            config.Strikes,
            config.Forgiven,
            // 🔴 Reported so the page can show it, and modelled on the record so no toggle deletes it.
            // Off is "" on the wire, never null: the bridge leaves a null out, and the page tells a
            // shell older than the intake by this field's absence (AGT6, seen on the window).
            IntakeAdapter = config.IntakeAdapter ?? "",
            // Off is "" on the wire for the intake's reason: a shell older than Ask Daoris sends no field.
            HelperAdapter = config.HelperAdapter ?? "",
            // The lines as set (WSR2), as rows rather than an object's keys: a key policy on the
            // bridge would respell a repository's name.
            Lines = config.Lines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Branch = p.Value }).ToArray(),
            WorkspaceLines = config.WorkspaceLines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Branch = p.Value }).ToArray(),
            // The landing rules as set (WSR1), as rows for the same reason.
            Landings = config.Landings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, p.Value.Form, p.Value.Pattern, p.Value.Tidy, p.Value.Plugin }).ToArray(),
            WorkspaceLandings = config.WorkspaceLandings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, p.Value.Form, p.Value.Pattern, p.Value.Tidy, p.Value.Plugin }).ToArray(),
            Running = _loop.Processes.Running,
            // Who is driving Daoris's browser (BRW8): the running sessions handed a server that drives it.
            DrivingBrowser = _loop.Processes.DrivingBrowser,
        };
    }

    /// <summary>
    /// What a proposal of Ask Daoris's is judged against (HELP1c): the driver's file, and the names the
    /// machine holds — its registered repositories and their circles, and the agents it has. For the
    /// kinds that need them (HELP6), each door as the Agents screen's roster reads it, and every quest
    /// and ask with the service's own reading of whether it may be deleted — asked only then.
    /// </summary>
    private async Task<(DriverConfig Config, HelpMachineFacts Facts)> HelpFactsAsync(
        ServiceClient service, IReadOnlyCollection<HelpProposal> proposals, CancellationToken ct)
    {
        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var workspaces = snapshot.Repositories.Select(known => known.Workspace)
            .Append(RemoteTarget.DefaultWorkspace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var config = DriverConfig.Load(_loop.ConfigPath);
        var facts = new HelpMachineFacts(
            [.. snapshot.Repositories.Select(known => known.Repository)], workspaces, _loop.Harnesses.Adapters.Names)
        {
            // A landing rule naming a plugin is judged by the catalogue the landing route reads (HELP8, D100).
            Plugins = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names),
            // A plugin is added from a registered checkout, never from the home, and may not shadow a
            // harness this build carries (PLUG9) — what `daoris plugin add` refuses.
            Home = _loop.Home,
            Checkouts = snapshot.Repositories
                .GroupBy(known => known.Repository, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(same => same.Key, same => same.First().Root, StringComparer.OrdinalIgnoreCase),
            Reserved = AdapterSet.Built().Names,
            // The install's own plugins (PLUG9 d), which an add may name by id, and where an offer's update reads.
            OffersFolder = OffersFolder,
            Offers = PluginOffers.Load(OffersFolder, _loop.Home, AdapterSet.Built().Names),
        };

        if (proposals.Any(proposal => proposal.Kind is "agent" or "account"))
        {
            // The HARNESSES route's own reading of each door, field by field.
            var roster = await _loop.Harnesses.RosterAsync(config, ct: ct).ConfigureAwait(false);
            var settings = _loop.Harnesses.Settings;
            facts = facts with
            {
                Doors = [.. roster.Select(report =>
                {
                    var toolchain = _loop.Harnesses.Toolchain(report.Adapter);
                    var pinned = settings.ResolveVersion(report.Adapter, null, null);
                    var owner = toolchain?.Owner(report.Adapter) ?? report.Adapter;
                    return new HelpDoorFacts(report.Adapter)
                    {
                        Present = report.Present,
                        Updates = toolchain is null ? null : HarnessActions.UpdateOf(toolchain, pinned),
                        Pinned = pinned,
                        Package = toolchain?.Package,
                        Channel = toolchain?.Channel,
                        Product = toolchain?.Product,
                        Owner = owner,
                        Accounts = HarnessSettings.Profiles(_loop.Harnesses.Home, owner),
                        SettingsKnown = _loop.Harnesses.AccountToolchain(report.Adapter)?.SettingsFile is { Length: > 0 },
                    };
                })],
            };
        }

        if (proposals.Any(proposal => proposal.Kind == "delete"))
        {
            var (quests, asks) = await HelpProposals.RecordsAsync(service, ct).ConfigureAwait(false);
            facts = facts with { Quests = quests, Asks = asks };
        }

        return (config, facts);
    }

    /// <summary>
    /// A result said into Ask Daoris's conversation in the person's name (HELP1c): plain words, since a
    /// person's message renders verbatim and a sentence full of backticks read as noise on the window.
    /// </summary>
    private static string InPersonsWords(string told) => told.Replace("`", "", StringComparison.Ordinal);

    private void Change(Func<DriverConfig, DriverConfig> change)
    {
        change(DriverConfig.Load(_loop.ConfigPath)).Save(_loop.ConfigPath);
        _loop.Nudge();
    }
}
