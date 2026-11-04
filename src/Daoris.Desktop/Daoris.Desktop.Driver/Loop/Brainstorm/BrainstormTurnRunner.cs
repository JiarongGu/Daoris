using System.Text.Json;
using static Daoris.Driver.DriverBrainstorms;

namespace Daoris.Driver;

/// <summary>A brainstorm's voice and its real turns: the start plan, the room measurement, the one turn through the walk and the limit cooling.</summary>
internal sealed class BrainstormTurnRunner(DriverBrainstorms owner, LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// A voice's start plan (the foundation design §2.4's table, FOUNDATION1j part ii; D205's round-2 note and §4.4): Brainstorm's, its order
    /// the voice's own entry alone, frozen with the brainstorm's plan, so a voice that cannot run has nothing to be replaced by; its workspace
    /// the request's, for the version that workspace pins.
    /// </summary>
    internal static StartPlan Voice(BrainstormTurn turn, DriverConfig config, HarnessRoster roster) =>
        new(ParticipantWork.Brainstorm, config, roster, roster.Adapters, new PassMemory(), order: [turn.Entry ?? new ActorEffective(turn.Agent, null, null, "before")],
            catalogue: RoleCatalog.ReadHome(roster.Home), place: new RolePlace(turn.Workspace))
        {
            Workspace = turn.Workspace, Subject = $"brainstorm:{turn.Id}",
        };

    /// <summary>
    /// The account a voice's turn runs on: its plan's one ask for its one entry, each account judged by its reading for the voice's model, the
    /// ones it already passed left out (D205 §4.4), counted where it chooses one. Not allowed, the voice is dropped, never replaced.
    /// </summary>
    internal static Task<HarnessSelection> VoiceAsync(BrainstormTurn turn, DriverConfig config, HarnessRoster roster, CancellationToken ct)
    {
        var plan = Voice(turn, config, roster);
        return plan.AccountsAsync(turn.Agent, plan.Order[0].Model, turn.Passed, ct);
    }

    /// <summary>The minutes a room measurement's one pass may take (BRAINSTORM1c): the policy's default for a pass, the first run's to read again.</summary>
    internal const int MeasureMinutes = 10;

    /// <summary>
    /// A room measurement of <paramref name="agent"/> (BRAINSTORM1c, D176 point 2, D53): one probe pass through the same walk, posture,
    /// connector and usage as a brainstorm's generator, judged by what its run shows (<see cref="BrainstormRoomProbe"/>), and kept as evidence
    /// under <see cref="BrainstormFiles.MeasureId"/>. Not <paramref name="spend"/>, it only walks: the account it would run on, nothing started.
    /// </summary>
    /// <remarks>
    /// One pass is one turn on the walk's first ready account: a spend, said as one, and the owner's to ask for. It never runs by itself: its
    /// verb is the press. Its probe names paths a real pass is never told, in a folder of its own that is removed when it ends; nothing is
    /// written in any repository.
    /// </remarks>
    internal async Task<RoomMeasure> MeasureRoomAsync(string agent, bool spend, CancellationToken ct)
    {
        var files = new BrainstormFiles(_context.Home);
        var id = BrainstormFiles.MeasureId(agent);
        var entry = new ActorEffective(agent, null, null, "before");
        HarnessSelection selection;
        try
        {
            _context.Adapters.Resolve(agent);
            selection = await VoiceAsync(new BrainstormTurn(id, "g1", agent, "", "", "", MeasureMinutes, entry), _context.Config, _context.Harnesses, ct)
                .ConfigureAwait(false);
        }
        catch (DriverException)
        {
            return new RoomMeasure(agent, null, RoomVerdicts.NotRun, StartPasses.Unavailable, [], DateTimeOffset.UtcNow);
        }

        if (!selection.Allowed) return new RoomMeasure(agent, null, RoomVerdicts.NotRun, PassCode(selection), [], DateTimeOffset.UtcNow);
        if (!spend) return new RoomMeasure(agent, selection.Profile, RoomVerdicts.NotRun, "dry-run", [], DateTimeOffset.UtcNow);

        files.Clear(id);
        var laid = BrainstormRoomFiles.Lay(
            Path.Combine(files.Rooms(id), "g1"), files.PathOf(id, "probe"), () => "probe-" + Guid.NewGuid().ToString("N")[..12]);
        var instruction = BrainstormRoomProbe.Instruction(laid);
        File.WriteAllText(Path.Combine(laid.Room, BrainstormInstruction.InstructionFile), instruction);
        var turn = new BrainstormTurn(id, "g1", agent, laid.Room, instruction, files.AnswerPath(id, "g1"), MeasureMinutes, entry);
        try
        {
            using var limit = new CancellationTokenSource(owner.Minute * MeasureMinutes, owner.Clock);
            using var window = CancellationTokenSource.CreateLinkedTokenSource(ct, limit.Token);
            BrainstormTurnRun ran;
            try
            {
                ran = await (_context.BrainstormTurns ?? TurnAsync)(turn, window.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                ran = new BrainstormTurnRun(true, null, TimedOut, $"stopped  room measurement of {agent}");
            }

            // What the turn did not start is not a verdict: nothing ran, and nothing is said of the posture.
            var transcript = BrainstormFiles.ReadText(files.PathOf(id, "passes", "g1.log")) ?? "";
            var probes = ran.Ran ? BrainstormRoomFiles.Judge(laid, transcript) : [];
            var measure = new RoomMeasure(
                agent, ran.Account ?? selection.Profile, ran.Ran ? BrainstormRoomProbe.Verdict(probes) : RoomVerdicts.NotRun, ran.Code, probes, DateTimeOffset.UtcNow);
            files.WriteMeasure(id, measure);
            return measure;
        }
        finally
        {
            files.RemoveRooms(id);
            BrainstormFiles.Remove(files.PathOf(id, "probe"));
        }
    }

    /// <summary>
    /// The code a pass is <c>unavailable</c> with where its voice's walk found no ready account (BRAINSTORM1c, D176 point 7): the walk's own,
    /// read from its facts and never its words (<see cref="StartPasses.Of"/>), the one vocabulary every walk over an entry says: <c>unavailable</c>
    /// where nothing of the agent can run here, <c>not-offered</c>, <c>cooling</c>, <c>signed-out</c>, else <c>held</c>.
    /// </summary>
    internal static string PassCode(HarnessSelection selection) => StartPasses.Of(selection);

    /// <summary>
    /// One real turn (BRAINSTORM1a, BRAINSTORM1a2): the agent's account by the walk, its spawn prepared as any session's, the
    /// <c>brainstorm</c> posture held to its room, one turn framed as one prompt, its connector its only one and carrying the turn so
    /// its result tool writes the answer file, and its own minutes. Its record is the brainstorm's own, under the home's data: no
    /// session record is opened on the service, and nothing of it syncs. A limit its door reports cools the account (D125,
    /// BRAINSTORM1b), and the pass starts once more on the next.
    /// </summary>
    internal async Task<BrainstormTurnRun> TurnAsync(BrainstormTurn turn, CancellationToken ct)
    {
        var sessionId = $"brainstorm-{turn.Id}-{turn.Turn}";
        // Its connector, and no plugin's server beside it (D176 point 2): the turn whole on it, so `brainstorm_give` or
        // `brainstorm_synthesize` writes the file the driver reads and judges it against the room. A pass answers through that tool
        // alone, so on a machine with no connector it is not started, and no account is spent on a turn that cannot answer.
        var scope = BrainstormVariables.Scope(turn);
        var connector = _driver.Connector(sessionId, scope);
        if (connector is null)
        {
            return new BrainstormTurnRun(false, null, NoConnector,
                $"held  brainstorm turn {turn.Turn}: this machine has no connector, and a pass answers only through its tool");
        }

        ISessionAdapter adapter;
        HarnessSelection selection;
        try
        {
            adapter = _context.Adapters.Resolve(turn.Agent);
            selection = await VoiceAsync(turn, _context.Config, _context.Harnesses, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return new BrainstormTurnRun(false, null, "not-started", $"held  brainstorm turn {turn.Turn}: {error.Message}", error.Message);
        }

        if (!selection.Allowed)
        {
            return new BrainstormTurnRun(false, null, PassCode(selection), $"held  brainstorm turn {turn.Turn}: {selection.Refusal}");
        }

        var files = new BrainstormFiles(_context.Home);
        var passId = turn.Turn.EndsWith('r') ? turn.Turn[..^1] : turn.Turn;
        var passBefore = files.ReadPasses(turn.Id).FirstOrDefault(pass => pass.By.Pass == passId);
        await using var reports = new ActorRun(_context.Events, sessionId, "brainstorm", passBefore?.Ran, ran =>
        {
            var pass = files.ReadPasses(turn.Id).FirstOrDefault(each => each.By.Pass == passId);
            if (pass is not null)
            {
                files.WritePass(turn.Id, pass with
                {
                    Actor = "brainstorm", Choice = turn.Entry?.From ?? "before", Asked = turn.Entry?.Asked ?? ActorAsked.Before(adapter.Name), Ran = ran,
                });
            }

            return Task.CompletedTask;
        });
        var target = new SessionTarget(QuestId: "", "Brainstorm", "", "Daoris", "", turn.Room, "")
        {
            Brainstorm = turn.Id,
            Asked = turn.Entry?.Asked ?? ActorAsked.Before(adapter.Name),
            Choice = turn.Entry?.From,
            BrainstormTurn = turn.Turn,
            BrainstormAnswer = turn.Answer,
            Session = sessionId,
            Prompt = turn.Instruction,
            // CONTEXTONE1c: in no repository and reading only its room, its standing block is the workspace's part alone.
            Block = new StandingFacts { Knowledge = false },
        };
        var (info, notice) = _driver.Prepare(adapter, target, selection);
        var transcript = files.PathOf(turn.Id, "passes", $"{turn.Turn}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        // The protocol door carries the connector on the wire, with the same scope; the pipe door takes a file under the home.
        var handed = SpawnServers.Hand(adapter, info, _context.Home, sessionId, [connector]);
        var rules = _driver.HandRules(
            adapter, info, sessionId, null, null, tree: turn.Room, kept: null, brainstorm: BrainstormPermissions.ToolOf(turn.Turn));
        try
        {
            return await _driver.Holds.HoldAsync(
                "brainstorm", adapter, info, target, sessionId, transcript, turn.Room, notice, rules, handed: handed,
                refusesInput: "a brainstorm's pass is one turn, and takes no words",
                conclude: (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        // The usage is the brainstorm's own (D176 point 7): the reading names it, and its account counts it.
                        _context.Usage?.Record(new UsageEntry(sessionId, "", adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow)
                        {
                            Brainstorm = turn.Id,
                        });
                    }

                    if (ended.NotOffered is not null)
                    {
                        _context.Harnesses.Release(selection.Number, sessionId);
                        return Task.FromResult(new BrainstormTurnRun(true, selection.Profile, AgentOffers.NotOffered, $"{AgentOffers.NotOffered}  brainstorm turn {turn.Turn}"));
                    }

                    var code = turnFailed is { Length: > 0 } failure && Cooled(adapter, selection, failure, sessionId, ended.Refusal) ? Limit
                        : turnFailed is { Length: > 0 } ? "turn-failed"
                        : exitCode is null ? TimedOut
                        : exitCode != 0 ? "exited" : null;
                    return Task.FromResult(new BrainstormTurnRun(true, selection.Profile, code, $"{code ?? "completed"}  brainstorm turn {turn.Turn}"));
                },
                ct, scope: scope, handedServers: [], minutes: turn.Minutes,
                said: _driver.Said(adapter, selection, sessionId),
                // D205 §4.4: its agent's refusal of the voice's model at the open is kept as the account's reading, and the turn ends with
                // it (above) for the pass's one bounded restart on the next account; a voice has no other entry to move to on this account.
                offers: new OpenOffers(selection.Version, selection.Profile, _ => Task.FromResult<(ActorAsked Asked, string Choice)?>(null)),
                noConnector: "— this machine has no connector, so this pass cannot give its answer.", movedFirst: true)
                .ConfigureAwait(false);
        }
        finally
        {
            _context.Output?.Close(sessionId);
        }
    }

    /// <summary>
    /// A limit the turn's door reported, read as the roster reads one (D125 §2): the account cools for the time the agent named or
    /// the machine's cool-off, and no strike is counted. False where the words are no limit, or the cool-off could not be kept.
    /// </summary>
    /// <remarks>
    /// FOUNDATION1g part b: the failure is also read by the one reader (<see cref="AgentErrors.Read"/>), from the refusal the door carried with
    /// its code, so a limit it says by its code alone cools the account as a driven run's and a conversation's do (§3.1). The pass is handed
    /// no transient retry, which would open an episode the design has not decided for a voice (row 1d), so it reads the refusal itself, once.
    /// </remarks>
    private bool Cooled(ISessionAdapter adapter, HarnessSelection selection, string failure, string sessionId, AcpRefusal? refusal)
    {
        try
        {
            var read = refusal is null ? null
                : AgentErrors.Read(_context.Adapters, adapter, DoorFailure.OfPrompt(refusal), _context.Harnesses.Clock(), _context.Harnesses.Zone);
            var byCode = read is { Kind: AgentErrorKind.Limit, Limit: { } coded } ? coded : null;
            return _context.Harnesses.Limited(adapter.Name, selection.Profile, failure, sessionId, _context.Config.CoolOff, byCode) is not null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
}
